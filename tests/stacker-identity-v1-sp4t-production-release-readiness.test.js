'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const manifest = JSON.parse(read('docs/deployment/production-release-v2-manifest.json'));
const workflow = read('.github/workflows/deploy-production-release-v2.yml');
const runbook = read('docs/deployment/PRODUCTION_RELEASE_V2_RUNBOOK.md');

assert.equal(manifest.releaseVersion, 'production-release-v2-2026-09-24');
assert.equal(manifest.approvedLiveSourceSha, 'ba80538a912a3dd0f708fdd65a8823ed7988dbd6');

assert.deepEqual(manifest.databaseMigrations, [
  '20260906033000_AddCompetitionActivityModuleCode',
  '20260910103000_StackerIdentityPersistenceV1',
  '20260914093000_FinalsRankingGovernanceSp4g'
]);

assert.equal(manifest.applicationFiles.length, 11, 'SP-4T Release v2 must govern exactly 11 application files');
assert.equal(manifest.applicationFiles.filter(item => item.expectedLiveState === 'present').length, 6);
assert.equal(manifest.applicationFiles.filter(item => item.expectedLiveState === 'absent').length, 5);

const byRemote = new Map(manifest.applicationFiles.map(item => [item.remote, item]));
for (const remote of [
  '/wwwroot/app.js',
  '/wwwroot/profile/index.html',
  '/wwwroot/profile/profile.js',
  '/StackMeet.Api.dll'
]) {
  assert.ok(byRemote.has(remote), `SP-4T manifest missing ${remote}`);
}

assert.equal(byRemote.get('/wwwroot/app.js').expectedLiveState, 'present',
  'PR #62 team-integrity app.js is an existing production asset and must be fingerprinted before overwrite');
assert.equal(byRemote.get('/wwwroot/profile/index.html').expectedLiveState, 'absent');
assert.equal(byRemote.get('/wwwroot/profile/profile.js').expectedLiveState, 'absent');
assert.equal(byRemote.get('/StackMeet.Api.dll').deployOrder, 100, 'DLL must remain last');

assert.match(workflow, /workflow_dispatch:/);
assert.doesNotMatch(workflow, /\n\s*(push|pull_request|schedule):\s*\n/i,
  'production release workflow must remain manual-only');
assert.match(workflow, /Release v2 must contain exactly 11 application files/);
assert.match(workflow, /RELEASE_FILE_COUNT=11/);
assert.match(workflow, /POST_UPLOAD_FILE_COUNT=11/);
assert.match(workflow, /Get200 "\$b\/app\.js"\|Out-Null/);
assert.match(workflow, /MIGRATIONS-APPLIED-AND-VERIFIED/);
assert.match(workflow, /POOL-STOPPED/);
assert.match(workflow, /RELEASE-V2-PATHS-CONFIRMED/);
assert.match(workflow, /DEPLOY RELEASE V2/);
assert.match(workflow, /MASTER_RECHECK_BEFORE_WRITE=PASS/);
assert.match(workflow, /PRODUCTION_WRITES=0/);

assert.doesNotMatch(workflow, /\bdotnet\s+ef\s+database\s+update\b/i,
  'GitHub workflow must not execute production migrations');
assert.doesNotMatch(workflow, /\bInvoke-Sqlcmd\b|\bsqlcmd\b/i,
  'GitHub workflow must not execute production SQL');
assert.match(workflow, /Forbidden configuration change/);
assert.match(workflow, /web\\\.config/);
assert.match(workflow, /appsettings/);

assert.match(runbook, /exactly eleven files/);
assert.match(runbook, /ten reviewed wwwroot assets/);
assert.match(runbook, /wwwroot\/app\.js/);
assert.match(runbook, /six expected existing files/);
assert.match(runbook, /five new release files are absent/);
assert.match(runbook, /POST_UPLOAD_FILE_COUNT=11/);

console.log('SP-4T production release readiness guards passed.');
