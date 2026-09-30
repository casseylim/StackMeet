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

assert.match(contract, /PublicationVersion = "sp4m-public-finals-placement-v1"/);
assert.match(contract, /RequiredCategory = "mixed"/);
assert.match(contract, /RequiredGender = "all"/);
assert.match(contract, /no additional gender filter/);
assert.match(contract, /FinalsHistoricalPlacementProjectionService\.ProjectionVersion/);
assert.match(contract, /FinalsRankingRuleVersions\.GovernedFinalsV2/);
assert.match(contract, /FinalsRankingGovernanceService\.GovernedV2SnapshotSchemaVersion/);
assert.match(contract, /FinalsRankingCertificationReadinessService\.OperatorContractVersion/);
assert.match(contract, /IsSha256\(evidence\.SnapshotSha256\)/);
assert.match(contract, /NormalizeResultStatus/,
  'SP-4M publication contract must accept canonical SP-4K\/SP-4L result semantics independent of case.');

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

console.log('SP-4M public Finals placement contract static guards passed.');
