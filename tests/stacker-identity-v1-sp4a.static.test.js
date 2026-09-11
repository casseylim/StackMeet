const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs';
const testProject = 'tests/StackerTournamentHistoryTests/StackerTournamentHistoryTests.csproj';
const testProgram = 'tests/StackerTournamentHistoryTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP4A.md';
const ciPath = '.github/workflows/ci.yml';

for (const file of [modelsPath, servicePath, testProject, testProgram, architecturePath, ciPath]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /record SportStackerTournamentHistory/);
assert.match(models, /record SportStackerTournamentPerformance/);
assert.match(models, /IReadOnlyList<SportStackerTournamentHistory> TournamentHistory/);
for (const forbidden of ['BirthDate', 'Email', 'Phone', 'Gender', 'WssaId']) {
  const historyStart = models.indexOf('record SportStackerTournamentPerformance');
  const historyEnd = models.indexOf('record PublicSportStackerCareerProfile');
  const historyContracts = models.slice(historyStart, historyEnd);
  assert.ok(!historyContracts.includes(forbidden), `SP-4A history contract must exclude ${forbidden}`);
}

const service = read(servicePath);
assert.match(service, /competition\.IsPubliclyListed/);
assert.match(service, /competition\.Status == "Closed"/);
assert.match(service, /competition\.Status == "Archived"/);
assert.match(service, /result\.ParticipantType == "Individual"/);
assert.match(service, /Where\(item => item\.CompetitionId == appearance\.CompetitionId\)/);
assert.match(service, /GroupBy\(item => item\.EventCode/);
assert.match(service, /OrderByDescending\(item => item\.StartDate\)/);
assert.match(service, /new SportStackerTournamentHistory/);
assert.match(service, /new SportStackerTournamentPerformance/);
assert.match(service, /AsNoTracking\(\)/);
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-4A career service must remain read-only');
assert.ok(!/database\.[A-Za-z0-9_]+\.(?:Add|AddRange|Remove|RemoveRange|Update|UpdateRange)\s*\(/.test(service), 'SP-4A career service must not mutate EF DbSets');
assert.ok(!/ExecuteSql(?:Raw|Interpolated)(?:Async)?\s*\(/.test(service), 'SP-4A career service must not execute SQL writes');

const program = read(testProgram);
for (const scenario of [
  'SP-4A history uses the same finalized public appearance boundary',
  'a finalized public appearance remains in history even without a valid individual result',
  'SP-4A selects the best valid individual performance per event within a tournament',
  'Doubles results cannot enter individual tournament history',
  'active and non-public competitions cannot leak into SP-4A history',
  'SP-4A does not alter the existing career personal-best semantics'
]) {
  assert.ok(program.includes(scenario), `SP-4A integration scenario missing: ${scenario}`);
}

const architecture = read(architecturePath);
assert.match(architecture, /no database schema or migration/i);
assert.match(architecture, /no write path/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /Active\/provisional competitions and non-public competitions cannot enter tournament history/i);
assert.match(architecture, /existing profile page is not changed/i);

const ci = read(ciPath);
assert.ok(ci.includes('dotnet restore tests/StackerTournamentHistoryTests/StackerTournamentHistoryTests.csproj'), 'SP-4A test project must be restored in CI');
assert.ok(ci.includes('dotnet run --project tests/StackerTournamentHistoryTests/StackerTournamentHistoryTests.csproj'), 'SP-4A test project must run in CI');

console.log('SP-4A tournament history guards passed.');
