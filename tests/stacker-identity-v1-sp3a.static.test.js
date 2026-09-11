const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs';
const testProject = 'tests/StackerCareerProfileTests/StackerCareerProfileTests.csproj';
const testProgram = 'tests/StackerCareerProfileTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP3A.md';

for (const file of [modelsPath, servicePath, testProject, testProgram, architecturePath]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /record PublicSportStackerCareerProfile/);
assert.match(models, /record SportStackerPersonalBest/);
assert.match(models, /OfficialTime/);
assert.match(models, /AppliedPenalty/);
assert.match(models, /CompetitionKey/);

const service = read(servicePath);
assert.match(service, /item\.NadiTrackId == normalizedId && item\.IsPublicProfile/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.match(service, /competition\.Status == "Closed"/);
assert.match(service, /competition\.Status == "Archived"/);
assert.match(service, /result\.ParticipantType == "Individual"/);
assert.match(service, /value > 0m && value < 999m/);
assert.match(service, /row\.Penalty > 0m && row\.Penalty < 999m/);
assert.match(service, /AsNoTracking\(\)/);

// Read-only means no EF persistence operation. In-memory List<T>.Add is allowed.
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-3A career service must never call SaveChanges');
assert.ok(!/database\.[A-Za-z0-9_]+\.(?:Add|AddRange|Remove|RemoveRange|Update|UpdateRange)\s*\(/.test(service), 'SP-3A career service must not mutate EF DbSets');
assert.ok(!/database\.(?:Add|AddRange|Remove|RemoveRange|Update|UpdateRange)\s*\(/.test(service), 'SP-3A career service must not mutate DbContext');
assert.ok(!/ExecuteSql(?:Raw|Interpolated)(?:Async)?\s*\(/.test(service), 'SP-3A career service must not execute SQL writes');

const program = read(testProgram);
for (const scenario of [
  'only finalized publicly-listed competitions count as career appearances',
  'personal best uses best valid attempt plus applicable penalty',
  'scratch and malformed Cycle rows do not displace a valid PB',
  'faster active/private competition results cannot become public career PBs',
  'private profile is indistinguishable from not found'
]) {
  assert.ok(program.includes(scenario), `SP-3A integration scenario missing: ${scenario}`);
}

// The privacy assertions are intentionally generated from one forbidden-field loop.
for (const forbidden of ['BirthDate', 'Email', 'Phone', 'Gender', 'WssaId']) {
  assert.ok(program.includes(`"${forbidden}"`), `SP-3A forbidden public field guard missing: ${forbidden}`);
}
assert.match(program, /publicProperties\.Contains\(forbidden\)/);
assert.match(program, /public career contract excludes \{forbidden\}/);

const architecture = read(architecturePath);
assert.match(architecture, /read-only/i);
assert.match(architecture, /no public HTTP endpoint/i);
assert.match(architecture, /no frontend route/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /Closed/);
assert.match(architecture, /Archived/);

const controllersDir = path.join(root, 'backend/StackMeet.Api/Controllers');
for (const file of fs.readdirSync(controllersDir).filter(name => name.endsWith('.cs'))) {
  const controller = fs.readFileSync(path.join(controllersDir, file), 'utf8');
  assert.ok(!controller.includes('SportStackerCareerProfileService'), `SP-3A must not expose career profile HTTP API yet: ${file}`);
}

console.log('SP-3A public career profile guards passed.');
