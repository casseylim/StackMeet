const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const contracts = read('backend/StackMeet.Api/Activities/ActivityModuleContracts.cs');
const registry = read('backend/StackMeet.Api/Activities/ActivityModuleRegistry.cs');
const sportStacking = read('backend/StackMeet.Api/Activities/SportStackingActivityModule.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');

assert.ok(contracts.includes('public interface IActivityModule'), 'activity module contract must exist');
assert.ok(contracts.includes('ActivityModuleCapabilities'), 'activity module capability contract must exist');
assert.ok(contracts.includes('SupportsTeamEntries'), 'team-entry capability must be activity-neutral metadata');
assert.ok(contracts.includes('SupportsCategories'), 'category capability must be activity-neutral metadata');
assert.ok(contracts.includes('SupportsStages'), 'stage capability must be activity-neutral metadata');
assert.ok(contracts.includes('SupportsLiveResults'), 'live-results capability must be activity-neutral metadata');

assert.ok(sportStacking.includes('public const string ModuleCode = "sport-stacking";'), 'Sport Stacking must have a stable module code');
assert.ok(sportStacking.includes('public string DisplayName => "Sport Stacking";'), 'Sport Stacking display metadata must exist');
assert.ok(registry.includes('CompatibilityDefaultCode = SportStackingActivityModule.ModuleCode'), 'legacy competitions must resolve through the Sport Stacking compatibility default');
assert.ok(registry.includes('StringComparer.OrdinalIgnoreCase'), 'module codes must resolve case-insensitively');
assert.ok(registry.includes('Duplicate activity module code'), 'duplicate module codes must fail closed');
assert.ok(registry.includes('Unknown activity module'), 'unknown explicit module codes must fail closed');
assert.ok(registry.includes('string.IsNullOrWhiteSpace(moduleCode)'), 'blank module code must use compatibility resolution');

assert.ok(registration.includes('AddNadiTrackActivityModules'), 'DI registration seam must exist');
assert.ok(registration.includes('AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'Sport Stacking must be the only built-in module in the foundation seam');
assert.ok(registration.includes('AddSingleton<ActivityModuleRegistry>()'), 'activity module registry must be DI-registerable');

const sharedCoreSurface = `${contracts}\n${registry}\n${registration}`;
for (const forbidden of [
  '3-3-3',
  '3-6-3',
  'Cycle',
  'WssaId',
  'SpecialStacker',
  'Doubles',
  'Timed Relay',
  'Child/Parent',
  'Prelims',
  'Finals'
]) {
  assert.ok(!sharedCoreSurface.includes(forbidden), `Shared Core module contracts must not contain Sport Stacking rule token: ${forbidden}`);
}

for (const forbidden of ['DbContext', 'SqlServer', 'CompetitionResultRules', 'FinalsReportEngine']) {
  assert.ok(!sharedCoreSurface.includes(forbidden), `module registry seam must not depend on runtime/domain implementation: ${forbidden}`);
}

assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'foundation must not require a Competition schema change');

console.log('Modular Platform Foundation v1 Phase 2 static guards passed.');
