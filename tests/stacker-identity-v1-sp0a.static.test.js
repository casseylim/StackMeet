const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const identityPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerIdentity.cs';
const idRulesPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/NadiTrackIdRules.cs';
const linkPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityLink.cs';
const methodsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityMatchMethod.cs';
const designPath = 'docs/architecture/STACKER_IDENTITY_V1.md';

for (const file of [identityPath, idRulesPath, linkPath, methodsPath, designPath]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const identity = read(identityPath);
assert.match(identity, /class SportStackerIdentity/);
assert.match(identity, /string NadiTrackId/);
assert.match(identity, /string\? WssaId/);
assert.match(identity, /Optional external WSSA reference/);
assert.match(identity, /bool IsPublicProfile/);

const idRules = read(idRulesPath);
assert.match(idRules, /Prefix = "NDT-"/);
assert.match(idRules, /BodyLength = 7/);
const alphabet = idRules.match(/Alphabet = "([^"]+)"/)?.[1];
assert.ok(alphabet, 'NADITrack ID alphabet must be declared');
for (const ambiguous of ['0', 'O', '1', 'I', 'L']) {
  assert.ok(!alphabet.includes(ambiguous), `NADITrack ID alphabet must exclude ambiguous ${ambiguous}`);
}
assert.match(idRules, /ToUpperInvariant/);
assert.match(idRules, /IsValid/);

const link = read(linkPath);
assert.match(link, /class StackerIdentityLink/);
assert.match(link, /SportStackerIdentityId/);
assert.match(link, /StackerId/);
assert.match(link, /MatchMethod/);
assert.match(link, /LinkedAt/);

const methods = read(methodsPath);
for (const value of ['CREATED_NEW', 'NADITRACK_ID', 'WSSA_ID', 'NAME_AND_BIRTH_DATE', 'EMAIL', 'PHONE', 'MANUAL']) {
  assert.ok(methods.includes(`"${value}"`), `match provenance ${value} must remain stable`);
}

const design = read(designPath);
for (const invariant of [
  'A competition Stacker is an entry. A NADITrack Stacker is a person.',
  'same NADITrack ID for life',
  'WssaId` is optional external-reference metadata only',
  'must never auto-merge two identities based on name alone',
  'must not rewrite historical competition registration snapshots'
]) {
  assert.ok(design.includes(invariant), `design invariant missing: ${invariant}`);
}

// SP-0A's temporary no-persistence boundary is intentionally superseded by SP-1.
// Preserve the more important compatibility contract: the competition-scoped Stacker/API does not gain a public NadiTrackId field.
const stackerModel = read('backend/StackMeet.Api/Models/Stacker.cs');
const stackerDtos = read('backend/StackMeet.Api/Dtos/StackerDtos.cs');
const stackerController = read('backend/StackMeet.Api/Controllers/StackersController.cs');
assert.match(stackerModel, /string\? WssaId/);
assert.ok(!stackerModel.includes('NadiTrackId'), 'competition-scoped Stacker must not become the permanent identity');
assert.match(stackerDtos, /WssaId/);
assert.ok(!stackerDtos.includes('NadiTrackId'), 'existing Stacker DTOs must remain backward compatible');
assert.match(stackerController, /WssaId/);
assert.ok(!stackerController.includes('NadiTrackId'), 'current Stacker API behavior must remain unchanged during SP-1');

console.log('SP-0A permanent NADITrack identity foundation guards passed.');
