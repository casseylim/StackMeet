"use strict";
const assert = require("node:assert/strict");
const { expectedEntries, validateLedger, validateCoverage } = require("./Validate-RegressionEvidence.js");
const inventory = require("../docs/engineering/evidence/eng-06a-suite-inventory.json");
const commit = "a".repeat(40);
const options = { suite: "All", coverage: true, noBuild: false, commit };
const expected = expectedEntries(inventory, options);
const baseline = { schemaVersion: 1, commit, sdk: "8.0.100", node: "v24.0.0", os: "Synthetic",
  suites: expected.map(x => ({ ...x, exitCode: 0, durationMs: 0 })) };
const logs = new Set(expected.map(x => x.log));
const readLog = file => { if (!logs.has(file)) throw Error("missing"); return 1; };
const check = (mutate, pattern) => {
  const copy = structuredClone(baseline);
  mutate(copy);
  const result = validateLedger(copy, inventory, options, readLog);
  assert.equal(result.status, "failed");
  assert.ok(result.errors.some(x => pattern.test(x)), result.errors.join("\n"));
};
assert.equal(validateLedger(baseline, inventory, options, readLog).status, "passed");
assert.equal(expectedEntries(inventory, { suite: "DotNet", noBuild: true }).length, inventory.required.dotnet.length);
check(x => x.suites.pop(), /Missing suite/);
check(x => x.suites.push(x.suites[0]), /Duplicate suite/);
check(x => x.suites[0].exitCode = 1, /Nonzero/);
check(x => x.suites[0].exitCode = "0", /invalid exit/);
check(x => x.suites[0].durationMs = -1, /Invalid duration/);
check(x => x.commit = "b".repeat(40), /SHA mismatch/);
check(x => x.sdk = "", /runtime metadata/);
check(x => x.suites[0].log = "../outside.log", /Unexpected log path/);
check(x => x.suites.push({ kind: "Infrastructure", name: "runner", exitCode: 1 }), /Unexpected suite/);
assert.ok(validateLedger(baseline, inventory, options, () => { throw Error("missing"); }).errors.some(x => /Missing or unreadable log/.test(x)));
assert.ok(validateLedger(baseline, inventory, options, () => 0).errors.some(x => /Empty or invalid log/.test(x)));
const coverage = { schemaVersion: 1, package: "StackMeet.Api", countingUnit: "Cobertura class-level line entries",
  lines: { covered: 7, total: 10, reportedRate: 0.7 }, branches: { covered: 3, total: 8, reportedRate: 0.375 } };
assert.deepEqual(validateCoverage(coverage), []);
for (const bad of [
  { ...coverage, package: "All" },
  { ...coverage, lines: { ...coverage.lines, total: 0 } },
  { ...coverage, lines: { ...coverage.lines, covered: 11 } },
  { ...coverage, branches: { ...coverage.branches, reportedRate: 0.99 } }
]) assert.ok(validateCoverage(bad).length);
console.log("PASS ENG06B required suite, failure, metadata, log and coverage negative controls");
