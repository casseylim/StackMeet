"use strict";
// Mutation probes run the original designated gates in disposable directories.
// No tracked application file is modified, and no network service is started.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const crypto = require("node:crypto");
const { spawnSync } = require("node:child_process");
const root = path.resolve(__dirname, "..");
const temp = fs.mkdtempSync(path.join(os.tmpdir(), "stackmeet-eng06c-"));
const api = "backend/StackMeet.Api/";
const app = api + "wwwroot/app.js";
const provider = api + "wwwroot/js/storage/ApiProvider.js";
const publicController = api + "Controllers/PublicResultsController.cs";
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
const original = new Map();

function copy(relative) {
  const bytes = fs.readFileSync(path.join(root, relative));
  original.set(relative, hash(bytes));
  const target = path.join(temp, relative);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, bytes);
}
function mutate(relative, before, after) {
  const target = path.join(temp, relative);
  const source = fs.readFileSync(target, "utf8");
  assert.equal(source.split(before).length, 2, "Mutation anchor must appear exactly once: " + relative);
  fs.writeFileSync(target, source.replace(before, () => after));
}
function run(test) {
  const result = spawnSync(process.execPath, [path.join(temp, test)], {
    cwd: temp, encoding: "utf8", timeout: 30000, maxBuffer: 4 * 1024 * 1024
  });
  assert.ifError(result.error);
  assert.equal(result.signal, null, "Gate must terminate normally");
  return { status: result.status, output: result.stdout + "\n" + result.stderr };
}
function probe(label, test, files, mutateCopy, expectedFailure) {
  for (const relative of new Set([test, ...files])) copy(relative);
  const baseline = run(test);
  assert.equal(baseline.status, 0, label + " clean gate failed:\n" + baseline.output);
  mutateCopy();
  const broken = run(test);
  assert.equal(broken.status, 1, label + " mutation must fail the designated gate");
  assert.match(broken.output, /AssertionError/, label + " must fail an assertion, not infrastructure");
  assert.match(broken.output, expectedFailure, label + " wrong failure:\n" + broken.output);
  for (const relative of files) copy(relative);
  assert.equal(run(test).status, 0, label + " restored copy must pass");
  console.log("PASS ENG06C " + label + " clean/mutant/restored gate");
}

try {
  probe("Finals-only rule", "tests/finals-only-overall.test.js", [app],
    () => mutate(app, 'const stage = kind === "overall" ? "Finals" : val("finalsReportStage") || "Finals";',
      'const stage = kind === "overall" ? "Prelims" : val("finalsReportStage") || "Finals";'),
    /Prelims[\s\S]*Finals/);
  const privacyTest = "tests/public-results-portal.static.test.js";
  const privacySource = fs.readFileSync(path.join(root, privacyTest), "utf8");
  const privacyFiles = [...new Set([...privacySource.matchAll(/\bread\("([^"]+)"\)/g)].map(match => match[1]))];
  assert.ok(privacyFiles.includes(publicController));
  probe("public privacy", privacyTest, privacyFiles,
    () => { const target = path.join(temp, publicController); fs.appendFileSync(target, "\n// Synthetic negative control: email = \"fixture@example.test\"\n"); },
    /Public responses must not expose email addresses/);
  probe("stale-write baseline", "tests/competition-state-concurrency.test.js",
    [provider, api + "wwwroot/js/storage/Repository.js", api + "Models/CompetitionState.cs", api + "Controllers/CompetitionStateController.cs"],
    () => mutate(provider, "if (acceptRevision) this.#captureEtag(response);", "this.#captureEtag(response);"),
    /ordinary load must not accept a remote revision/);
  for (const [relative, digest] of original) {
    assert.equal(hash(fs.readFileSync(path.join(root, relative))), digest, "Original source changed: " + relative);
  }
  console.log("PASS ENG06C tracked source hashes unchanged");
} finally {
  fs.rmSync(temp, { recursive: true, force: true });
}
