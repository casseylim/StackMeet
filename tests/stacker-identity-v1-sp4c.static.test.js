const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs';
const profileHtmlPath = 'backend/StackMeet.Api/wwwroot/profile/index.html';
const profileCssPath = 'backend/StackMeet.Api/wwwroot/profile/profile.css';
const profileJsPath = 'backend/StackMeet.Api/wwwroot/profile/profile.js';
const integrationProject = 'tests/StackerCareerProgressTests/StackerCareerProgressTests.csproj';
const integrationProgram = 'tests/StackerCareerProgressTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP4C.md';
const releaseSafetyPath = 'docs/architecture/STACKER_IDENTITY_SP4B_RELEASE_NOTE.md';

for (const file of [
  modelsPath,
  servicePath,
  profileHtmlPath,
  profileCssPath,
  profileJsPath,
  integrationProject,
  integrationProgram,
  architecturePath,
  releaseSafetyPath
]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /record SportStackerCareerProgressPoint/);
assert.match(models, /bool IsNewPersonalBest/);
assert.match(models, /decimal PersonalBestAfter/);
assert.match(models, /decimal\? ImprovementFromPreviousBest/);
assert.match(models, /record SportStackerEventProgression/);
assert.match(models, /IReadOnlyList<SportStackerEventProgression> CareerProgression/);

const service = read(servicePath);
assert.match(service, /BuildCareerProgression\(tournamentBestCandidates\)/);
assert.match(service, /previousBest is null \|\| item\.OfficialTime < previousBest\.Value/);
assert.match(service, /previousBest\.Value - item\.OfficialTime/);
assert.match(service, /GroupBy\(item => new \{ item\.CompetitionId, item\.EventCode \}\)/);
assert.match(service, /result\.ParticipantType == "Individual"/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.match(service, /AsNoTracking\(\)/);
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-4C public career service must remain read-only');

const html = read(profileHtmlPath);
assert.match(html, /id="careerProgressTitle"/);
assert.match(html, /id="careerProgression"/);
assert.match(html, /id="noCareerProgression"/);
assert.match(html, /Career Progress/);
assert.match(html, /meta name="robots" content="noindex,nofollow"/);

const js = read(profileJsPath);
new Function(js);
assert.match(js, /function renderCareerProgression\(careerProgression\)/);
assert.match(js, /renderCareerProgression\(profile\.careerProgression\)/);
assert.match(js, /point\.officialTime/);
assert.match(js, /point\.personalBestAfter/);
assert.match(js, /point\.improvementFromPreviousBest/);
assert.match(js, /point\.isNewPersonalBest/);
assert.match(js, /point\.stage/);
assert.match(js, /New PB/);
assert.match(js, /PB held/);
assert.match(js, /textContent/);
assert.match(js, /createElement/);
assert.match(js, /credentials: 'omit'/);
assert.match(js, /cache: 'no-store'/);
assert.ok(!/innerHTML/.test(js), 'SP-4C progression renderer must never inject HTML');
assert.ok(!/Authorization/i.test(js), 'SP-4C public profile fetch must not send authentication credentials');
for (const forbidden of ['birthDate', 'email', 'phone', 'gender', 'wssaId', 'stackerId', 'competitionId']) {
  assert.ok(!js.includes(forbidden), `SP-4C renderer must not consume private/internal field: ${forbidden}`);
}

// The browser must render the reviewed server projection rather than recreating PB logic.
assert.ok(!/officialTime\s*</.test(js), 'SP-4C browser must not compare official times to decide PB status');
assert.ok(!/personalBestAfter\s*=/.test(js), 'SP-4C browser must not calculate the running PB');

const css = read(profileCssPath);
for (const className of [
  '.progress-grid',
  '.progress-card',
  '.progress-point',
  '.progress-marker',
  '.progress-time',
  '.progress-badge'
]) {
  assert.ok(css.includes(className), `SP-4C presentation style missing: ${className}`);
}

const integration = read(integrationProgram);
for (const scenario of [
  'career progression event order is stable',
  'career progression is chronological',
  'first valid finalized performance establishes the baseline PB',
  'slower tournament remains visible without changing the running PB',
  'strictly faster finalized time records the exact improvement from previous PB',
  'a tie does not create a second PB milestone',
  'best performance within a tournament retains stage provenance',
  'SP-3A career PB remains aligned with SP-4C progression',
  'SP-4A tournament history remains restricted to finalized publicly-listed appearances'
]) {
  assert.ok(integration.includes(scenario), `SP-4C integration scenario missing: ${scenario}`);
}

const architecture = read(architecturePath);
assert.match(architecture, /browser renders the server-projected values only/i);
assert.match(architecture, /strictly lower/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /production `web\.config` must not be overwritten/i);

const releaseSafety = read(releaseSafetyPath);
assert.match(releaseSafety, /exclude `web\.config` from the deployment payload/i);
assert.match(releaseSafety, /verify the production `web\.config` hash remains unchanged after deployment/i);

console.log('SP-4C career progression guards passed.');
