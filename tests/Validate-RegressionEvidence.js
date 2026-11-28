"use strict";
const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const root = path.join(__dirname, "..");
const inventory = require("../docs/engineering/evidence/eng-06a-suite-inventory.json");

function expectedEntries(manifest, { suite = "All", noBuild = false, coverage = false } = {}) {
  assert.ok(["All", "DotNet"].includes(suite), "Invalid suite mode");
  const projects = manifest.required.dotnet.map(file => path.posix.basename(file, ".csproj"));
  const expected = [];
  const add = (kind, name, prefix = "logs/") => expected.push({ kind, name, log: prefix + name + ".log" });
  if (!noBuild) projects.forEach(name => add("Build", name + "-build"));
  if (coverage) add("Coverage", "coverage");
  projects.forEach(name => add("DotNet", name, coverage ? "covered/logs/" : "logs/"));
  if (suite === "All") {
    manifest.required.node.forEach(file => add("Node", path.posix.basename(file)));
    add("Node", "storage-smoke");
    add("PowerShell", "release-safety");
  }
  assert.equal(new Set(expected.map(x => x.kind + ":" + x.name)).size, expected.length, "Duplicate expected suite identity");
  return expected;
}

function validateLedger(report, manifest, options, readLog) {
  const errors = [];
  const fail = message => errors.push(message);
  const expected = expectedEntries(manifest, options);
  const key = entry => entry.kind + ":" + entry.name;
  const expectedByKey = new Map(expected.map(entry => [key(entry), entry]));
  const seen = new Set(), seenLogs = new Set();
  if (report?.schemaVersion !== 1) fail("Unsupported ledger schema");
  if (!/^[a-f0-9]{40}$/i.test(options.commit || "") || report?.commit !== options.commit) fail("Source SHA mismatch");
  for (const field of ["sdk", "node", "os"]) {
    if (typeof report?.[field] !== "string" || !report[field].trim()) fail("Missing runtime metadata: " + field);
  }
  if (!Array.isArray(report?.suites)) fail("Suite ledger is not an array");
  for (const entry of Array.isArray(report?.suites) ? report.suites : []) {
    if (!entry || typeof entry !== "object") { fail("Malformed suite record"); continue; }
    const id = key(entry);
    if (seen.has(id)) fail("Duplicate suite: " + id);
    seen.add(id);
    const required = expectedByKey.get(id);
    if (!required) { fail("Unexpected suite: " + id); continue; }
    if (!Number.isInteger(entry.exitCode) || entry.exitCode !== 0) fail("Nonzero or invalid exit: " + id);
    if (!Number.isSafeInteger(entry.durationMs) || entry.durationMs < 0) fail("Invalid duration: " + id);
    if (entry.log !== required.log) { fail("Unexpected log path: " + id); continue; }
    if (seenLogs.has(entry.log)) fail("Duplicate log: " + entry.log);
    seenLogs.add(entry.log);
    try {
      const bytes = readLog(entry.log);
      if (!Number.isSafeInteger(bytes) || bytes < 1) fail("Empty or invalid log: " + entry.log);
    } catch { fail("Missing or unreadable log: " + entry.log); }
  }
  for (const entry of expected) if (!seen.has(key(entry))) fail("Missing suite: " + key(entry));
  const counts = {};
  for (const entry of expected) counts[entry.kind] = (counts[entry.kind] || 0) + 1;
  return { schemaVersion: 1, sourceSha: options.commit, suite: options.suite, coverage: options.coverage,
    expected: expected.length, observed: Array.isArray(report?.suites) ? report.suites.length : 0,
    expectedByKind: counts, status: errors.length ? "failed" : "passed", errors };
}

function validateCoverage(summary) {
  const errors = [];
  if (summary?.schemaVersion !== 1 || summary?.package !== "StackMeet.Api"
      || summary?.countingUnit !== "Cobertura class-level line entries") errors.push("Wrong API coverage scope");
  for (const metric of ["lines", "branches"]) {
    const item = summary?.[metric];
    if (!Number.isSafeInteger(item?.covered) || !Number.isSafeInteger(item?.total)
        || item.total <= 0 || item.covered < 0 || item.covered > item.total
        || !Number.isFinite(item?.reportedRate) || Math.abs(item.reportedRate - item.covered / item.total) > 1e-9)
      errors.push("Invalid or inconsistent API " + metric + " counts");
  }
  return errors;
}

function run(directory, commit) {
  const output = path.resolve(directory);
  const options = { suite: "All", noBuild: false, coverage: true, commit };
  const realOutput = fs.realpathSync(output);
  const load = file => JSON.parse(fs.readFileSync(path.join(output, file), "utf8"));
  const readLog = file => {
    const resolved = fs.realpathSync(path.join(output, file));
    const relative = path.relative(realOutput, resolved);
    if (relative === ".." || relative.startsWith(".." + path.sep) || path.isAbsolute(relative)) throw Error("Log outside evidence directory");
    const stat = fs.statSync(resolved);
    if (!stat.isFile()) throw Error("Not a file");
    return stat.size;
  };
  let result;
  try {
    result = validateLedger(load("results.json"), inventory, options, readLog);
    const child = validateLedger(load("covered/results.json"), inventory,
      { suite: "DotNet", noBuild: true, coverage: false, commit }, file => readLog("covered/" + file));
    result.coveredLedger = child;
    result.errors.push(...child.errors.map(message => "Covered child: " + message));
    const parent = load("results.json");
    const covered = load("covered/results.json");
    for (const field of ["sdk", "node", "os"]) if (parent[field] !== covered[field]) result.errors.push("Covered runtime mismatch: " + field);
    for (const entry of covered.suites || []) {
      const merged = parent.suites.find(item => item.kind === entry.kind && item.name === entry.name);
      if (!merged || merged.exitCode !== entry.exitCode || merged.durationMs !== entry.durationMs
          || merged.log !== "covered/" + entry.log) result.errors.push("Covered ledger merge mismatch: " + entry.name);
    }
    result.apiCoverage = load("api-coverage.json");
    if (result.apiCoverage.sourceSha !== commit) result.errors.push("API coverage source SHA mismatch");
    result.errors.push(...validateCoverage(result.apiCoverage));
  } catch (error) {
    result ||= { schemaVersion: 1, sourceSha: commit, errors: [] };
    result.errors.push("Evidence infrastructure: " + error.message);
  }
  result.status = result.errors.length ? "failed" : "passed";
  fs.writeFileSync(path.join(output, "validation.json"), JSON.stringify(result, null, 2) + "\n");
  console.log("ENG06B ledger validation " + result.status + ": " + (result.observed ?? 0) + "/" + (result.expected ?? 0) + " records");
  for (const error of result.errors) console.error(error);
  return result.errors.length ? 1 : 0;
}
if (require.main === module) {
  if (process.argv.length !== 4) { console.error("Usage: node Validate-RegressionEvidence.js <evidence-directory> <source-sha>"); process.exitCode = 1; }
  else process.exitCode = run(process.argv[2], process.argv[3]);
}
module.exports = { expectedEntries, validateLedger, validateCoverage, run };
