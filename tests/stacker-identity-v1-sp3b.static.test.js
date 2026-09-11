const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const controllerPath = 'backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs';
const registrationPath = 'backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileService.cs';
const programPath = 'backend/StackMeet.Api/Program.cs';
const profileHtmlPath = 'backend/StackMeet.Api/wwwroot/profile/index.html';
const profileCssPath = 'backend/StackMeet.Api/wwwroot/profile/profile.css';
const profileJsPath = 'backend/StackMeet.Api/wwwroot/profile/profile.js';
const integrationProject = 'tests/StackerPublicProfileTests/StackerPublicProfileTests.csproj';
const integrationProgram = 'tests/StackerPublicProfileTests/Program.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP3B.md';

for (const file of [
  controllerPath,
  registrationPath,
  servicePath,
  programPath,
  profileHtmlPath,
  profileCssPath,
  profileJsPath,
  integrationProject,
  integrationProgram,
  architecturePath
]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const controller = read(controllerPath);
assert.match(controller, /Route\("api\/public\/stackers\/\{nadiTrackId\}"\)/);
assert.match(controller, /ResponseCache\(NoStore = true, Location = ResponseCacheLocation\.None\)/);
assert.match(controller, /profiles\.GetPublicAsync\(nadiTrackId, ct\)/);
assert.match(controller, /profile is null \? NotFound\(\) : Ok\(profile\)/);
assert.match(controller, /Route\("Stackers\/\{nadiTrackId\}"\)/);
assert.match(controller, /NadiTrackIdRules\.IsValid\(nadiTrackId\)/);
assert.match(controller, /X-Robots-Tag/);
assert.match(controller, /noindex, nofollow/);
assert.match(controller, /profile", "index\.html/);

const registration = read(registrationPath);
assert.match(registration, /AddScoped<SportStackerCareerProfileService>\(\)/);

const service = read(servicePath);
assert.match(service, /item\.NadiTrackId == normalizedId && item\.IsPublicProfile/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.ok(!/SaveChanges(?:Async)?\s*\(/.test(service), 'SP-3B must reuse the read-only SP-3A projection');

const program = read(programPath);
assert.match(program, /!path\.StartsWithSegments\("\/api\/public"\)/);

const html = read(profileHtmlPath);
assert.match(html, /meta name="robots" content="noindex,nofollow"/);
assert.match(html, /id="displayName"/);
assert.match(html, /id="personalBests"/);
assert.match(html, /Finalized results only/);

const js = read(profileJsPath);
assert.match(js, /\/api\/public\/stackers\/\$\{encodeURIComponent\(nadiTrackId\)\}/);
assert.match(js, /credentials: 'omit'/);
assert.match(js, /cache: 'no-store'/);
assert.match(js, /textContent/);
assert.match(js, /replaceChildren\(\)/);
assert.ok(!/innerHTML/.test(js), 'SP-3B public profile renderer must not inject HTML');
assert.ok(!/Authorization/i.test(js), 'SP-3B public profile fetch must not send authentication credentials');
for (const forbidden of ['birthDate', 'email', 'phone', 'gender', 'wssaId']) {
  assert.ok(!js.includes(forbidden), `SP-3B renderer must not consume private field: ${forbidden}`);
}

const integration = read(integrationProgram);
for (const scenario of [
  'public endpoint normalizes permanent NADITrack ID',
  'public endpoint returns reviewed display name',
  'public endpoint returns finalized public career count',
  'public endpoint returns finalized personal best projection',
  'private profile returns the same not-found boundary',
  'unknown valid profile returns not found',
  'malformed profile ID returns not found',
  'public profile API stays under the existing unauthenticated public boundary',
  'public profile API is explicitly non-cacheable',
  'permanent public profile page route is stable'
]) {
  assert.ok(integration.includes(scenario), `SP-3B integration scenario missing: ${scenario}`);
}

const architecture = read(architecturePath);
assert.match(architecture, /`GET \/api\/public\/stackers\/\{NadiTrackId\}`/);
assert.match(architecture, /`\/Stackers\/\{NadiTrackId\}`/);
assert.match(architecture, /private, unknown, and malformed/i);
assert.match(architecture, /noindex/i);
assert.match(architecture, /no production deployment/i);

console.log('SP-3B public profile endpoint and presentation guards passed.');
