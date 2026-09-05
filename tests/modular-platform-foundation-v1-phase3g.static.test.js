const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const auth = read('backend/StackMeet.Api/wwwroot/js/auth/AuthSession.js');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const index = read('backend/StackMeet.Api/wwwroot/index.html');
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
  'Phase 3G must preserve the compatibility-default capability semantics established in Phase 3F'
);
assert.ok(auth.includes('function teamEntryRoutes()'), 'Phase 3G must derive team-entry routes through one bounded shell helper');
assert.ok(
  auth.includes('template.content.querySelectorAll(".team-builder-action[data-route]")'),
  'team-entry routes must be discovered from existing semantic shell markup rather than hard-coded route names'
);
assert.ok(
  auth.includes('.map(node => node.dataset.route)') && auth.includes('.filter(Boolean)'),
  'team-entry route discovery must consume only declared route metadata'
);
assert.strictEqual(
  (auth.match(/activityRuntime\.supports\("supportsTeamEntries"\)/g) || []).length,
  2,
  'Phase 3G must consume supportsTeamEntries only for route guarding and shell presentation'
);
assert.ok(
  /!activityRuntime\.supports\("supportsTeamEntries"\)[\s\S]{0,180}teamEntryRoutes\(\)\.has\(requestedRoute\)[\s\S]{0,120}location\.hash = "dashboard"/.test(auth),
  'direct navigation to a declared team-entry route must fall back to Dashboard only when team entries are explicitly unsupported'
);
assert.ok(
  /const teamEntriesEnabled = activityRuntime\.supports\("supportsTeamEntries"\);[\s\S]{0,160}const teamRoutes = teamEntryRoutes\(\);[\s\S]{0,220}document\.querySelectorAll\("\[data-route\]"\)/.test(auth),
  'Phase 3G presentation must remain generic and route-metadata driven'
);
assert.ok(
  /if \(teamRoutes\.has\(node\.dataset\.route\)\) node\.hidden = !teamEntriesEnabled;/.test(auth),
  'only controls whose declared routes are team-entry routes may be hidden by supportsTeamEntries'
);
assert.ok(
  auth.includes('new MutationObserver(() => applyActivityShellCapabilities()).observe(view, { childList: true, subtree: true });'),
  'Phase 3G must reapply capability presentation when route templates are rendered into the view'
);
assert.ok(!/\b(doubles|relay)\b/i.test(auth), 'generic activity shell runtime must not learn Sport Stacking team route names');

const settingsStart = index.indexOf('<template id="settingsView">');
const settingsEnd = index.indexOf('</template>', settingsStart);
assert.ok(settingsStart >= 0 && settingsEnd > settingsStart, 'Settings template must remain available as the semantic team-entry route source');
const settingsTemplate = index.slice(settingsStart, settingsEnd);
assert.strictEqual(
  (settingsTemplate.match(/class="team-builder-action" data-route=/g) || []).length,
  2,
  'the existing settings shell must still declare exactly two team-builder route entry points'
);
assert.ok(index.includes('data-participant-route="stackers"'), 'individual participant navigation must remain available');
assert.ok(index.includes('data-participant-route="doubles"') && index.includes('data-participant-route="relay"'), 'existing Sport Stacking team tabs must remain present for compatible modules');

assert.ok(/function routeIsAvailable\(\[key\]\) \{\s*return true;\s*\}/.test(app), 'Sport Stacking application route logic must remain unchanged in Phase 3G');
assert.ok(!app.includes('StackMeetActivityRuntime'), 'activity capability logic must remain outside the Sport Stacking monolith');
assert.ok(!app.includes('supportsTeamEntries'), 'Sport Stacking app logic must not learn the generic team-entry capability');
assert.ok(controller.includes('[HttpGet("{id:int}/activity")]'), 'Phase 3G must continue using the existing read-only activity descriptor contract');
assert.strictEqual((auth.match(/\/activity/g) || []).length, 1, 'Phase 3G must not add another descriptor endpoint call');
assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must remain outside activity routing');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy competition state must remain outside activity routing');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3G must not persist activity selection');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3G must not add an activity migration');

console.log('Modular Platform Foundation v1 Phase 3G team-entry shell capability guards passed.');
