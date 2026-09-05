const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const controller = read('backend/StackMeet.Api/Controllers/CompetitionsController.cs');
const activityDto = read('backend/StackMeet.Api/Dtos/CompetitionActivityDtos.cs');
const competitionDto = read('backend/StackMeet.Api/Dtos/CompetitionDtos.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const authSession = read('backend/StackMeet.Api/wwwroot/js/auth/AuthSession.js');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const migrationsDir = path.join(root, 'backend/StackMeet.Api/Migrations');
const migrations = fs.readdirSync(migrationsDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(migrationsDir, name), 'utf8'))
  .join('\n');
const jsRoot = path.join(root, 'backend/StackMeet.Api/wwwroot');
const jsFiles = [];
function collectJs(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) collectJs(full);
    else if (entry.name.endsWith('.js')) jsFiles.push(fs.readFileSync(full, 'utf8'));
  }
}
collectJs(jsRoot);
const frontendJs = jsFiles.join('\n');

assert.ok(controller.includes('[HttpGet("{id:int}/activity")]'), 'Phase 3D must expose the bounded activity descriptor GET route');
assert.ok(
  controller.includes('Task<ActionResult<CompetitionActivityResponse>> Activity(int id, CancellationToken ct)'),
  'activity descriptor endpoint must use the dedicated response contract'
);
assert.strictEqual(
  (controller.match(/activityResolver\.Resolve\(/g) || []).length,
  2,
  'Phase 3D must have exactly two resolver consumptions: original detail GET plus activity descriptor GET'
);

const activityRouteIndex = controller.indexOf('[HttpGet("{id:int}/activity")]');
const activityResolveIndex = controller.indexOf('var module = activityResolver.Resolve(item);', activityRouteIndex);
const activityMapIndex = controller.indexOf('return Ok(MapActivity(module));', activityResolveIndex);
const postIndex = controller.indexOf('[HttpPost]');
assert.ok(activityRouteIndex >= 0 && activityResolveIndex > activityRouteIndex && activityMapIndex > activityResolveIndex, 'activity endpoint must resolve after reading the competition and then map generic module metadata');
assert.ok(postIndex > activityMapIndex, 'activity descriptor resolver consumption must remain before all write actions');

assert.ok(activityDto.includes('public sealed record CompetitionActivityResponse('), 'dedicated activity descriptor DTO must exist');
assert.ok(activityDto.includes('public sealed record ActivityCapabilitiesResponse('), 'generic capability DTO must exist');
for (const capability of [
  'SupportsTeamEntries',
  'SupportsCategories',
  'SupportsStages',
  'SupportsLiveResults',
  'SupportsCertificates',
  'SupportsOfflinePackage'
]) {
  assert.ok(activityDto.includes(capability), `activity descriptor must expose generic capability: ${capability}`);
  assert.ok(controller.includes(`module.Capabilities.${capability}`), `activity mapper must project generic capability: ${capability}`);
}
assert.ok(controller.includes('static CompetitionActivityResponse MapActivity(IActivityModule module)'), 'activity descriptor must map from the generic module contract');
assert.ok(!controller.includes('SportStackingActivityModule'), 'controller must not depend directly on the Sport Stacking module implementation');
assert.ok(!activityDto.includes('Sport Stacking') && !activityDto.includes('sport-stacking'), 'shared activity DTO must remain activity-neutral');

const expectedCompetitionDto = 'public sealed record CompetitionResponse(int Id, string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, bool IsPubliclyListed, DateTime CreatedAt, DateTime UpdatedAt);';
assert.ok(competitionDto.includes(expectedCompetitionDto), 'existing competition detail response contract must remain unchanged');
assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must remain outside Phase 3D resolver routing');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy competition state must remain outside Phase 3D resolver routing');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3D must not persist activity selection');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3D must not add an activity migration');

const frontendActivityRefs = frontendJs.match(/\/activity/g) || [];
assert.strictEqual(frontendActivityRefs.length, 1, 'later frontend activation must keep exactly one bounded activity descriptor route reference');
assert.ok(authSession.includes('/activity'), 'the single frontend activity descriptor read must remain in the competition-selection shell');
assert.ok(!app.includes('/activity'), 'the Sport Stacking application monolith must not call the activity descriptor directly');

console.log('Modular Platform Foundation v1 Phase 3D activity descriptor guards passed.');
