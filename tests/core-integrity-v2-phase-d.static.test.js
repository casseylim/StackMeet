const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const references = read("backend/StackMeet.Api/Services/CompetitionParticipantReferenceService.cs");
const stackers = read("backend/StackMeet.Api/Controllers/StackersController.cs");
const harness = read("tests/CoreIntegrityIntegrationTests/Program.cs");
const ci = read(".github/workflows/ci.yml");

for (const field of ["members", "childStackerId", "parentStackerId", "stackerOneId", "stackerTwoId", "selectedQualifiers"]) {
  assert.ok(references.includes(`\"${field}\"`), `missing protected participant reference field: ${field}`);
}

assert.match(references, /ContainsReferenceValue/);
assert.match(references, /StringComparison\.OrdinalIgnoreCase/);
assert.match(references, /catch \(JsonException\)[\s\S]*return true;/);
assert.match(stackers, /references\.ContainsParticipant\(state\.JsonData, item\.StackerCode\)/);

for (const scenario of [
  "relay members array reference protected",
  "child parent doubles alias protected",
  "legacy doubles alias protected",
  "participant references compare case-insensitively",
  "malformed state fails closed for deletion safety",
  "external parent name is not a participant reference",
  "unrelated text is not a participant reference"
]) {
  assert.ok(harness.includes(scenario), `missing executable Phase D scenario: ${scenario}`);
}

assert.ok(ci.includes("Run core integrity Phase D static guards"), "Phase D static guard is not enforced explicitly by CI");
console.log("Core integrity V2 Phase D static tests passed.");
