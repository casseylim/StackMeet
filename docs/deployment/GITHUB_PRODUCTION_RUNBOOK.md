# NADITrack GitHub Production Deployment Runbook

## Purpose

This runbook defines the approved GitHub-controlled production deployment path for NADITrack / StackMeet.Api.

The production workflow is `.github/workflows/deploy-production.yml` and is manual-only (`workflow_dispatch`). It is designed for an exact one-file deployment of `StackMeet.Api.dll` with explicit source, hash, remote-path, application-pool, and rollback interlocks.

This workflow does **not** authorize database migrations, SQL changes, `web.config` changes, appsettings changes, directory synchronization, Web Deploy, or `dotnet publish`.

## Production baseline recorded 2026-09-05

Successful first GitHub-controlled production deployment:

- Deployed source SHA: `ba80538a912a3dd0f708fdd65a8823ed7988dbd6`
- Deployed DLL SHA256: `443531BC17E24251D6E059C99B2AF79BF324C253F2980800F2AFDC8271165D2E`
- Remote DLL path in the constrained FTP session: `/StackMeet.Api.dll`
- Deployment binary verification: PASS
- Post-start HTTP validation: PASS
- Phase 3F public API shape: PASS
- Privacy deny-list check: PASS
- Rollback required: no

Live `DEFAULT` competition snapshot observed during the successful post-start verification:

- results: 250
- stackers: 42
- doubles: 1
- relays: 3
- language: `en`

These record counts are an operational snapshot, **not** a permanent deployment invariant. The VERIFY operation reports current counts automatically. Expected counts are advisory by default and only become a hard failure when `enforce_expected_counts` is explicitly enabled.

> Important: GitHub `master` may advance after a production deployment because workflow, documentation, or application changes can be merged later. Always use a fresh PRE-FLIGHT result for the exact master SHA intended for the next deployment. Do not reuse an old master build hash.

## Repository and Environment controls

Required controls:

- repository visibility: Public
- `master`: protected
- pull request required before merge
- required check: `Build and test`
- branch must be up to date before merge
- bypass disabled
- force pushes disabled
- branch deletion disabled for `master`
- GitHub Environment: `production`
- `production` Environment deployment branches: protected branches only

Environment secret names:

- `PROD_FTP_HOST`
- `PROD_FTP_USERNAME`
- `PROD_FTP_PASSWORD`

Environment variable names:

- `PROD_FTP_PORT`
- `PROD_FTP_ENABLE_SSL`

Do not commit or document secret values in the repository.

## Exact deployment manifest

Local build artifact:

`backend/StackMeet.Api/bin/Release/net8.0/StackMeet.Api.dll`

Remote path:

`/StackMeet.Api.dll`

Deployment file count must remain exactly 1.

## Forbidden deployment scope

The production DLL workflow must not perform or include:

- `dotnet publish`
- Web Deploy / msdeploy
- rsync or directory mirroring
- wildcard or recursive FTP upload
- `web.config`
- `appsettings.json` or `appsettings.*.json`
- SQL scripts
- Entity Framework migration execution
- `dotnet ef database update`
- `sqlcmd` / `Invoke-Sqlcmd`
- database writes
- automatic IIS/application-pool restart

If a source delta contains a migration, `.sql` file, `StackMeet.Api.csproj`, `web.config`, or appsettings change, the one-DLL workflow must fail closed. A future database migration requires a separate explicitly approved workflow and backup procedure.

## Standard deployment sequence

### 1. PRE-FLIGHT — no production connection

Run **NADITrack Production Deployment** from `master` with:

- `operation = preflight`
- `commit_sha =` exact current protected `master` SHA
- `live_source_sha =` source SHA of the DLL currently deployed to production

PRE-FLIGHT must confirm:

- requested SHA is exact current `master`
- live source SHA is an ancestor of the requested SHA
- forbidden deployment-class source changes are absent
- Release restore/build succeeds
- approved DLL exists
- one-file manifest is valid
- fresh DLL SHA256 and ProductVersion are reported
- production connections = 0
- production writes = 0

Record the reported DLL SHA256. It is the `expected_build_sha256` for this deployment only.

### 2. FTP READCHECK — production read only

Run:

- `operation = ftp-readcheck`
- `expected_live_sha256 =` SHA256 of the DLL currently deployed to production

The job must:

- connect using the protected `production` Environment
- use explicit TLS when configured
- RETR exactly `/StackMeet.Api.dll`
- calculate SHA256
- require it to equal `expected_live_sha256`
- perform zero remote writes

Do not continue if the live hash is unexpected.

### 3. Manually stop the application pool

Use the hosting control panel to stop the NADITrack application pool.

Confirm the pool is stopped before starting DEPLOY.

The GitHub workflow does not stop or start IIS automatically.

### 4. DEPLOY — exact one-DLL write

Run from `master` with:

- `operation = deploy`
- `commit_sha =` exact approved master SHA
- `live_source_sha =` currently deployed source SHA
- `expected_build_sha256 =` fresh PRE-FLIGHT build hash
- `expected_live_sha256 =` live hash proven by FTP READCHECK
- `pool_confirmation = POOL-STOPPED`
- `remote_path_confirmation = /StackMeet.Api.dll`
- `deployment_confirmation = DEPLOY StackMeet.Api.dll`

Before the first write, the job must:

1. revalidate all source/interlock gates;
2. rebuild the exact approved source and reproduce `expected_build_sha256`;
3. RETR the live DLL as a temporary backup;
4. require the backup hash to equal `expected_live_sha256`;
5. re-query GitHub and prove `master` has not advanced.

The only intended write is the approved DLL upload.

After upload, the workflow must RETR the remote DLL and require its SHA256 to equal `expected_build_sha256`.

### 5. Rollback behavior

If any failure occurs after the write begins, the workflow attempts to upload the previously retrieved live backup and then RETR it again to verify the original hash.

If rollback is verified, the deployment run remains failed but the original production DLL is restored.

If rollback cannot be verified, the workflow reports:

- `ROLLBACK_ATTEMPTED=True`
- `ROLLBACK_VERIFIED=False`
- `PRODUCTION_STATE=UNKNOWN`
- `DO NOT START THE POOL`

In that state, do not start the application pool until the production DLL state is inspected and recovered manually.

### 6. Manually start the application pool

Only start the application pool after GitHub reports:

- `PRE_UPLOAD_BACKUP=PASS`
- `MASTER_RECHECK_BEFORE_WRITE=PASS`
- `DEPLOYMENT_BINARY_VERIFIED=PASS`
- `POST_UPLOAD_REMOTE_SHA256=` the approved build SHA256

Wait for the application to initialize before running VERIFY.

### 7. VERIFY — production read only

Run:

- `operation = verify`
- `verification_competition_id =` an appropriate live competition ID
- expected count fields: optional
- `enforce_expected_counts = false` for normal deployment verification

Hard gates:

- `/api/health` returns HTTP 200
- `/api/version` returns HTTP 200
- public competition results endpoint returns HTTP 200
- `settings` exists
- `settings.language` exists
- `translations` exists
- `results`, `stackers`, `doubles`, `relays` exist
- privacy deny-list scan passes

VERIFY automatically reports live counts and language in the job log and GitHub step summary.

If expected counts are supplied with `enforce_expected_counts = false`, mismatches produce warnings but do not turn an otherwise healthy deployment red.

Use `enforce_expected_counts = true` only when exact data counts are intentionally part of an acceptance test.

## After a successful deployment

Record the new production baseline:

- live source SHA = deployed `commit_sha`
- live DLL SHA256 = verified `POST_UPLOAD_REMOTE_SHA256`
- VERIFY result = PASS
- current observed counts = values reported by VERIFY

For the next deployment, use that live source SHA and live DLL hash as the starting production identity.

Do not assume the current `master` SHA equals the deployed live source SHA unless a deployment has just completed from that exact master commit.

## Production safety rule

A merge to `master` is **not** a deployment. Production changes happen only through an explicitly launched DEPLOY operation after PRE-FLIGHT, FTP READCHECK, manual pool stop, and all exact interlocks have been satisfied.
