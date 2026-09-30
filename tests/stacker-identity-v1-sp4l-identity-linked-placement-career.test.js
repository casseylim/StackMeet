const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const servicePath = path.join(
  root,
  'backend',
  'StackMeet.Api',
  'Activities',
  'SportStacking',
  'Identity',
  'IdentityLinkedFinalsPlacementCareerService.cs');
const publicModelsPath = path.join(
  root,
  'backend',
  'StackMeet.Api',
  'Activities',
  'SportStacking',
  'Identity',
  'SportStackerCareerProfileModels.cs');
const publicControllerPath = path.join(
  root,
  'backend',
  'StackMeet.Api',
  'Controllers',
  'PublicStackerProfilesController.cs');
const programPath = path.join(root, 'backend', 'StackMeet.Api', 'Program.cs');

const service = fs.readFileSync(servicePath, 'utf8');
const publicModels = fs.readFileSync(publicModelsPath, 'utf8');
const publicController = fs.readFileSync(publicControllerPath, 'utf8');
const program = fs.readFileSync(programPath, 'utf8');

assert.match(
  service,
  /new FinalsHistoricalPlacementProjectionService\(database\)/,
  'SP-4L must delegate historical placement to the immutable SP-4K projector');

assert.match(
  service,
  /database\.StackerIdentityLinks\.AsNoTracking\(\)/,
  'SP-4L must associate history through persisted identity links');

assert.match(
  service,
  /link\.StackerId equals stacker\.Id/,
  'SP-4L may use the reviewed link target only to recover competition-scoped participant code');

for (const forbidden of [
  'database.CompetitionResults',
  'database.CompetitionStates',
  'stacker.FirstName',
  'stacker.LastName',
  'stacker.Gender',
  'stacker.BirthDate',
  'stacker.WssaId'
]) {
  assert.ok(
    !service.includes(forbidden),
    `SP-4L service must not use mutable/live ranking or name/profile authority: ${forbidden}`);
}

assert.match(
  service,
  /competition\.IsPubliclyListed/,
  'SP-4L must keep the finalized/public competition eligibility gate');
assert.match(
  service,
  /competition\.Status == "Closed"/,
  'SP-4L must accept finalized Closed competitions');
assert.match(
  service,
  /competition\.Status == "Archived"/,
  'SP-4L must accept finalized Archived competitions');

assert.ok(
  !publicModels.includes('IdentityLinkedFinalsPlacementCareer'),
  'SP-4L must not alter the existing public profile contract');
assert.ok(
  !publicController.includes('IdentityLinkedFinalsPlacementCareer'),
  'SP-4L must not expose placement through the public profile controller');
assert.ok(
  !program.includes('IdentityLinkedFinalsPlacementCareerService'),
  'SP-4L must not activate the placement career service in application startup');

assert.match(
  service,
  /FinalsHistoricalPlacementScope Scope/,
  'every SP-4L placement fact must retain its explicit SP-4K scope');
assert.match(
  service,
  /int\? Placement/,
  'non-valid Finals rows must be able to remain unplaced');
assert.match(
  service,
  /SnapshotSha256/,
  'SP-4L must retain immutable evidence provenance');

console.log('SP-4L identity-linked Finals placement career static guards passed.');
