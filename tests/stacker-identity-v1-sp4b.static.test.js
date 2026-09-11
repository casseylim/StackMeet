const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const profileHtmlPath = 'backend/StackMeet.Api/wwwroot/profile/index.html';
const profileCssPath = 'backend/StackMeet.Api/wwwroot/profile/profile.css';
const profileJsPath = 'backend/StackMeet.Api/wwwroot/profile/profile.js';
const controllerPath = 'backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs';
const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs';
const architecturePath = 'docs/architecture/STACKER_IDENTITY_SP4B.md';

for (const file of [
  profileHtmlPath,
  profileCssPath,
  profileJsPath,
  controllerPath,
  modelsPath,
  architecturePath
]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const html = read(profileHtmlPath);
assert.match(html, /meta name="robots" content="noindex,nofollow"/);
assert.match(html, /id="tournamentHistoryTitle"/);
assert.match(html, /id="tournamentHistory"/);
assert.match(html, /id="noTournamentHistory"/);
assert.match(html, /Tournament History/);
assert.match(html, /Finalized competitions only/);

const js = read(profileJsPath);
new Function(js);
assert.match(js, /function renderTournamentHistory\(tournamentHistory\)/);
assert.match(js, /renderTournamentHistory\(profile\.tournamentHistory\)/);
assert.match(js, /Array\.isArray\(tournamentHistory\)/);
assert.match(js, /Array\.isArray\(tournament\.performances\)/);
assert.match(js, /Appearance recorded · no valid individual times available\./);
assert.match(js, /performance\.eventCode/);
assert.match(js, /performance\.officialTime/);
assert.match(js, /performance\.rawBestTime/);
assert.match(js, /performance\.appliedPenalty/);
assert.match(js, /performance\.stage/);
assert.match(js, /textContent/);
assert.match(js, /createElement/);
assert.match(js, /replaceChildren\(\)/);
assert.ok(!/innerHTML/.test(js), 'SP-4B public tournament renderer must never inject HTML');

assert.match(js, /\/api\/public\/stackers\/\$\{encodeURIComponent\(nadiTrackId\)\}/);
assert.match(js, /credentials: 'omit'/);
assert.match(js, /cache: 'no-store'/);
assert.ok(!/Authorization/i.test(js), 'SP-4B public profile fetch must not send authentication credentials');
for (const forbidden of ['birthDate', 'email', 'phone', 'gender', 'wssaId']) {
  assert.ok(!js.includes(forbidden), `SP-4B renderer must not consume private field: ${forbidden}`);
}

const css = read(profileCssPath);
for (const className of [
  '.tournament-timeline',
  '.tournament-card',
  '.tournament-performance-grid',
  '.tournament-performance',
  '.tournament-time'
]) {
  assert.ok(css.includes(className), `SP-4B presentation style missing: ${className}`);
}

const models = read(modelsPath);
assert.match(models, /record SportStackerTournamentHistory/);
assert.match(models, /IReadOnlyList<SportStackerTournamentHistory> TournamentHistory/);

const controller = read(controllerPath);
assert.match(controller, /Route\("api\/public\/stackers\/\{nadiTrackId\}"\)/);
assert.match(controller, /Route\("Stackers\/\{nadiTrackId\}"\)/);
assert.match(controller, /noindex, nofollow/);

const architecture = read(architecturePath);
assert.match(architecture, /presentation-only/i);
assert.match(architecture, /credentials: 'omit'/);
assert.match(architecture, /innerHTML.*forbidden/i);
assert.match(architecture, /production `web\.config`.*must \*\*not\*\* be overwritten/i);
assert.match(architecture, /no production deployment action/i);

console.log('SP-4B tournament history presentation guards passed.');
