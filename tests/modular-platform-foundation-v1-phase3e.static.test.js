const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const auth = read('backend/StackMeet.Api/wwwroot/js/auth/AuthSession.js');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const controller = read('backend/StackMeet.Api/Controllers/CompetitionsController.cs');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const migrationsDir = path.join(root, 'backend/StackMeet.Api/Migrations');
const migrations = fs.readdirSync(migrationsDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(migrationsDir, name), 'utf8'))
  .join('\n');

assert.ok(auth.includes('window.StackMeetActivityRuntime = activityRuntime;'), 'Phase 3E must expose one generic frontend activity runtime seam');
assert.ok(
  auth.includes('return this.descriptor?.capabilities?.[capability] !== false;'),
  'activity runtime capability lookup must remain generic while preserving compatibility when metadata is absent'
);
assert.ok(auth.includes('descriptor: null'), 'activity runtime must start without a selected descriptor');
assert.ok(!/sport-stacking|Sport Stacking|SportStackingActivityModule/.test(auth), 'frontend activity runtime must remain free of Sport Stacking implementation knowledge');

const loaderStart = auth.indexOf('async function loadActivityDescriptor(session)');
const loaderEnd = auth.indexOf('function initializeLoginLanguage()', loaderStart);
assert.ok(loaderStart >= 0 && loaderEnd > loaderStart, 'Phase 3E must define a bounded activity descriptor loader');
const loader = auth.slice(loaderStart, loaderEnd);

assert.ok(loader.includes('const competitionId = Number(session?.selectedCompetitionSqlId);'), 'descriptor loading must use the already-authorized numeric SQL competition id');
assert.ok(loader.includes('Number.isInteger(competitionId)') && loader.includes('competitionId <= 0'), 'descriptor loading must reject invalid SQL competition ids');
assert.ok(loader.includes('session?.localFileTest'), 'local-file compatibility mode must not depend on the server descriptor endpoint');
assert.ok(loader.includes('`/api/competitions/${encodeURIComponent(competitionId)}/activity`'), 'descriptor loader must call only the Phase 3D activity endpoint');
assert.ok(loader.includes('method: "GET"'), 'activity descriptor bootstrap must be read-only');
for (const writeMethod of ['POST', 'PUT', 'PATCH', 'DELETE']) {
  assert.ok(!loader.includes(`method: "${writeMethod}"`), `activity descriptor bootstrap must not issue ${writeMethod}`);
}
assert.ok(loader.includes('...authHeaders()'), 'activity descriptor read must use the existing authenticated request headers');
assert.ok(loader.includes('activityRuntime.descriptor = descriptor && typeof descriptor === "object" ? descriptor : null;'), 'successful descriptor reads must populate only runtime memory');
assert.ok(loader.includes('console.warn("Competition activity descriptor unavailable; continuing with compatibility behavior."'), 'descriptor failures must be explicitly non-fatal');
assert.ok(loader.includes('return null;'), 'descriptor failure/compatibility paths must return null rather than block startup');

assert.ok(
  /if \(hasSelectedCompetition\(session\)\) \{\s*await loadActivityDescriptor\(session\);\s*return session;\s*\}/.test(auth),
  'existing selected sessions must bootstrap the descriptor before application startup'
);
assert.ok(
  /session = chooseCompetition\([\s\S]*?updateChrome\(\);\s*await loadActivityDescriptor\(session\);\s*resolve\(session\);/.test(auth),
  'newly selected competitions must bootstrap the descriptor before resolving login'
);
assert.ok(
  /function clearSession\(\) \{[\s\S]*?clearActivityDescriptor\(\);[\s\S]*?\}/.test(auth),
  'logout/session clearing must also clear the in-memory activity descriptor'
);
assert.ok(
  /function chooseCompetition\([\s\S]*?clearActivityDescriptor\(\);[\s\S]*?selectedCompetitionSqlId: selected\.competitionId/.test(auth),
  'competition switching must clear stale activity metadata before saving the new selection'
);
assert.ok(!/sessionStorage\.setItem\([^\n]*activity/i.test(auth), 'Phase 3E must not persist the resolved activity descriptor in browser storage');

assert.ok(controller.includes('[HttpGet("{id:int}/activity")]'), 'Phase 3E frontend bootstrap must continue consuming the bounded Phase 3D GET contract');
assert.strictEqual((auth.match(/\/activity/g) || []).length, 1, 'AuthSession must contain exactly one activity endpoint reference');
assert.ok(!app.includes('/activity'), 'Sport Stacking application runtime must not call the activity endpoint directly');
assert.ok(!app.includes('StackMeetActivityRuntime'), 'the Sport Stacking monolith must remain independent of the generic activity runtime');
assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must remain outside activity routing');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy competition state must remain outside activity routing');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3E must not persist activity selection');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3E must not add an activity migration');

console.log('Modular Platform Foundation v1 Phase 3E frontend descriptor bootstrap guards passed.');
