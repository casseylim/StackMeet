const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");

const readiness = read("backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingCertificationReadinessService.cs");
const governance = read("backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingGovernanceService.cs");
const controllersDir = path.join(root, "backend", "StackMeet.Api", "Controllers");
const controllers = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith(".cs"))
  .map(name => fs.readFileSync(path.join(controllersDir, name), "utf8"))
  .join("\n");
const architecture = read("docs/architecture/STACKER_IDENTITY_SP4I.md");

assert.match(readiness, /OperatorContractVersion = "sp4h-event-finals-v1"/);
assert.match(readiness, /Task<FinalsRankingCertificationReadiness> AssessAsync/);
assert.match(readiness, /governed-finals-v2-not-explicitly-selected/);
assert.match(readiness, /competition-not-finalized/);
assert.match(readiness, /competition-state-missing/);
assert.match(readiness, /result-revision-inconsistent/);
assert.match(readiness, /result-attempts-malformed/);
assert.match(readiness, /blockers\.Count == 0/);

// SP-4I proves readiness only. The SP-4G capture block must remain until the next reviewed phase.
assert.match(governance, /governed-finals-v2 snapshot capture is blocked/i);
assert.ok(!controllers.includes("FinalsRankingCertificationReadinessService"), "SP-4I readiness must remain an internal service with no controller/API activation");
assert.ok(!controllers.includes("AssessAsync("), "controllers must not expose SP-4I certification readiness");

assert.match(architecture, /readiness assessment only/i);
assert.match(architecture, /does not remove SP-4G's v2 snapshot-capture block/i);
assert.match(architecture, /time-of-check\/time-of-use gap/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /no production database migration/i);
assert.match(architecture, /web\.config[^\n]*must never be overwritten/i);
assert.match(architecture, /SP-4J[^\n]*Snapshot Certification Activation/i);

console.log("SP-4I governed Finals v2 certification readiness guards passed.");
