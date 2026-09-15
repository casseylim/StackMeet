'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const recordBody = (source, name) => {
  const match = source.match(new RegExp(`public sealed record ${name}\\(([\\s\\S]*?)\\);`));
  assert.ok(match, `${name} contract must exist.`);
  return match[1];
};

const contract = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicFinalsPlacementPublicationContract.cs');
const controller = read('backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const profileJs = read('backend/StackMeet.Api/wwwroot/profile/profile.js');
const publicModels = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs');

assert.match(contract, /PublicationVersion = "sp4m-public-finals-placement-v1"/);
assert.match(contract, /RequiredCategory = "mixed"/);
assert.match(contract, /RequiredGender = "all"/);
assert.match(contract, /no additional gender filter/);
assert.match(contract, /FinalsHistoricalPlacementProjectionService\.ProjectionVersion/);
assert.match(contract, /FinalsRankingRuleVersions\.GovernedFinalsV2/);
assert.match(contract, /FinalsRankingGovernanceService\.GovernedV2SnapshotSchemaVersion/);
assert.match(contract, /FinalsRankingCertificationReadinessService\.OperatorContractVersion/);
assert.match(contract, /IsSha256\(evidence\.SnapshotSha256\)/);

const publicPoint = recordBody(contract, 'PublicFinalsPlacementPoint');
for (const forbidden of [
  'Division', 'Category', 'Gender', 'Scope', 'Evidence', 'ParticipantCode', 'ParticipantName',
  'CompetitionId', 'StackerId', 'ResultPublicId', 'BirthDate', 'Email', 'Phone', 'WssaId',
  'Medal', 'Award', 'Podium', 'Record'
]) {
  assert.doesNotMatch(publicPoint, new RegExp(`\\b${forbidden}\\b`, 'i'),
    `SP-4M public point must not expose ${forbidden}.`);
}

assert.doesNotMatch(contract, /StackMeetDbContext|CompetitionResults|CompetitionState|Stackers/,
  'SP-4M contract must transform immutable SP-4L facts, not read mutable ranking sources.');

for (const [name, source] of [
  ['public profile controller', controller],
  ['application startup', program],
  ['public profile browser', profileJs],
  ['existing public profile DTO', publicModels]
]) {
  assert.doesNotMatch(source, /PublicFinalsPlacementPublicationContract|PublicFinalsPlacementCareerPublication/,
    `SP-4M must not activate placement through ${name}.`);
}

const existingFinalsPoint = recordBody(publicModels, 'SportStackerFinalsHistoryPoint');
const existingFinalsSummary = recordBody(publicModels, 'SportStackerEventFinalsSummary');
const existingPublicProfile = recordBody(publicModels, 'PublicSportStackerCareerProfile');
for (const signature of [existingFinalsPoint, existingFinalsSummary, existingPublicProfile]) {
  assert.doesNotMatch(signature, /\bPlacement\b|\bSharesPlacement\b/,
    'Existing public career profile signatures must remain placement-free during SP-4M.');
}

console.log('SP-4M public Finals placement contract static guards passed.');
