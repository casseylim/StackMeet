'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const service = read('backend/StackMeet.Api/Activities/SportStacking/Identity/IdentityLinkedTeamCareerService.cs');
const integrity = read('backend/StackMeet.Api/Services/CompetitionTeamResultIntegrityService.cs');
const results = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const state = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const profileController = read('backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs');
const profileModels = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const integration = read('tests/StackerTeamCareerReadModelTests/Program.cs');

assert.match(integrity, /TryReadReadyTeamMemberships/);
assert.match(integrity, /CompetitionReadyTeamMemberships/);
assert.match(service, /new CompetitionTeamResultIntegrityService\(database\)/);
assert.match(service, /TryReadReadyTeamMemberships\(state\.JsonData/);

assert.match(service, /database\.StackerIdentityLinks/);
assert.match(service, /item\.NadiTrackId == normalizedId && item\.IsPublicProfile/);
assert.match(service, /competition\.IsPubliclyListed/);
assert.match(service, /competition\.Status == "Closed"/);
assert.match(service, /competition\.Status == "Archived"/);
assert.match(service, /competition\.ArchivedAt != null/);
assert.match(service, /CompetitionResultRules\.NormalizeParticipantType/);
assert.match(service, /CompetitionResultRules\.NormalizeStage/);
assert.match(service, /CompetitionResultRules\.NormalizeEvent/);
assert.match(service, /MembershipEvidenceVersion = "sp4q-team-membership-v1"/);
assert.match(service, /SHA256\.HashData/);
assert.match(service, /hasExternalPartner = participantType == "Doubles" && members\.Count == 1/);
assert.match(service, /group\.Count\(\) != 1/);

for (const forbidden of [
  '.FirstName',
  '.LastName',
  '.WssaId',
  '.BirthDate',
  '.Email',
  '.Phone'
]) {
  assert.ok(!service.includes(forbidden), 'SP-4Q must not match/read identity using ' + forbidden);
}

assert.match(results, /competition\.Status is "Closed" or "Archived" \|\| competition\.ArchivedAt is not null/);
assert.match(state, /ValidateStateAgainstExistingResultsAsync/);
assert.match(integrity, /cannot change Doubles team members while SQL results reference it/);
assert.match(integrity, /cannot change Timed Relay team members while SQL results reference it/);

assert.ok(!profileController.includes('IdentityLinkedTeamCareerService'),
  'SP-4Q must not activate team career through the public profile controller');
assert.ok(!profileModels.includes('IdentityLinkedTeamCareer'),
  'SP-4Q must not add team career to the public profile contract');
assert.ok(!program.includes('IdentityLinkedTeamCareerService'),
  'SP-4Q must not activate team career in application startup');

for (const scenario of [
  'only linked finalized/public team performances enter SP-4Q',
  'external parent display name never creates a permanent identity team link',
  'shared server integrity boundary blocks historical team membership drift while SQL results exist',
  'multiple reviewed same-competition identity links fail closed'
]) {
  assert.ok(integration.includes(scenario), 'missing SP-4Q integration scenario: ' + scenario);
}

console.log('SP-4Q identity-linked team career static guards passed.');
