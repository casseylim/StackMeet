'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const policy = require('../backend/StackMeet.Api/wwwroot/profile/finals-placement-presentation.js');

const id = 'NDT-2345678';
const basePoint = {
  competitionKey: 'SP4P-TEST',
  competitionName: 'SP-4P Test Competition',
  competitionDate: '2026-09-17',
  eventCode: 'Cycle',
  resultStatus: 'Valid',
  officialBestTime: 6.123,
  placement: 2,
  sharesPlacement: false
};
const publication = {
  publicationVersion: policy.PUBLICATION_VERSION,
  nadiTrackId: id,
  cohortPolicy: policy.COHORT_POLICY,
  history: [basePoint]
};

assert.equal(policy.PUBLICATION_VERSION, 'sp4m-public-finals-placement-v1');
assert.equal(
  policy.COHORT_POLICY,
  'Competition-time division · mixed category · no additional gender filter'
);
assert.equal(policy.validatePublication(publication, id), publication,
  'Reviewed SP-4M publication must be presentation-safe.');
assert.equal(
  policy.placementText(basePoint),
  'Certified placement in competition-time division: #2'
);
assert.equal(
  policy.placementText({ ...basePoint, sharesPlacement: true }),
  'Certified shared placement in competition-time division: #2'
);

for (const unsafe of [
  { ...publication, publicationVersion: 'unknown' },
  { ...publication, nadiTrackId: 'NDT-ABCDEFG' },
  { ...publication, cohortPolicy: 'Other cohort' },
  { ...publication, history: null },
  { ...publication, history: [{ ...basePoint, eventCode: 'All-Around' }] },
  { ...publication, history: [{ ...basePoint, resultStatus: 'Unknown' }] },
  { ...publication, history: [{ ...basePoint, placement: null }] },
  { ...publication, history: [{ ...basePoint, placement: 0 }] },
  { ...publication, history: [{ ...basePoint, officialBestTime: null }] },
  { ...publication, history: [{ ...basePoint, competitionDate: '17/09/2026' }] },
  { ...publication, history: [{ ...basePoint, competitionKey: '' }] }
]) {
  assert.equal(policy.validatePublication(unsafe, id), null,
    'Unreviewed or inconsistent placement payload must fail closed.');
}

for (const status of ['Scratch', 'Missing', 'Invalid']) {
  const nonPlacement = {
    ...basePoint,
    resultStatus: status,
    officialBestTime: null,
    placement: null,
    sharesPlacement: false
  };
  assert.ok(policy.validatePublication({ ...publication, history: [nonPlacement] }, id));
  assert.equal(policy.placementText(nonPlacement), 'No certified placement for this Finals result');

  for (const inconsistent of [
    { ...nonPlacement, placement: 1 },
    { ...nonPlacement, officialBestTime: 7.001 },
    { ...nonPlacement, sharesPlacement: true }
  ]) {
    assert.equal(
      policy.validatePublication({ ...publication, history: [inconsistent] }, id),
      null,
      `${status} must never publish a time, placement or shared-placement claim.`
    );
  }
}

assert.equal(policy.validatePublication(publication, 'NDT-8765432'), null,
  'Embedded placement identity must match the already-validated profile identity.');

const profileJs = read('backend/StackMeet.Api/wwwroot/profile/profile.js');
const profileHtml = read('backend/StackMeet.Api/wwwroot/profile/index.html');
const presentationJs = read('backend/StackMeet.Api/wwwroot/profile/finals-placement-presentation.js');

assert.match(profileJs, /NadiTrackFinalsPlacementPresentation/,
  'Profile renderer must consume the governed placement presentation gate.');
assert.match(profileJs, /validatePublication\(publication, expectedNadiTrackId\)/,
  'Browser must validate the placement publication against the public profile identity before rendering.');
assert.match(profileJs, /renderFinalsPlacements\(profile\.finalsPlacements, profile\.nadiTrackId\)/,
  'Already-validated profile identity must be passed to the placement renderer.');
assert.doesNotMatch(profileJs, /innerHTML/,
  'Public profile rendering must remain DOM/textContent based.');

const policyScript = profileHtml.indexOf('/profile/finals-placement-presentation.js');
const profileScript = profileHtml.indexOf('/profile/profile.js');
assert.ok(policyScript >= 0 && profileScript > policyScript,
  'Presentation policy must load before the public profile renderer.');
assert.match(profileHtml, /<meta name="robots" content="noindex,nofollow">/,
  'Existing noindex/nofollow release boundary must remain intact.');

for (const forbidden of ['Medal', 'Award', 'Podium', 'Record-holder', 'Global ranking', 'National ranking']) {
  assert.doesNotMatch(presentationJs, new RegExp(forbidden, 'i'),
    `SP-4P presentation gate must not infer ${forbidden}.`);
}
assert.doesNotMatch(presentationJs, /CompetitionResult|CompetitionState|StackMeetDbContext|FinalsReportEngine/,
  'SP-4P presentation must validate the reviewed public contract, not become a ranking authority.');

console.log('SP-4P Finals placement presentation governance guards passed.');
