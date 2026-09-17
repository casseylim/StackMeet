'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const controller = read('backend/StackMeet.Api/Controllers/PublicResultsProfileLinksController.cs');
const browser = read('backend/StackMeet.Api/wwwroot/results/profile-links.js');
const html = read('backend/StackMeet.Api/wwwroot/results/index.html');

assert.match(controller, /Route\("api\/public\/competitions\/\{competitionId\}\/profile-links"\)/,
  'SP-4O must expose a competition-scoped public profile-link bridge.');
assert.match(controller, /database\.StackerIdentityLinks\.AsNoTracking\(\)/,
  'SP-4O must start from durable reviewed identity links.');
assert.match(controller, /link\.Stacker\.CompetitionId == competition\.Id/,
  'SP-4O links must be restricted to the requested competition.');
assert.match(controller, /link\.SportStackerIdentity\.IsPublicProfile/,
  'private permanent identities must never receive a public result-page profile link.');
assert.match(controller, /NadiTrackIdRules\.IsValid\(item\.NadiTrackId\)/,
  'malformed permanent IDs must fail closed.');
assert.match(controller, /profileUrl = \$"\/Stackers\/\{NadiTrackIdRules\.Normalize\(item\.NadiTrackId\)\}"/,
  'the server must own the permanent career-profile URL.');

for (const forbidden of ['FirstName', 'LastName', 'BirthDate', 'Email', 'Phone', 'Gender', 'WssaId', 'CustomDivision']) {
  assert.doesNotMatch(controller, new RegExp(`\\b${forbidden}\\b`),
    `SP-4O public link bridge must not publish or match on ${forbidden}.`);
}

assert.match(browser, /approvedProfilePath = \/\^\\\/Stackers\\\/NDT-/,
  'browser must accept only the NADITrack career-profile path shape.');
assert.match(browser, /document\.querySelectorAll\("\.stacker-cell"\)/,
  'SP-4O should decorate already-rendered individual result names without rewriting the results engine.');
assert.match(browser, /credentials:\s*"omit"/,
  'public profile-link lookup must remain credential-free.');
assert.match(browser, /cache:\s*"no-store"/,
  'public profile-link lookup must not be cached as identity visibility can change.');
assert.doesNotMatch(browser, /innerHTML\s*=/,
  'SP-4O browser rendering must remain DOM/textContent based.');
assert.match(browser, /existingLink\.replaceWith\(plainNameNode\(name\)\)/,
  'if a profile is no longer public, the browser must revert the name to plain text.');
assert.match(browser, /catch \{[\s\S]*profileLinks = new Map\(\)/,
  'link-service failures must fail closed without breaking public results.');

assert.match(html, /<script src="\/results\/profile-links\.js\?v=sp4o-20260917a"><\/script>/,
  'public Results shell must activate SP-4O after the existing results engine.');
assert.ok(
  html.indexOf('/results/results.js?v=multilanguage-v2-phase3c') < html.indexOf('/results/profile-links.js?v=sp4o-20260917a'),
  'SP-4O companion script must load after the existing results renderer.');

console.log('SP-4O public Results career-profile linking static guards passed.');
