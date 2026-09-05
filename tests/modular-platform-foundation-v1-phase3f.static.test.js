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

assert.ok(
  auth.includes('return this.descriptor?.capabilities?.[capability] !== false;'),
  'Phase 3F capability evaluation must preserve compatibility unless a module explicitly disables a capability'
);
assert.ok(auth.includes('function enforceActivityShellRoute()'), 'Phase 3F must define one bounded shell route guard');
assert.ok(auth.includes('function applyActivityShellCapabilities()'), 'Phase 3F must define one bounded shell presentation hook');
assert.ok(auth.includes('function observeActivityShell()'), 'Phase 3F must keep capability presentation synchronized with nav rendering');
assert.strictEqual(
  (auth.match(/activityRuntime\.supports\("supportsLiveResults"\)/g) || []).length,
  2,
  'Phase 3F must consume exactly one generic capability for route and presentation checks'
);
assert.strictEqual(
  (auth.match(/\[data-route="leaderboard"\]/g) || []).length,
  1,
  'Phase 3F must gate exactly the Leader Board navigation surface'
);
assert.ok(
  /!activityRuntime\.supports\("supportsLiveResults"\)[\s\S]{0,120}location\.hash\.replace\("#", ""\) === "leaderboard"[\s\S]{0,120}location\.hash = "dashboard"/.test(auth),
  'direct Leader Board navigation must fall back to Dashboard only when live results are explicitly unsupported'
);
assert.ok(
  auth.includes('new MutationObserver(() => applyActivityShellCapabilities()).observe(nav, { childList: true, subtree: true });'),
  'Phase 3F must reapply only the shell presentation rule when the navigation is rebuilt'
);
assert.ok(auth.includes('window.addEventListener("hashchange", enforceActivityShellRoute);'), 'Phase 3F must guard later direct hash navigation');
assert.ok(
  /function clearActivityDescriptor\(\) \{[\s\S]*?activityRuntime\.descriptor = null;[\s\S]*?applyActivityShellCapabilities\(\);[\s\S]*?\}/.test(auth),
  'descriptor clearing must immediately restore compatibility shell behavior'
);
assert.ok(
  /activityRuntime\.descriptor = descriptor && typeof descriptor === "object" \? descriptor : null;\s*applyActivityShellCapabilities\(\);/.test(auth),
  'successful descriptor loading must apply the bounded shell capability before application startup continues'
);
assert.ok(!/sport-stacking|Sport Stacking|SportStackingActivityModule/.test(auth), 'Phase 3F shell behavior must remain free of Sport Stacking implementation knowledge');

assert.ok(app.includes('["leaderboard", "Leader Board"]'), 'existing Leader Board route definition must remain present for compatible modules');
assert.ok(/function routeIsAvailable\(\[key\]\) \{\s*return true;\s*\}/.test(app), 'Sport Stacking app route logic must remain unchanged in Phase 3F');
assert.ok(!app.includes('StackMeetActivityRuntime'), 'activity capability logic must remain outside the Sport Stacking monolith');
assert.ok(!app.includes('supportsLiveResults'), 'Sport Stacking app logic must not learn the generic live-results capability yet');

assert.ok(controller.includes('[HttpGet("{id:int}/activity")]'), 'Phase 3F must continue using the existing read-only activity descriptor contract');
assert.strictEqual((auth.match(/\/activity/g) || []).length, 1, 'Phase 3F must not add another descriptor endpoint call');
assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must remain outside activity routing');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy competition state must remain outside activity routing');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3F must not persist activity selection');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3F must not add an activity migration');

console.log('Modular Platform Foundation v1 Phase 3F live-results shell capability guards passed.');
