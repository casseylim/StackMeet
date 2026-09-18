# NADITrack Production Release v2 Runbook

Status: release mechanism preparation only. No production database or application write is performed by committing this runbook or workflow.

## Release objective

Promote the protected-master identity/profile and governed Finals work from the currently deployed production baseline to the reviewed master build without bypassing the existing production safety model.

Approved live source baseline:

- ba80538a912a3dd0f708fdd65a8823ed7988dbd6
- known live DLL SHA256 from the last controlled deployment: 443531BC17E24251D6E059C99B2AF79BF324C253F2980800F2AFDC8271165D2E

The target commit is always the exact protected-master SHA supplied to the workflow at dispatch time. The workflow rejects a moved master.

## Release footprint

Database changes: exactly three additive EF migrations:

1. 20260906033000_AddCompetitionActivityModuleCode
2. 20260910103000_StackerIdentityPersistenceV1
3. 20260914093000_FinalsRankingGovernanceSp4g

Application deployment: exactly ten files defined by production-release-v2-manifest.json:

- StackMeet.Api.dll
- nine reviewed wwwroot assets required by modular Finals, career profiles, and Results-to-profile linking

Excluded from this release: web.config, appsettings files, application csproj changes, ad-hoc SQL files, package/dependency rollout, automatic database migration, automatic IIS/app-pool control.

## Safety model

The Release v2 GitHub workflow is manual-only and shares the same naditrack-production concurrency group as the existing production workflow.

The GitHub workflow may generate an idempotent EF migration package, read production files over FTP, deploy the exact reviewed application manifest, and perform read-only HTTP verification. It does not execute SQL against production.

The deploy operation cannot begin unless all of these exact gates are supplied:

- MIGRATIONS-APPLIED-AND-VERIFIED
- POOL-STOPPED
- RELEASE-V2-PATHS-CONFIRMED
- DEPLOY RELEASE V2
- the exact preflight manifest SHA256
- the exact live fileset SHA256 from the immediately preceding FTP readcheck
- the expected current live DLL SHA256

## Step 1 - Run Release v2 preflight

Workflow: NADITrack Production Release v2

Operation: preflight

Inputs:

- commit_sha = exact current protected-master SHA
- live_source_sha = ba80538a912a3dd0f708fdd65a8823ed7988dbd6

Expected output:

- MASTER_EXACT_HEAD_GATE=PASS
- RELEASE_DELTA_ALLOWLIST=PASS
- RELEASE_FILE_COUNT=10
- MIGRATION_COUNT=3
- RELEASE_MANIFEST_SHA256=<record this>
- MIGRATION_PACKAGE_SHA256=<record this>
- TARGET_DLL_SHA256=<record this>
- PRODUCTION_WRITES=0

Download and retain the production-release-v2-preflight artifact. It contains:

- migrations.idempotent.sql
- release-manifest.resolved.json

Do not proceed if the preflight source SHA, manifest hash, migration count, or file count is unexpected.

## Step 2 - Back up and migrate the production database

Before executing the generated migration package, create and verify a production database backup/snapshot using the hosting provider's supported database backup mechanism.

Apply only the exact migrations.idempotent.sql from the preflight artifact whose SHA256 matches MIGRATION_PACKAGE_SHA256. Because the script is idempotent, EF migration history determines which migration blocks execute.

After execution, verify the schema with read-only SQL. Example checks:

```sql
SELECT [MigrationId]
FROM [dbo].[__EFMigrationsHistory]
WHERE [MigrationId] IN (
  '20260906033000_AddCompetitionActivityModuleCode',
  '20260910103000_StackerIdentityPersistenceV1',
  '20260914093000_FinalsRankingGovernanceSp4g'
);

SELECT COL_LENGTH('dbo.Competition', 'ActivityModuleCode') AS ActivityModuleCodeColumn;
SELECT OBJECT_ID('dbo.SportStackerIdentity', 'U') AS SportStackerIdentityTable;
SELECT OBJECT_ID('dbo.StackerIdentityLink', 'U') AS StackerIdentityLinkTable;
SELECT OBJECT_ID('dbo.FinalsRankingGovernance', 'U') AS FinalsRankingGovernanceTable;
SELECT OBJECT_ID('dbo.TR_FinalsRankingGovernance_ImmutableSnapshot', 'TR') AS GovernanceImmutableTrigger;
```

All three migration IDs and all five schema objects above must be present before application deployment. Record that evidence, then use the schema confirmation phrase only if the checks pass.

Do not run Down migrations as an emergency application rollback. These migrations are additive and are intended to remain forward-compatible with the old application binary while the application release is completed.

## Step 3 - Establish the live FTP fileset fingerprint

Operation: ftp-readcheck

Inputs:

- commit_sha = the same exact protected-master SHA used for preflight
- live_source_sha = ba80538a912a3dd0f708fdd65a8823ed7988dbd6
- expected_live_dll_sha256 = 443531BC17E24251D6E059C99B2AF79BF324C253F2980800F2AFDC8271165D2E

This operation performs no remote writes. It retrieves the five expected existing files, confirms the five new release files are absent, and emits LIVE_FILESET_SHA256.

Record LIVE_FILESET_SHA256. The deploy operation recomputes the entire fileset before its first write and aborts if any file changed.

## Step 4 - Stop the application pool manually

Use the hosting control panel to stop the production application pool/site. Confirm the site is stopped before entering POOL-STOPPED.

The workflow never stops or starts IIS automatically.

## Step 5 - Deploy the exact application release

Operation: deploy

Supply:

- commit_sha = exact protected-master SHA
- live_source_sha = approved live baseline SHA
- expected_preflight_manifest_sha256 = RELEASE_MANIFEST_SHA256 from Step 1
- expected_live_fileset_sha256 = LIVE_FILESET_SHA256 from Step 3
- expected_live_dll_sha256 = known live DLL hash
- schema_confirmation = MIGRATIONS-APPLIED-AND-VERIFIED
- pool_confirmation = POOL-STOPPED
- path_confirmation = RELEASE-V2-PATHS-CONFIRMED
- deployment_confirmation = DEPLOY RELEASE V2

The deploy job rebuilds the exact target source, reproduces the preflight manifest hash, re-reads and fingerprints all live files, backs up every existing target file, rechecks master, then uploads and verifies each manifest file. The DLL is deployed last.

If a write fails, existing files are restored from verified backups and newly introduced release files are removed. If rollback cannot be verified, the workflow emits PRODUCTION_STATE=UNKNOWN and DO NOT START THE POOL.

## Step 6 - Start the application pool manually

Start the production application pool/site only when the deploy job reports:

- RELEASE_V2_APPLICATION_DEPLOYMENT=PASS
- POST_UPLOAD_FILE_COUNT=10
- POOL_STATE=STOPPED-MANUAL-START-REQUIRED

## Step 7 - Run post-start verification

Operation: verify

Provide a known public competition ID. Expected counts are optional; use enforce_expected_counts only when fresh, independently recorded production counts are available.

The verification gate checks:

- /api/health
- /api/version
- public results API shape and privacy
- public profile-links contract
- Release v2 static assets
- optional results/stackers/doubles/relays count drift

Expected hard gates:

- POST_START_HTTP_VALIDATION=PASS
- RELEASE_V2_STATIC_ASSETS=PASS
- PUBLIC_PROFILE_LINK_CONTRACT=PASS
- PRIVACY_CHECK=PASS
- PRODUCTION_WRITES=0

## Step 8 - Identity data activation

The schema migration creates the permanent identity tables but intentionally does not auto-create identities or links from historical participant names.

Career profile links become visible only after reviewed SportStackerIdentity records and reviewed StackerIdentityLink mappings exist with IsPublicProfile enabled. No automatic name, WSSA ID, DOB, gender, club, or demographic matching should be introduced during production activation.

Finals career placements additionally require eligible governed-v2 immutable Finals snapshots. Legacy competitions without certified evidence remain without permanent placement claims.

## Closure

After successful verification, record the new production source SHA, deployed DLL SHA256, resolved manifest SHA256, migration package SHA256, live fileset evidence, workflow run IDs, and verification results in the production deployment record. Only then should the old ba80538 baseline be considered superseded.
