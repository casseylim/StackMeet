const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");

const governance = read("backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingGovernanceService.cs");
const readiness = read("backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingCertificationReadinessService.cs");
const controllersDir = path.join(root, "backend", "StackMeet.Api", "Controllers");
const controllers = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith(".cs"))
  .map(name => fs.readFileSync(path.join(controllersDir, name), "utf8"))
  .join("\n");
const architecture = read("docs/architecture/STACKER_IDENTITY_SP4J.md");

assert.match(governance, /CertifyGovernedV2SnapshotAsync/,
  "SP-4J exposes a separate explicit governed-v2 certification method");
assert.match(governance, /governed-finals-v2 snapshot capture is blocked until a later phase/i,
  "legacy generic capture path remains fail-closed for governed v2");
assert.match(governance, /IsolationLevel\.Serializable/,
  "certification shares serializable snapshot transaction boundary");
assert.match(governance, /LockCompetitionAsync/);
assert.match(governance, /ReadGovernanceAsync\(competitionId, forUpdate: true/);
assert.match(governance, /LockCompetitionStateAsync/);
assert.match(governance, /FinalsRankingCertificationEvidenceValidator\.Validate/,
  "SP-4I evidence rules are re-evaluated inside the capture transaction");
assert.match(governance, /governed-finals-v2 snapshot certification blocked:/,
  "failed certification reports stable readiness blockers");
assert.match(governance, /LegacySnapshotSchemaVersion\s*=\s*"finals-ranking-source-v1"/,
  "legacy snapshot schema remains frozen");
assert.match(governance, /GovernedV2SnapshotSchemaVersion\s*=\s*"finals-ranking-source-v2"/,
  "v2 certification uses a distinct versioned evidence schema");
assert.match(governance, /FinalsRankingCertificationReadinessService\.OperatorContractVersion/,
  "v2 certified evidence freezes the reviewed operator contract version");
assert.match(governance, /GovernedV2FinalsRankingSourceSnapshot/,
  "v2 uses a provenance-aware source envelope rather than changing legacy payload shape");
assert.match(governance, /SHA256\.HashData/,
  "certified v2 source evidence remains hash-protected");

assert.match(readiness, /internal static class FinalsRankingCertificationEvidenceValidator/,
  "readiness and activation share one internal evidence validator");
assert.ok(!controllers.includes("CertifyGovernedV2SnapshotAsync"),
  "SP-4J does not expose certification through a controller/API");
assert.ok(!controllers.includes("FinalsRankingCertificationEvidenceValidator"),
  "internal certification validation is not a controller concern");

assert.match(architecture, /no historical placement/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /no production database migration/i);
assert.match(architecture, /web\.config[^\n]*must never be overwritten/i);

console.log("SP-4J governed Finals v2 snapshot certification activation guards passed.");
