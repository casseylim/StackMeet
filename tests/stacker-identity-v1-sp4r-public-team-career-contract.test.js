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

const contract = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicTeamCareerPublicationContract.cs');

assert.match(contract, /PublicationVersion = "sp4r-public-team-career-v1"/);
assert.match(contract, /Verified team membership · teammate identities withheld/);
assert.match(contract, /point\.ParticipantType is "Doubles" or "Timed Relay"/);
assert.match(contract, /point\.Stage is "Prelims" or "Finals"/);
assert.match(contract, /point\.ParticipantType != "Timed Relay" \|\| point\.EventCode == "3-6-3"/);
assert.match(contract, /IsSha256\(evidence\.RegisteredMembershipSha256\)/);
assert.match(contract, /evidence\.CompetitionStateRevision > 0/);
assert.match(contract, /evidence\.CompetitionResultsRevision > 0/);
assert.match(contract, /evidence\.ResultRevision > 0/);
assert.match(contract, /point\.OfficialBestTime == point\.RawBestTime \+ point\.AppliedPenalty/);
assert.match(contract, /ContextAmbiguous/);

const publicPoint = recordBody(contract, 'PublicTeamCareerPoint');
for (const forbidden of [
  'TeamCode', 'RegisteredMemberCount', 'HasExternalPartner', 'Evidence',
  'RegisteredMembershipSha256', 'CompetitionStateRevision', 'CompetitionResultsRevision', 'ResultRevision',
  'ParticipantCode', 'ParticipantName', 'MemberParticipantCodes', 'MemberNames', 'PartnerName', 'ParentName',
  'StackerId', 'CompetitionId', 'ResultPublicId', 'BirthDate', 'Email', 'Phone', 'WssaId',
  'Placement', 'Rank', 'Medal', 'Award', 'Podium', 'Record'
]) {
  assert.doesNotMatch(publicPoint, new RegExp(`\\b${forbidden}\\b`, 'i'),
    `SP-4R public point must not expose ${forbidden}.`);
}

assert.doesNotMatch(contract, /StackMeetDbContext|database\\.(?:CompetitionResults|CompetitionStates|Stackers)/,
  'SP-4R contract must transform SP-4Q facts, not read mutable persistence sources.');

console.log('SP-4R public team career publication contract static guards passed.');
