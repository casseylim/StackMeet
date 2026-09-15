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

const integration = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicFinalsPlacementCareerIntegrationService.cs');
const contract = read('backend/StackMeet.Api/Activities/SportStacking/Identity/PublicFinalsPlacementPublicationContract.cs');
const models = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerCareerProfileModels.cs');
const controller = read('backend/StackMeet.Api/Controllers/PublicStackerProfilesController.cs');
const sharedRegistration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const sportStackingRegistration = read('backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackingIdentityRegistration.cs');
const program = read('backend/StackMeet.Api/Program.cs');
const profileJs = read('backend/StackMeet.Api/wwwroot/profile/profile.js');
const profileHtml = read('backend/StackMeet.Api/wwwroot/profile/index.html');

assert.match(integration, /FinalsRankingGovernanceService/,
  'SP-4N selection must start from persisted immutable governance evidence.');
assert.match(integration, /SnapshotJson/);
assert.match(integration, /SnapshotSha256/);
assert.match(integration, /competitionState/);
assert.match(integration, /finalsResults/);
assert.match(integration, /FinalsRankingCertificationReadinessService\.OperatorContractVersion/);
assert.match(integration, /PublicFinalsPlacementPublicationContract\.RequiredCategory/);
assert.match(integration, /PublicFinalsPlacementPublicationContract\.RequiredGender/);
assert.match(integration, /IdentityLinkedFinalsPlacementCareerService/,
  'SP-4N must reuse SP-4L rather than calculate a second placement path.');
assert.match(integration, /PublicFinalsPlacementPublicationContract\.Create/,
  'SP-4N must pass all public facts through the SP-4M privacy contract.');

assert.doesNotMatch(integration, /database\.CompetitionResults/,
  'SP-4N canonical selection must not read current CompetitionResult rows.');
assert.doesNotMatch(integration, /\.Gender\b|\.CustomDivision\b|\.Special\b/,
  'SP-4N canonical selection must not use current demographic/division attributes.');
assert.doesNotMatch(integration, /FirstName|LastName|ParticipantName/,
  'SP-4N selection must not match historical placement by name.');

assert.match(contract, /NormalizeResultStatus/,
  'SP-4M boundary must normalize actual SP-4K\/SP-4L lowercase result statuses.');
assert.match(contract, /"valid" => "Valid"/);
assert.match(contract, /"scratch" => "Scratch"/);

const publicPoint = recordBody(contract, 'PublicFinalsPlacementPoint');
for (const forbidden of [
  'Division', 'Category', 'Gender', 'Scope', 'Evidence', 'ParticipantCode', 'ParticipantName',
  'CompetitionId', 'StackerId', 'ResultPublicId', 'SnapshotSha256', 'SourceStateRevision',
  'SourceResultsRevision', 'BirthDate', 'Email', 'Phone', 'WssaId', 'Medal', 'Award', 'Podium', 'Record'
]) {
  assert.doesNotMatch(publicPoint, new RegExp(`\\b${forbidden}\\b`, 'i'),
    `SP-4N public placement point must not expose ${forbidden}.`);
}

assert.match(models, /PublicFinalsPlacementCareerPublication\? FinalsPlacements \{ get; init; \}/,
  'public profile contract must add placement as an additive SP-4N member.');
assert.match(controller, /FinalsPlacements = publication/,
  'existing public profile endpoint must attach the privacy-safe publication.');
assert.doesNotMatch(sharedRegistration, /PublicFinalsPlacementCareerIntegrationService|\bFinals\b/,
  'Shared Core activity-module registration must remain free of Sport Stacking Finals services.');
assert.match(sportStackingRegistration, /AddScoped<SportStacking\.Identity\.PublicFinalsPlacementCareerIntegrationService>/,
  'SP-4N integration service must be registered through a Sport-Stacking-owned DI seam.');
assert.match(program, /AddSportStackingIdentityProfileServices\(\)/,
  'application composition root must activate the Sport Stacking identity/profile service seam.');

assert.match(profileHtml, /id="finalsPlacements"/);
assert.match(profileHtml, /Immutable certified evidence only/);
assert.match(profileJs, /renderFinalsPlacements\(profile\.finalsPlacements\)/);
assert.match(profileJs, /competition-time division/);
assert.doesNotMatch(profileJs, /innerHTML\s*=/,
  'public placement browser rendering must remain DOM\/textContent based.');
assert.match(profileJs, /credentials:\s*'omit'/,
  'public profile fetch remains credential-free.');
assert.match(profileJs, /cache:\s*'no-store'/,
  'public profile fetch remains non-cacheable.');

for (const sensitive of ['birthDate', 'email', 'phone', 'wssaId', 'snapshotSha256', 'sourceStateRevision', 'sourceResultsRevision']) {
  assert.doesNotMatch(profileJs, new RegExp(`point\\.${sensitive}`, 'i'),
    `browser must not render ${sensitive} from placement facts.`);
}

console.log('SP-4N public Finals placement integration static guards passed.');
