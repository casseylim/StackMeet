const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const identityPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/SportStackerIdentity.cs';
const linkPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityLink.cs';
const generatorPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/NadiTrackIdGenerator.cs';
const servicePath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityPersistenceService.cs';
const modelsPath = 'backend/StackMeet.Api/Activities/SportStacking/Identity/StackerIdentityPersistenceModels.cs';
const migrationPath = 'backend/StackMeet.Api/Migrations/20260910103000_StackerIdentityPersistenceV1.cs';
const integrationProject = 'tests/StackerIdentityPersistenceTests/StackerIdentityPersistenceTests.csproj';
const integrationProgram = 'tests/StackerIdentityPersistenceTests/Program.cs';

for (const file of [identityPath, linkPath, generatorPath, servicePath, modelsPath, migrationPath, integrationProject, integrationProgram]) {
  assert.ok(fs.existsSync(path.join(root, file)), `${file} must exist`);
}

const identity = read(identityPath);
assert.match(identity, /Table\("SportStackerIdentity", Schema = "dbo"\)/);
assert.match(identity, /Index\(nameof\(NadiTrackId\), IsUnique = true/);
assert.match(identity, /MaxLength\(11\)/);
assert.match(identity, /bool IsPublicProfile/);

const link = read(linkPath);
assert.match(link, /Table\("StackerIdentityLink", Schema = "dbo"\)/);
assert.match(link, /Index\(nameof\(StackerId\), IsUnique = true/);
assert.match(link, /ResolutionReasonCode/);
assert.match(link, /ResolutionNote/);
assert.match(link, /DeleteBehavior\(DeleteBehavior\.Restrict\)/);

const generator = read(generatorPath);
assert.match(generator, /RandomNumberGenerator\.GetInt32/);
assert.match(generator, /NadiTrackIdRules\.Alphabet/);
assert.match(generator, /NadiTrackIdRules\.Prefix/);
assert.ok(!/Interlocked|increment|sequence/i.test(generator), 'public NADITrack IDs must not become sequential/enumerable');

const service = read(servicePath);
assert.match(service, /MaxIdIssuanceAttempts = 32/);
assert.match(service, /IsolationLevel\.Serializable/);
assert.match(service, /Only an SP-0C Approved identity resolution may be persisted/);
assert.match(service, /AnyAsync\(item => item\.StackerId == stacker\.Id/);
assert.match(service, /NadiTrackIdRules\.IsValid\(generated\)/);
assert.match(service, /AnyAsync\(item => item\.NadiTrackId == normalized/);
assert.match(service, /IsPublicProfile = false/);
assert.match(service, /APPROVED_CREATE_NEW_DESPITE_CANDIDATES/);
assert.match(service, /Manual identity links require a persisted review note/);

const migration = read(migrationPath);
assert.match(migration, /Migration\("20260910103000_StackerIdentityPersistenceV1"\)/);
assert.match(migration, /name: "SportStackerIdentity"/);
assert.match(migration, /name: "StackerIdentityLink"/);
assert.match(migration, /name: "UX_SportStackerIdentity_NadiTrackId"/);
assert.match(migration, /name: "UX_StackerIdentityLink_StackerId"/);
assert.match(migration, /ResolutionReasonCode/);
assert.match(migration, /ResolutionNote/);
assert.match(migration, /onDelete: ReferentialAction\.Restrict/);
assert.ok(!/unique:\s*true[\s\S]{0,120}WssaId/.test(migration), 'SP-1 must not introduce WSSA uniqueness before legacy-data audit');

const stackerModel = read('backend/StackMeet.Api/Models/Stacker.cs');
const stackerDtos = read('backend/StackMeet.Api/Dtos/StackerDtos.cs');
const stackerController = read('backend/StackMeet.Api/Controllers/StackersController.cs');
assert.match(stackerModel, /StackerIdentityLink\? IdentityLink/);
assert.match(stackerModel, /JsonIgnore/);
assert.ok(!stackerModel.includes('NadiTrackId'), 'competition Stacker remains a snapshot, not the permanent identity');
assert.ok(!stackerDtos.includes('NadiTrackId'), 'SP-1 does not change the public Stacker DTO');
assert.ok(!stackerController.includes('NadiTrackId'), 'SP-1 does not expose a new Stacker API path');

const integration = read(integrationProgram);
for (const scenario of [
  'identity table migrated',
  'exact identity link does not create person',
  'issuer retries an existing generated ID',
  'manual review note persisted',
  'approved duplicate override can create distinct identity',
  'same competition stacker cannot be linked twice',
  'database enforces unique NADITrack ID'
]) {
  assert.ok(integration.includes(scenario), `SP-1 integration scenario missing: ${scenario}`);
}

console.log('SP-1 NADITrack identity persistence guards passed.');
