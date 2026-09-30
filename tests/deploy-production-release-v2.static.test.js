'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const workflowPath = '.github/workflows/deploy-production-release-v2.yml';
const manifestPath = 'docs/deployment/production-release-v2-manifest.json';
const w = read(workflowPath);
const manifest = JSON.parse(read(manifestPath));

const has = (value, message) => assert.ok(w.includes(value), message || ('missing: ' + value));
const no = (regex, message) => assert.ok(!regex.test(w), message || ('forbidden: ' + regex));

assert.equal(manifest.releaseVersion, 'production-release-v2-2026-09-24');
assert.equal(manifest.approvedLiveSourceSha, 'ba80538a912a3dd0f708fdd65a8823ed7988dbd6');
assert.deepEqual(manifest.databaseMigrations, [
  '20260906033000_AddCompetitionActivityModuleCode',
  '20260910103000_StackerIdentityPersistenceV1',
  '20260914093000_FinalsRankingGovernanceSp4g'
]);

const expectedApplicationFiles = [
  'backend/StackMeet.Api/wwwroot/js/reports/FinalsRankingPolicy.js',
  'backend/StackMeet.Api/wwwroot/profile/index.html',
  'backend/StackMeet.Api/wwwroot/profile/profile.css',
  'backend/StackMeet.Api/wwwroot/profile/profile.js',
  'backend/StackMeet.Api/wwwroot/results/profile-links.js',
  'backend/StackMeet.Api/wwwroot/js/auth/AuthSession.js',
  'backend/StackMeet.Api/wwwroot/js/reports/FinalsReportEngine.js',
  'backend/StackMeet.Api/wwwroot/js/storage/StackerApi.js',
  'backend/StackMeet.Api/wwwroot/results/index.html',
  'backend/StackMeet.Api/wwwroot/app.js',
  'backend/StackMeet.Api/bin/Release/net8.0/StackMeet.Api.dll'
];
assert.deepEqual(manifest.applicationFiles.map(item => item.source), expectedApplicationFiles);
assert.equal(manifest.applicationFiles.length, 11);
assert.equal(manifest.applicationFiles.filter(item => item.expectedLiveState === 'present').length, 6);
assert.equal(manifest.applicationFiles.filter(item => item.expectedLiveState === 'absent').length, 5);
const appJs = manifest.applicationFiles.find(item => item.remote === '/wwwroot/app.js');
assert.ok(appJs, 'team-integrity app.js must be in the governed release set');
assert.equal(appJs.expectedLiveState, 'present');
assert.equal(appJs.deployOrder, 95);
assert.equal(manifest.applicationFiles.at(-1).remote, '/StackMeet.Api.dll');
assert.equal(manifest.applicationFiles.at(-1).deployOrder, 100);
assert.equal(new Set(manifest.applicationFiles.map(item => item.remote)).size, 11, 'remote paths must be unique');
assert.equal(new Set(manifest.applicationFiles.map(item => item.deployOrder)).size, 11, 'deploy order values must be unique');
for (const item of manifest.applicationFiles) {
  assert.ok(item.remote === '/StackMeet.Api.dll' || item.remote.startsWith('/wwwroot/'), 'unexpected remote path: ' + item.remote);
  assert.ok(!/web\.config$|appsettings.*\.json$|\.sql$/i.test(item.source), 'forbidden deploy source: ' + item.source);
}

has('workflow_dispatch:');
no(/\n\s*(push|pull_request|schedule):\s*\n/i, 'Release v2 workflow must remain manual-only');
has('options: [preflight, ftp-readcheck, deploy, verify]');
has('group: naditrack-production');
has('cancel-in-progress: false');
has('RELEASE_MANIFEST: docs/deployment/production-release-v2-manifest.json');
has("DOTNET_EF_VERSION: '8.0.8'");
has('dotnet ef migrations script --idempotent');
has('actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 # v4');
has('RELEASE_MANIFEST_SHA256=');
has('MIGRATION_PACKAGE_SHA256=');
has('LIVE_FILESET_SHA256=');
has('PREFLIGHT_MANIFEST_REPRODUCED=PASS');
has('LIVE_FILESET_RECHECK=PASS');

no(/\bdotnet\s+ef\s+database\s+update\b/i, 'workflow must never apply production migrations');
no(/\bInvoke-Sqlcmd\b/i, 'workflow must not execute production SQL');
no(/\bsqlcmd\b/i, 'workflow must not execute production SQL');

has('MIGRATIONS-APPLIED-AND-VERIFIED');
has('POOL-STOPPED');
has('RELEASE-V2-PATHS-CONFIRMED');
has('DEPLOY RELEASE V2');
has('PRE_UPLOAD_BACKUP=PASS');
has('ROLLBACK_ATTEMPTED=True');
has('ROLLBACK_VERIFIED=True');
has('ROLLBACK_VERIFIED=False');
has('PRODUCTION_STATE=UNKNOWN');
has('DO NOT START THE POOL');
has('DeleteRemote');
has('--ftp-create-dirs');
has('MASTER_RECHECK_BEFORE_WRITE=PASS');
has('POOL_STATE=STOPPED-MANUAL-START-REQUIRED');

const readStart = w.indexOf('  ftp_readcheck:');
const readEnd = w.indexOf('\n  deploy:', readStart);
assert.ok(readStart >= 0 && readEnd > readStart, 'ftp-readcheck boundaries must exist');
const readOnly = w.slice(readStart, readEnd);
assert.ok(readOnly.includes('environment: production'));
assert.ok(readOnly.includes("'FTP_READ_ONLY_CHECK=PASS'"));
assert.ok(readOnly.includes("'PRODUCTION_WRITES=0'"));
assert.ok(!/--upload-file|\bStor\(|\bDeleteRemote\(|\bDELE\b/i.test(readOnly), 'ftp-readcheck must contain no remote-write primitive');

const deployStart = w.indexOf('  deploy:');
const verifyStart = w.indexOf('\n  verify:', deployStart);
assert.ok(deployStart >= 0 && verifyStart > deployStart, 'deploy/verify boundaries must exist');
const deployOnly = w.slice(deployStart, verifyStart);
assert.ok(deployOnly.includes("if: ${{ inputs.operation == 'deploy' }}"));
assert.ok(deployOnly.includes('environment: production'));
assert.ok(deployOnly.includes('expected_preflight_manifest_sha256'));
assert.ok(deployOnly.includes('expected_live_fileset_sha256'));
assert.ok(deployOnly.includes('expected_live_dll_sha256'));

const verifyOnly = w.slice(verifyStart);
assert.ok(verifyOnly.includes("'POST_START_HTTP_VALIDATION=PASS'"));
assert.ok(verifyOnly.includes("'RELEASE_V2_STATIC_ASSETS=PASS'"));
assert.ok(verifyOnly.includes('Get200 "$b/app.js"|Out-Null'), 'verify must read back the deployed app.js asset');
assert.ok(verifyOnly.includes("'PUBLIC_PROFILE_LINK_CONTRACT=PASS'"));
assert.ok(verifyOnly.includes("'PRIVACY_CHECK=PASS'"));
assert.ok(verifyOnly.includes("'PRODUCTION_WRITES=0'"));
assert.ok(!/--upload-file|\bStor\(|\bDeleteRemote\(|\bDELE\b/i.test(verifyOnly), 'verify must remain read only');

has('actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4');
has('actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4');
no(/uses:\s+actions\/(?:checkout|setup-dotnet|upload-artifact)@v\d+/i, 'production actions must be commit-pinned');

console.log('Production Release v2 workflow and manifest static guards passed.');
