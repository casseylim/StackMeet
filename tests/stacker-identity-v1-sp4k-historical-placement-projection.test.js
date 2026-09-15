const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const projector = read("backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsHistoricalPlacementProjectionService.cs");
const architecture = read("docs/architecture/STACKER_IDENTITY_SP4K.md");
const program = read("backend/StackMeet.Api/Program.cs");
const controllersDir = path.join(root, "backend", "StackMeet.Api", "Controllers");
const controllers = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith(".cs"))
  .map(name => fs.readFileSync(path.join(controllersDir, name), "utf8"))
  .join("\n");
const profileDir = path.join(root, "backend", "StackMeet.Api", "wwwroot", "profile");
const profile = fs.existsSync(profileDir)
  ? fs.readdirSync(profileDir, { recursive: true })
      .filter(name => typeof name === "string")
      .map(name => path.join(profileDir, name))
      .filter(name => fs.existsSync(name) && fs.statSync(name).isFile())
      .map(name => fs.readFileSync(name, "utf8"))
      .join("\n")
  : "";

assert.match(projector, /ProjectionVersion\s*=\s*"sp4k-historical-finals-placement-v1"/,
  "SP-4K projection contract is explicitly versioned");
assert.match(projector, /GovernedV2SnapshotSchemaVersion/,
  "SP-4K requires the governed source-v2 snapshot schema");
assert.match(projector, /GovernedFinalsV2/,
  "SP-4K requires the governed-finals-v2 rule");
assert.match(projector, /OperatorContractVersion/,
  "SP-4K verifies reviewed operator-contract provenance");
assert.match(projector, /SHA256\.HashData/,
  "SP-4K independently verifies the immutable snapshot hash");
assert.match(projector, /participantType must be Individual/i,
  "SP-4K Stacker Identity historical projection is Individual-only");
assert.match(projector, /division must be one explicit competition-snapshot division/i,
  "SP-4K refuses bare all-division placement");
assert.match(projector, /result\.Penalty >= 999m/,
  "SP-4K preserves governed-v2 scratch-penalty precedence");
assert.match(projector, /valid\[0\] \+ appliedPenalty/,
  "SP-4K governed-v2 primary comparator uses official best");
assert.match(projector, /rank = index \+ 1/,
  "SP-4K preserves competition ranking gaps after ties");

assert.ok(!projector.includes("database.CompetitionStates"),
  "SP-4K projector cannot read current CompetitionState rows");
assert.ok(!projector.includes("database.CompetitionResults"),
  "SP-4K projector cannot read current CompetitionResult rows");
assert.ok(!projector.includes("database.Stackers"),
  "SP-4K projector cannot reconstruct historical scope from current Stacker rows");
assert.ok(!controllers.includes("FinalsHistoricalPlacementProjectionService"),
  "SP-4K projection is not exposed through controllers/API");
assert.ok(!program.includes("FinalsHistoricalPlacementProjectionService"),
  "SP-4K projection is not globally activated through startup/DI");
assert.ok(!profile.includes("FinalsHistoricalPlacementProjection") && !profile.includes("historicalPlacement"),
  "SP-4K does not publish historical placement on the public profile");

assert.match(architecture, /never from current live participant\/result state/i);
assert.match(architecture, /Legacy `finals-ranking-source-v1` snapshots fail closed/i);
assert.match(architecture, /category\/gender filters \*\*before\*\* placement grouping and ranking/i);
assert.match(architecture, /no public API for historical placement/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /no production database migration/i);
assert.match(architecture, /web\.config[^\n]*must never be overwritten/i);

console.log("SP-4K immutable historical Finals placement projection guards passed.");
