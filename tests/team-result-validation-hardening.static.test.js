'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const service = read('backend/StackMeet.Api/Services/CompetitionTeamResultIntegrityService.cs');
const results = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const state = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const integration = read('tests/CoreIntegrityIntegrationTests/Program.cs');

assert.match(program, /AddScoped<CompetitionTeamResultIntegrityService>\(\)/);

assert.match(results, /CompetitionTeamResultIntegrityService teamResults/);
assert.match(results, /WITH \(UPDLOCK, ROWLOCK\)/);
assert.match(results, /request\.Upserts\.Any\(item => item\.Type is "Doubles" or "Timed Relay"\)/);
assert.match(results, /ValidateResultUpserts\(stateJson, request\.Upserts\)/);
assert.doesNotMatch(results, /ValidateResultUpserts\(stateJson, request\.Deletes/);

assert.match(state, /CompetitionTeamResultIntegrityService teamResults/);
assert.match(state, /TryReadReadyTeams\(jsonData/);
assert.match(state, /WITH \(UPDLOCK, HOLDLOCK\).*\[Competition\]/s);
assert.match(state, /ValidateStateAgainstExistingResultsAsync/);
assert.ok(
  state.indexOf('WITH (UPDLOCK, HOLDLOCK)') < state.indexOf('ValidateStateAgainstExistingResultsAsync'),
  'competition row lock must be acquired before checking existing result references'
);

for (const alias of ['stackerTwoId', 'parentStackerId', 'parentName', 'partnerName']) {
  assert.ok(service.includes('"' + alias + '"'), 'missing Doubles legacy compatibility alias: ' + alias);
}
for (const slot of ['one', 'two', 'three', 'four', 'five', 'six']) {
  assert.ok(service.includes('"' + slot + '"'), 'missing Relay legacy member slot: ' + slot);
}
assert.match(service, /RelayMemberCount\(team\) < 4/);
assert.match(service, /pending.*return false/s);
assert.match(service, /Competition Doubles team IDs must be unique/);
assert.match(service, /Competition Relay team IDs must be unique/);
assert.match(service, /StringComparer\.OrdinalIgnoreCase/);
assert.match(service, /Doubles result participant must reference a complete Doubles team/);
assert.match(service, /Timed Relay result participant must reference a ready relay team/);
assert.match(service, /cannot remove or invalidate a Doubles team while SQL results reference it/);
assert.match(service, /cannot remove or invalidate a Timed Relay team while SQL results reference it/);
assert.match(service, /Team result members must belong to this competition's registered Stackers/);
assert.match(service, /cannot change Doubles team members while SQL results reference it/);
assert.match(service, /cannot change Timed Relay team members while SQL results reference it/);
assert.match(service, /database\.Stackers/);
assert.match(service, /item\.CompetitionId == competitionId/);
assert.match(service, /DoublesMembers/);
assert.match(service, /RelaysMembers/);

assert.match(app, /async function deleteTeamSqlResults\(type, id\)/);
assert.match(app, /await saveSqlResults\(\[\], deletes\)/);
assert.match(app, /shouldSave = await deleteDouble\(target\.dataset\.id\)/);
assert.match(app, /shouldSave = await deleteRelay\(target\.dataset\.id\)/);
assert.ok(
  app.indexOf('await deleteTeamSqlResults("Doubles", id)') < app.indexOf('state.doubles = state.doubles.filter'),
  'Doubles SQL results must be removed before team state'
);
assert.ok(
  app.indexOf('await deleteTeamSqlResults("Timed Relay", id)') < app.indexOf('state.relays = state.relays.filter'),
  'Relay SQL results must be removed before team state'
);

for (const scenario of [
  'complete doubles detected including legacy aliases',
  'ready relays detected including legacy member slots',
  'pending doubles result team rejected',
  'incomplete relay result team rejected',
  'duplicate team IDs fail closed',
  'malformed team state fails closed',
  'HTTP state cannot orphan existing doubles result',
  'HTTP rejected team removal leaves state unchanged',
  'team result resolves to registered competition stackers',
  'team result rejects member outside competition stackers',
  'child parent result links registered child while external parent remains external',
  'team metadata may change while result membership stays fixed',
  'team members cannot change while SQL results reference team'
]) {
  assert.ok(integration.includes(scenario), 'missing integration scenario: ' + scenario);
}

console.log('Team-result validation hardening static guards passed.');
