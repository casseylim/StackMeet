const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs';
const profileHtmlPath = 'backend/StackMeet.Api/wwwroot/profile/index.html';
const profileJsPath = 'backend/StackMeet.Api/wwwroot/profile/profile.js';
const integrationProject = 'tests/StackerPersonalRecordAchievementTests/StackerPersonalRecordAchievementTests.csproj';
const integrationProgram = 'tests/StackerPersonalRecordAchievementTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP5A.md';

for (const file of [
  modelsPath,
  servicePath,
  profileHtmlPath,
  profileJsPath,
  integrationProject,
  integrationProgram,
  architecturePath
]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /record SportStackerPersonalRecordAchievement/);
assert.match(models, /decimal FirstRecordedPersonalBest/);
assert.match(models, /decimal CurrentPersonalBest/);
assert.match(models, /decimal TotalImprovement/);
assert.match(models, /int PersonalBestMilestoneCount/);
assert.match(models, /int FinalizedPerformanceCount/);
assert.match(models, /IReadOnlyList<SportStackerPersonalRecordAchievement> PersonalRecords/);
assert.match(models, /IReadOnlyList<SportStackerPersonalBest> PersonalBests/);

const service = read(servicePath);
assert.match(service, /BuildPersonalRecords\(careerProgression\)/);
assert.match(service, /Where\(point => point\.IsNewPersonalBest\)/);
assert.match(service, /first\.PersonalBestAfter - current\.PersonalBestAfter/);
assert.match(service, /progression\.Points\.Count/);
assert.match(service, /BuildCareerProgression\(tournamentBestCandidates\)/);
assert.match(service, /result\.ParticipantType == "Individual"/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-5A public career service must remain read-only');

const html = read(profileHtmlPath);
assert.match(html, /Personal record achievements/i);
assert.match(html, />Personal Records</);
assert.match(html, /meta name="robots" content="noindex,nofollow"/);
assert.match(html, /server from the same finalized tournament-best progression/i);

const js = read(profileJsPath);
new Function(js);
assert.match(js, /function renderPersonalBests\(personalBests, personalRecords\)/);
assert.match(js, /renderPersonalBests\(profile\.personalBests, profile\.personalRecords\)/);
assert.match(js, /record\.firstRecordedPersonalBest/);
assert.match(js, /record\.totalImprovement/);
assert.match(js, /record\.personalBestMilestoneCount/);
assert.match(js, /record\.finalizedPerformanceCount/);
assert.match(js, /record\.firstPersonalBestCompetitionName/);
assert.match(js, /textContent/);
assert.match(js, /createElement/);
assert.match(js, /credentials: 'omit'/);
assert.match(js, /cache: 'no-store'/);
assert.ok(!/innerHTML/.test(js), 'SP-5A renderer must never inject HTML');
assert.ok(!/Authorization/i.test(js), 'SP-5A public profile fetch must not send authentication credentials');

// Presentation may format the server values but must not recreate PB or improvement logic.
assert.ok(!/record\.firstRecordedPersonalBest\s*-\s*record\.currentPersonalBest/.test(js),
  'SP-5A browser must not calculate total improvement');
assert.ok(!/record\.currentPersonalBest\s*[<>]/.test(js),
  'SP-5A browser must not compare times to decide PB state');
assert.ok(!/personalBestMilestoneCount\s*=/.test(js),
  'SP-5A browser must not calculate milestone counts');

for (const forbidden of ['birthDate', 'email', 'phone', 'gender', 'wssaId', 'stackerId', 'competitionId']) {
  assert.ok(!js.includes(forbidden), `SP-5A renderer must not consume private/internal field: ${forbidden}`);
}

const integration = read(integrationProgram);
for (const scenario of [
  'first finalized 3-3-3 performance establishes first recorded PB',
  'current 3-3-3 PB follows strict career progression',
  '3-3-3 total improvement is first PB minus current PB',
  'slower result and later tie do not create fake PB milestones',
  'all finalized public tournament-best 3-3-3 performances remain counted',
  'current PB provenance points to the strict-improvement milestone, not a later tie',
  'personal record provenance may correctly come from Prelims',
  'active/private competitions cannot contaminate public personal records',
  'existing PersonalBests contract remains aligned with new PersonalRecords summary'
]) {
  assert.ok(integration.includes(scenario), `SP-5A integration scenario missing: ${scenario}`);
}

const architecture = read(architecturePath);
assert.match(architecture, /browser formats and presents server-projected values only/i);
assert.match(architecture, /does \*\*not\*\*:\n\n- generate certificates/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /production `web\.config` \*\*must never be overwritten\*\*/i);

console.log('SP-5A personal record achievement guards passed.');
