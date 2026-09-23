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

const integration = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicTeamCareerIntegrationService.cs');
const contract = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicTeamCareerPublicationContract.cs');
const models = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs');
const controller = read('backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs');
const registration = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackingIdentityRegistration.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const profileJs = read('backend/StackMeet.Api/wwwroot/profile/profile.js');
const profileHtml = read('backend/StackMeet.Api/wwwroot/profile/index.html');

assert.match(integration, /IdentityLinkedTeamCareerService/,
  'SP-4S must reuse SP-4Q as the only team career source.');
assert.match(integration, /PublicTeamCareerPublicationContract\.Create/,
  'SP-4S must pass team facts through the SP-4R privacy contract.');
assert.match(integration, /catch \(InvalidOperationException\)/,
  'SP-4S must fail closed instead of taking down the public profile.');

assert.doesNotMatch(integration, /CompetitionResults|CompetitionStates|Stackers|TryReadReadyTeamMemberships/,
  'SP-4S integration must not create a second team parsing/result path.');

const publicPoint = recordBody(contract, 'PublicTeamCareerPoint');
for (const forbidden of [
  'TeamCode', 'RegisteredMemberCount', 'HasExternalPartner', 'Evidence',
  'RegisteredMembershipSha256', 'CompetitionStateRevision', 'CompetitionResultsRevision', 'ResultRevision',
  'ParticipantCode', 'ParticipantName', 'MemberParticipantCodes', 'MemberNames', 'PartnerName', 'ParentName',
  'StackerId', 'CompetitionId', 'ResultPublicId', 'BirthDate', 'Email', 'Phone', 'WssaId',
  'Placement', 'Rank', 'Medal', 'Award', 'Podium', 'Record'
]) {
  assert.doesNotMatch(publicPoint, new RegExp(`\\b${forbidden}\\b`, 'i'),
    `SP-4S public team point must not expose ${forbidden}.`);
}

assert.match(models, /PublicTeamCareerPublication\? TeamCareer \{ get; init; \}/,
  'public profile contract must add team career as an additive SP-4S member.');
assert.match(controller, /TeamCareer = publication/,
  'existing public profile endpoint must attach the SP-4R publication.');
assert.match(registration, /AddScoped<SportStacking\.Identity\.PublicTeamCareerIntegrationService>/,
  'SP-4S service must be registered through the Sport-Stacking-owned DI seam.');
assert.match(program, /AddSportStackingIdentityProfileServices\(\)/,
  'application composition root must continue activating the Sport Stacking identity/profile seam.');

assert.match(profileHtml, /id="teamCareer"/);
assert.match(profileHtml, /Teammate identities withheld/);
assert.match(profileJs, /renderTeamCareer\(profile\.teamCareer\)/);
assert.match(profileJs, /point\.participantType/);
assert.match(profileJs, /point\.stage/);
assert.match(profileJs, /point\.eventCode/);
assert.doesNotMatch(profileJs, /point\.(?:teamCode|registeredMemberCount|hasExternalPartner|registeredMembershipSha256|memberNames|parentName|partnerName)/i,
  'browser must not read withheld team/member relationship fields.');
assert.doesNotMatch(profileJs, /innerHTML\s*=/,
  'public team career rendering must remain DOM/textContent based.');
assert.match(profileJs, /credentials:\s*'omit'/);
assert.match(profileJs, /cache:\s*'no-store'/);

console.log('SP-4S public team career integration static guards passed.');
