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
const integrationProject = 'tests/StackerFinalsCareerTests/StackerFinalsCareerTests.csproj';
const integrationProgram = 'tests/StackerFinalsCareerTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP4D.md';
const releaseSafetyPath = 'docs/architecture/STACKER_IDENTITY_SP4B_RELEASE_NOTE.md';
const ciPath = '.github/workflows/ci.yml';

for (const file of [
  modelsPath,
  servicePath,
  profileHtmlPath,
  profileCssPath,
  profileJsPath,
  integrationProject,
  integrationProgram,
  architecturePath,
  releaseSafetyPath,
  ciPath
]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /record SportStackerFinalsHistoryPoint/);
assert.match(models, /string Status/);
assert.match(models, /decimal\? OfficialTime/);
assert.match(models, /decimal\? RawBestTime/);
assert.match(models, /record SportStackerEventFinalsSummary/);
assert.match(models, /int FinalsAppearanceCount/);
assert.match(models, /int ValidFinalsCount/);
assert.match(models, /decimal\? BestFinalOfficialTime/);
assert.match(models, /IReadOnlyList<SportStackerEventFinalsSummary> FinalsCareer/);

const finalsContractStart = models.indexOf('record SportStackerFinalsHistoryPoint');
const finalsContractEnd = models.indexOf('record PublicSportStackerCareerProfile');
const finalsContracts = models.slice(finalsContractStart, finalsContractEnd);
for (const forbidden of ['BirthDate', 'Email', 'Phone', 'Gender', 'WssaId', 'StackerId', 'CompetitionId', ' Rank,', ' Placement,', ' Medal,', ' Award,']) {
  assert.ok(!finalsContracts.includes(forbidden), `SP-4D public Finals contract must exclude ${forbidden.trim()}`);
}

const service = read(servicePath);
assert.match(service, /BuildFinalsCareer\(resultRows\)/);
assert.match(service, /stage != "Finals"/);
assert.match(service, /"Valid"/);
assert.match(service, /"Scratch"/);
assert.match(service, /"Missing"/);
assert.match(service, /"Invalid"/);
assert.match(service, /rawBestTime \+ appliedPenalty/);
assert.match(service, /result\.ParticipantType == "Individual"/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.match(service, /AsNoTracking\(\)/);
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-4D public career service must remain read-only');
assert.ok(!/ExecuteSql(?:Raw|Interpolated)(?:Async)?\s*\(/.test(service), 'SP-4D public career service must not execute SQL writes');

const html = read(profileHtmlPath);
assert.match(html, /id="finalsCareerTitle"/);
assert.match(html, /id="finalsCareer"/);
assert.match(html, /id="noFinalsCareer"/);
assert.match(html, /Finals Career/);
assert.match(html, /Placement, medals, awards and records are not inferred/i);
assert.match(html, /meta name="robots" content="noindex,nofollow"/);

const js = read(profileJsPath);
new Function(js);
assert.match(js, /function renderFinalsCareer\(finalsCareer\)/);
assert.match(js, /renderFinalsCareer\(profile\.finalsCareer\)/);
assert.match(js, /summary\.finalsAppearanceCount/);
assert.match(js, /summary\.validFinalsCount/);
assert.match(js, /summary\.bestFinalOfficialTime/);
assert.match(js, /point\.status/);
assert.match(js, /point\.officialTime/);
assert.match(js, /point\.rawBestTime/);
assert.match(js, /point\.appliedPenalty/);
assert.match(js, /textContent/);
assert.match(js, /createElement/);
assert.match(js, /credentials: 'omit'/);
assert.match(js, /cache: 'no-store'/);
assert.ok(!/innerHTML/.test(js), 'SP-4D Finals renderer must never inject HTML');
assert.ok(!/Authorization/i.test(js), 'SP-4D public profile fetch must not send authentication credentials');
assert.ok(!/\.rank\b/.test(js), 'SP-4D browser must not calculate or consume ranking');
assert.ok(!/\.placement\b/i.test(js), 'SP-4D browser must not calculate or consume placement');
assert.ok(!/\.medal\b/i.test(js), 'SP-4D browser must not calculate or consume medals');
for (const forbidden of ['birthDate', 'email', 'phone', 'gender', 'wssaId', 'stackerId', 'competitionId']) {
  assert.ok(!js.includes(forbidden), `SP-4D renderer must not consume private/internal field: ${forbidden}`);
}

const css = read(profileCssPath);
for (const className of [
  '.finals-grid',
  '.finals-card',
  '.finals-facts',
  '.finals-history-row',
  '.finals-time',
  '.finals-status'
]) {
  assert.ok(css.includes(className), `SP-4D presentation style missing: ${className}`);
}

const integration = read(integrationProgram);
for (const scenario of [
  'Finals career follows canonical event order',
  '3-3-3 counts finalized public Finals rows and valid finishes separately',
  'best Finals performance uses official time including an applicable penalty',
  'Finals history retains valid, missing and invalid statuses chronologically',
  'Prelims-only 3-6-3 does not enter Finals career',
  'Cycle Finals career excludes Doubles while retaining Individual scratch and invalid rows',
  'Cycle Finals history classifies scratch and malformed JSON without fabricating times',
  'active and non-public competitions cannot enter Finals career',
  'SP-3A personal best semantics remain unchanged and may still come from Prelims'
]) {
  assert.ok(integration.includes(scenario), `SP-4D integration scenario missing: ${scenario}`);
}

const architecture = read(architecturePath);
assert.match(architecture, /does \*\*not\*\* publish placement, podium, medals, awards, records, rankings/i);
assert.match(architecture, /browser renders the server projection only/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /production `web\.config` must never be overwritten/i);

const releaseSafety = read(releaseSafetyPath);
assert.match(releaseSafety, /exclude `web\.config` from the deployment payload/i);
assert.match(releaseSafety, /verify the production `web\.config` hash remains unchanged after deployment/i);

const ci = read(ciPath);
assert.ok(ci.includes('dotnet restore tests/StackerFinalsCareerTests/StackerFinalsCareerTests.csproj'), 'SP-4D test project must be restored in CI');
assert.ok(ci.includes('dotnet run --project tests/StackerFinalsCareerTests/StackerFinalsCareerTests.csproj'), 'SP-4D test project must run in CI');

console.log('SP-4D Finals career guards passed.');
