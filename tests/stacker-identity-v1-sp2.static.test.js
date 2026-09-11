const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityBackfillModels.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityBackfillService.cs';
const testProject = 'tests/StackerIdentityBackfillTests/StackerIdentityBackfillTests.csproj';
const testProgram = 'tests/StackerIdentityBackfillTests/Program.cs';

for (const file of [modelsPath, servicePath, testProject, testProgram]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const models = read(modelsPath);
assert.match(models, /enum StackerIdentityBackfillItemStatus/);
assert.match(models, /NoCandidates/);
assert.match(models, /ReviewRequired/);
assert.match(models, /enum StackerIdentityBackfillApplyStatus/);
assert.match(models, /AlreadyLinked/);
assert.match(models, /ResolutionBlocked/);
assert.match(models, /ExplicitNadiTrackId/);
assert.match(models, /CandidateConfirmed/);
assert.match(models, /CreateNewOverrideConfirmed/);
assert.ok(!/Email,\s*$|Phone,\s*$/m.test(models), 'backfill report DTOs must not expose email/phone fields');

const service = read(servicePath);
assert.match(service, /DefaultDiscoveryItems = 100/);
assert.match(service, /MaximumDiscoveryItems = 500/);
assert.match(service, /AsNoTracking\(\)/);
assert.match(service, /!database\.StackerIdentityLinks\.Any/);
assert.match(service, /StackerIdentityMatcher\.FindMatches/);
assert.match(service, /StackerIdentityResolutionPolicy\.Resolve/);
assert.match(service, /StackerIdentityPersistenceService/);
assert.match(service, /Recompute against current persisted data on every apply/);
assert.match(service, /ReviewRequired/);
assert.ok(!/Candidates\.Count\s*==\s*1[\s\S]{0,160}PersistAsync/.test(service), 'a unique candidate must never auto-link');
assert.ok(!/MatchStrength\.Strong[\s\S]{0,160}PersistAsync/.test(service), 'strong evidence must not bypass operator resolution');

const program = read(testProgram);
for (const scenario of [
  'discovery is read-only',
  'unique strong candidate still requires review',
  'strong historical candidate cannot auto-link',
  'possible historical candidate requires review note',
  'explicit NADITrack ID is authoritative for historical linking',
  'unknown explicit NADITrack ID cannot fall through to historical create-new',
  'completed competition backfill becomes empty and idempotent',
  'discovery batch size is bounded'
]) {
  assert.ok(program.includes(scenario), `SP-2 integration scenario missing: ${scenario}`);
}

const controllersDir = path.join(root, 'backend/StackMeet.Api/Controllers');
for (const file of fs.readdirSync(controllersDir).filter(name => name.endsWith('.cs'))) {
  const controller = fs.readFileSync(path.join(controllersDir, file), 'utf8');
  assert.ok(!controller.includes('StackerIdentityBackfillService'), `SP-2 must not expose backfill HTTP API yet: ${file}`);
}

console.log('SP-2 historical identity backfill guards passed.');
