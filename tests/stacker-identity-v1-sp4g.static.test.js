const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const service = read('backend/StackMeet.Api/Activities/SportStacking/Ranking/FinalsRankingGovernanceService.cs');
const migration = read('backend/StackMeet.Api/Migrations/20260914093000_FinalsRankingGovernanceSp4g.cs');
const admin = read('backend/StackMeet.Api/Controllers/CompetitionAdminController.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const finalsEngine = read('backend/StackMeet.Api/wwwroot/js/reports/FinalsReportEngine.js');
const policy = read('backend/StackMeet.Api/wwwroot/js/reports/FinalsRankingPolicy.js');
const architecture = read('docs/architecture/STACKER_IDENTITY_SP4G.md');

assert.match(service, /LegacyFinalsV1\s*=\s*"legacy-finals-v1"/, 'server keeps canonical legacy v1 identifier');
assert.match(service, /GovernedFinalsV2\s*=\s*"governed-finals-v2"/, 'server keeps canonical governed v2 identifier');
assert.match(policy, /LEGACY_FINALS_V1\s*=\s*"legacy-finals-v1"/, 'browser policy keeps same legacy identifier');
assert.match(policy, /GOVERNED_FINALS_V2\s*=\s*"governed-finals-v2"/, 'browser policy keeps same governed identifier');
assert.match(service, /string\.IsNullOrWhiteSpace\(value\)\) return LegacyFinalsV1/, 'unversioned stored competitions resolve to legacy v1');
assert.match(service, /governed-finals-v2 snapshot capture is blocked/, 'v2 historical certification is fail-closed in SP-4G');
assert.match(service, /CompetitionState is required to preserve the authoritative competition-time division snapshot/, 'competition-state division provenance is mandatory');
assert.match(service, /SHA256\.HashData/, 'snapshot evidence is hashed with SHA-256');
assert.match(service, /Stage == "Finals"/, 'snapshot evidence is Finals-stage bounded');

assert.match(migration, /CreateTable\(\s*name: "FinalsRankingGovernance"/, 'migration creates dedicated governance table');
assert.match(migration, /PK_FinalsRankingGovernance/, 'one governance row is keyed by competition');
assert.match(migration, /CK_FinalsRankingGovernance_RuleVersion/, 'database constrains supported rule versions');
assert.match(migration, /CK_FinalsRankingGovernance_SnapshotCompleteness/, 'database rejects partial snapshot provenance');
assert.match(migration, /TR_FinalsRankingGovernance_ImmutableSnapshot/, 'database installs immutable snapshot trigger');
assert.match(migration, /THROW 51041/, 'captured snapshot mutation fails explicitly');
assert.match(migration, /onDelete: ReferentialAction\.Restrict/, 'captured governance evidence is not cascaded away with competition deletion');

assert.ok(!admin.includes('FinalsRankingGovernanceService'), 'SP-4G does not wire governance into current admin status flow');
assert.ok(!program.includes('FinalsRankingGovernanceService'), 'SP-4G does not activate governance through application DI/runtime');
assert.ok(!finalsEngine.includes('governed-finals-v2'), 'current operator Finals engine is not silently switched to v2');
assert.ok(!finalsEngine.includes('FinalsRankingGovernance'), 'current operator Finals engine does not read persisted governance yet');

assert.match(architecture, /no production deployment/i, 'architecture explicitly prohibits SP-4G production deployment');
assert.match(architecture, /web\.config[^\n]*must never be overwritten/i, 'architecture preserves protected production web.config rule');
assert.match(architecture, /does not publish placement/i, 'SP-4G does not publish historical placement');
assert.match(architecture, /migration-managed/i, 'architecture documents intentionally non-EF-tracked evidence table');
assert.match(architecture, /governed-finals-v2[^\n]*blocked/i, 'architecture documents v2 certification block');

console.log('SP-4G static governance guards passed.');
