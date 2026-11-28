"use strict";

// ENG-06A continuation: compare reviewed inventory with repository sources
// and existing CI execution paths. Never execute credentialed smoke scripts.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.join(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const inventory = JSON.parse(read("docs/engineering/evidence/eng-06a-suite-inventory.json"));
const historical = JSON.parse(read("docs/engineering/evidence/eng-06a-regression-summary.json"));
const workflow = read(".github/workflows/ci.yml");
const collector = read("tests/Invoke-RegressionEvidence.ps1");
const sorted = values => [...values].sort();
const relative = file => path.relative(root, file).split(path.sep).join("/");
function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) return ["bin", "obj", "node_modules", ".git"].includes(entry.name) ? [] : walk(file);
    return entry.isFile() ? [relative(file)] : [];
  });
}
function exactInventory(label, expected, actual) {
  assert.equal(new Set(expected).size, expected.length, label + " duplicate inventory entries");
  const missing = expected.filter(file => !actual.includes(file));
  const extra = actual.filter(file => !expected.includes(file));
  assert.deepEqual({ missing, extra }, { missing: [], extra: [] }, label + " inventory drift: review additions/removals explicitly");
}
assert.equal(inventory.schemaVersion, 1);
const files = walk(path.join(root, "tests"));
const js = files.filter(file => /^tests\/[^/]+\.test\.js$/.test(file));
const projects = files.filter(file => file.endsWith(".csproj"));
exactInventory("JavaScript", inventory.required.node, js);
exactInventory(".NET", inventory.required.dotnet, projects);
assert.deepEqual(files.filter(file => file.endsWith(".test.js") && !js.includes(file)), [], "Nested JavaScript test has no automatic execution path");
exactInventory("PowerShell", [...inventory.required.offlinePowerShell, ...inventory.separate.filter(item => item.path.startsWith("tests/")).map(item => item.path)], files.filter(file => file.endsWith(".ps1")));
for (const file of [...inventory.required.storageSmoke, ...inventory.required.offlinePowerShell, ...inventory.separate.map(item => item.path)]) {
  assert.ok(fs.statSync(path.join(root, file)).isFile(), "Missing " + file);
}
const ciProjects = [...workflow.matchAll(/dotnet run --project (tests\/[^\s]+\.csproj)/g)].map(match => match[1]);
exactInventory("Explicit CI console invocations", inventory.required.dotnet, ciProjects);
assert.ok(workflow.includes("Get-ChildItem tests -Filter *.test.js"), "Existing CI JS discovery removed");
assert.ok(collector.includes("Get-ChildItem tests -Filter *.test.js"), "Evidence JS discovery removed");
assert.ok(collector.includes("Get-ChildItem tests -Recurse -Filter *.csproj"), "Evidence .NET discovery removed");
for (const file of [...inventory.required.storageSmoke, ...inventory.required.offlinePowerShell]) {
  assert.ok(workflow.includes(file), "Missing existing CI invocation: " + file);
  assert.ok(collector.includes(file), "Missing evidence invocation: " + file);
}
assert.ok(workflow.includes("Test-ReleaseDllReproducibility.ps1"), "Pinned-source release check missing");
assert.ok(workflow.includes("6182e405ced08c24cf5f8738f1172c8df5197d3e"), "Reviewed release SHA changed; separately governed review required");
for (const item of inventory.separate.filter(item => item.mode.startsWith("isolated-") || item.mode === "owner-approved-production-readonly")) {
  assert.ok(!workflow.includes(item.path) && !collector.includes(item.path), "Credentialed script must not be automatically invoked: " + item.path);
  assert.ok(item.reason, "Missing classification reason");
}
// The historical evidence is a floor, not a frozen test count. Preserve its
// known suites; the reviewed inventory must explicitly classify any new suite.
for (const entry of historical.suites) {
  if (entry.kind === "DotNet") assert.ok(projects.some(file => path.basename(file, ".csproj") === entry.name), "Historical .NET suite lost: " + entry.name);
  if (entry.kind === "Node" && entry.name !== "storage-smoke") assert.ok(js.includes("tests/" + entry.name), "Historical JS suite lost: " + entry.name);
}
// Negative controls exercise the same comparison, without modifying sources.
assert.throws(() => exactInventory("missing control", ["a", "b"], ["a"]), /inventory drift/);
assert.throws(() => exactInventory("unclassified control", ["a"], ["a", "b"]), /inventory drift/);
assert.throws(() => exactInventory("duplicate control", ["a", "a"], ["a"]), /duplicate inventory/);
console.log("PASS ENG06A inventory missing/unclassified/duplicate negative controls");
console.log("PASS ENG06A reviewed inventory: " + projects.length + " .NET, " + js.length + " JavaScript, storage smoke, offline PowerShell, explicit separate gates");
