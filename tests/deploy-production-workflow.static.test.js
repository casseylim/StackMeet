const assert = require('assert');
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const p = path.join(root, '.github', 'workflows', 'deploy-production.yml');
assert.ok(fs.existsSync(p), 'deploy-production.yml must exist');
const w = fs.readFileSync(p, 'utf8');
const has = (s, m) => assert.ok(w.includes(s), m || `missing: ${s}`);
const no = (r, m) => assert.ok(!r.test(w), m || `forbidden: ${r}`);

has('workflow_dispatch:');
no(/\n\s*(push|pull_request|schedule):\s*\n/i, 'production workflow must remain manual-only');
has('contents: read');
has('group: naditrack-production');
has('cancel-in-progress: false');
has('APPROVED_LOCAL_DLL: backend/StackMeet.Api/bin/Release/net8.0/StackMeet.Api.dll');
has('APPROVED_REMOTE_PATH: __UNCONFIRMED__');
has("REMOTE_PATH_CONFIRMED: 'false'");
has("if: ${{ inputs.operation == 'deploy' && false }}");
has("throw 'Deployment structurally disabled: remote FTP path is unconfirmed'");
has('environment: production');
has('POOL-STOPPED');
has('DEPLOY StackMeet.Api.dll');
has('PRE_UPLOAD_BACKUP=PASS');
has('ROLLBACK_ATTEMPTED=True');
has('ROLLBACK_VERIFIED=False');
has('PRODUCTION_STATE=UNKNOWN');
has('DO NOT START THE POOL');

has('actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4');
has('actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4');
no(/uses:\s+actions\/(?:checkout|setup-dotnet)@v\d+/i, 'production actions must be pinned');

for (const r of [/\bdotnet\s+publish\b/i,/\bmsdeploy\b/i,/\bwebdeploy\b/i,/\brsync\b/i,/\bInvoke-Sqlcmd\b/i,/\bsqlcmd\b/i,/\bdotnet\s+ef\b/i,/\bdatabase\s+update\b/i]) no(r);
has("'(^|/)Migrations/'");
has("'\\.sql$'");
has("'(^|/)web\\.config$'");
has("'(^|/)appsettings(?:\\..+)?\\.json$'");

const storCalls = [...w.matchAll(/\bStor\s+([^\r\n;]+)/g)].map(m => m[1].trim());
assert.deepStrictEqual(storCalls, ['$dll $remote', '$backup $remote'], 'only approved DLL/rollback backup may be uploaded');

console.log('Production deployment workflow static guards passed.');
