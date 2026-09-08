const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const chess = read('backend/StackMeet.Api/Activities/ChessActivityModule.cs');
const sport = read('backend/StackMeet.Api/Activities/SportStackingActivityModule.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const registry = read('backend/StackMeet.Api/Activities/ActivityModuleRegistry.cs');
const runtime = read('tests/Phase5AActivityModuleIntegrationTests/Program.cs');
const project = read('tests/Phase5AActivityModuleIntegrationTests/Phase5AActivityModuleIntegrationTests.csproj');
const workflow = read('.github/workflows/ci.yml');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const authSession = read('backend/StackMeet.Api/wwwroot/js/auth/AuthSession.js');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');

assert.ok(chess.includes('public sealed class ChessActivityModule : IActivityModule'), 'Chess must be a real production IActivityModule');
assert.ok(chess.includes('public const string ModuleCode = "chess";'), 'Chess module code must be stable');
assert.ok(chess.includes('public string DisplayName => "Chess";'), 'Chess display name must be stable');
assert.ok(chess.includes('public string Version => "1";'), 'Chess descriptor version must be stable');
for (const capability of [
  'SupportsTeamEntries',
  'SupportsCategories',
  'SupportsStages',
  'SupportsLiveResults',
  'SupportsCertificates',
  'SupportsOfflinePackage'
]) {
  assert.ok(chess.includes(`${capability}: false`), `Chess must not advertise unsupported ${capability} behavior in Phase 5A`);
}
assert.ok(!chess.includes('StackMeet.Api.Models'), 'Phase 5A Chess descriptor must not depend on competition domain models');
assert.ok(!chess.includes('StackMeetDbContext'), 'Phase 5A Chess descriptor must not depend on storage');

const moduleRegistrations = registration.match(/AddSingleton<IActivityModule,\s*[^>]+>\(\)/g) || [];
assert.strictEqual(moduleRegistrations.length, 2, 'Phase 5A production DI must register exactly two activity modules');
assert.ok(registration.includes('AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'Sport Stacking production registration must remain');
assert.ok(registration.includes('AddSingleton<IActivityModule, ChessActivityModule>()'), 'Chess must be registered through the existing activity seam');
assert.ok(registry.includes('public const string CompatibilityDefaultCode = SportStackingActivityModule.ModuleCode;'), 'Sport Stacking must remain the compatibility default');
assert.ok(sport.includes('public const string ModuleCode = "sport-stacking";'), 'Sport Stacking module code must remain unchanged');

assert.ok(project.includes('../../backend/StackMeet.Api/StackMeet.Api.csproj'), 'Phase 5A runtime test must reference the real API project');
assert.ok(runtime.includes('services.AddNadiTrackActivityModules();'), 'Phase 5A runtime test must exercise production DI registration');
assert.ok(runtime.includes('GetRequiredService<ActivityModuleRegistry>()'), 'Phase 5A runtime test must exercise the real registry');
assert.ok(runtime.includes('GetRequiredService<CompetitionActivityResolver>()'), 'Phase 5A runtime test must exercise the real resolver');
assert.ok(runtime.includes('GetRequiredService<ActivityAssignmentPolicy>()'), 'Phase 5A runtime test must exercise the real assignment policy');
assert.ok(runtime.includes('exactly Chess and Sport Stacking'), 'Phase 5A runtime test must pin the production module set');
assert.ok(runtime.includes('existing competition without selector still resolves Sport Stacking'), 'compatibility resolution must be runtime-proven');
assert.ok(runtime.includes('persisted Chess selector resolves Chess'), 'persisted Chess resolution must be runtime-proven');
assert.ok(runtime.includes('durable activity data still blocks Sport Stacking to Chess switch'), 'Phase 4B safety policy must remain enforced for real Chess');

assert.ok(workflow.includes('dotnet restore tests/Phase5AActivityModuleIntegrationTests/Phase5AActivityModuleIntegrationTests.csproj'), 'required CI must restore the Phase 5A runtime test');
assert.ok(workflow.includes('Run Phase 5A production module integration tests'), 'required CI must execute the Phase 5A runtime test');
assert.ok(workflow.includes('dotnet run --project tests/Phase5AActivityModuleIntegrationTests/Phase5AActivityModuleIntegrationTests.csproj -c Release --no-restore'), 'Phase 5A runtime command must remain explicit');

for (const sharedRuntime of [app, authSession]) {
  assert.ok(!sharedRuntime.includes('ChessActivityModule'), 'shared frontend runtime must not depend on the Chess implementation type');
  assert.ok(!sharedRuntime.includes('"chess"'), 'shared frontend runtime must not hard-code the Chess module code in Phase 5A');
}
assert.ok(!resultsController.includes('ChessActivityModule'), 'SQL-authoritative results must remain outside Chess module routing in Phase 5A');
assert.ok(!stateController.includes('ChessActivityModule'), 'CompetitionState must remain outside Chess module routing in Phase 5A');

console.log('Modular Platform Foundation v1 Phase 5A second production module guards passed.');
