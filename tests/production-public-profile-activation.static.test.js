'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const programPath = path.join(root, 'tools', 'StackerIdentityActivation', 'Program.cs');
const projectPath = path.join(root, 'tools', 'StackerIdentityActivation', 'StackerIdentityActivation.csproj');

assert.ok(fs.existsSync(programPath), 'activation Program.cs must exist');
assert.ok(fs.existsSync(projectPath), 'activation csproj must exist');

const p = fs.readFileSync(programPath, 'utf8');
const project = fs.readFileSync(projectPath, 'utf8');

assert.ok(project.includes('../../backend/StackMeet.Api/StackMeet.Api.csproj'),
  'activation tool must reuse reviewed application identity services');
assert.ok(p.includes('StackerIdentityMatcher.FindMatches'),
  'activation must perform duplicate/existing identity discovery');
assert.ok(p.includes('StackerIdentityResolutionPolicy.Resolve'),
  'activation must use reviewed SP-0C resolution policy');
assert.ok(p.includes('StackerIdentityPersistenceService'),
  'activation must use reviewed SP-1 persistence service');
assert.ok(p.includes('CryptographicNadiTrackIdGenerator'),
  'new identities must use the reviewed cryptographic NADITrack ID generator');
assert.ok(p.includes('ACTIVATE PUBLIC PROFILE {competitionId}/{stackerCode}'),
  'production write must require target-bound exact confirmation');
assert.ok(p.includes('MODE={(execute ? "EXECUTE" : "DRY-RUN")}'),
  'dry-run/execute mode must be explicit');
assert.ok(p.includes('PRODUCTION_WRITES=0'),
  'dry-run must emit zero-write evidence');
assert.ok(p.includes('PUBLIC_PROFILE_PROJECTION=PASS'),
  'execution must verify public projection after activation');
assert.ok(p.includes('if (publish) Fail("Create/link and publication must be separate actions.'),
  'creation/linking must reject combined publication');
assert.ok(p.includes('if (!publish)') && p.includes('PUBLIC_PROFILE_ACTIVATED=FALSE'),
  'link-only execution must preserve visibility');
assert.ok(p.includes('StackerIdentity.PublicationChanged') && p.includes('StackerIdentity.Linked'),
  'CLI transitions must use existing administrative audit events');
assert.ok(p.includes('Expected display name mismatch'),
  'target must be pinned by expected display name');
assert.ok(p.includes('ACTION_REQUIRED=REVIEW_EXISTING_IDENTITY_CANDIDATES'),
  'ambiguous identity candidates must fail closed');
assert.ok(!/Database\.Migrate|ExecuteSqlRaw|ExecuteSqlInterpolated|FromSqlRaw|FromSqlInterpolated|SqlCommand|DELETE\s+FROM|INSERT\s+INTO|UPDATE\s+dbo/i.test(p),
  'activation tool must not bypass EF/domain services with migration or raw SQL');
assert.ok(!/stacker\.(FirstName|LastName|BirthDate|Email|Phone|Gender|Country)\s*=/i.test(p),
  'activation tool must not mutate the competition registration snapshot');
assert.ok(!/Console\.WriteLine\([^\n]*(Email|Phone|BirthDate)/i.test(p),
  'activation output must not print private identity fields');

console.log('Governed production public-profile activation tool static guards passed.');
