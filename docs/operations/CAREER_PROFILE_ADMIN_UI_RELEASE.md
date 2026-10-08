# Career Profile Admin UI: controlled five-file release

This runbook prepares a release; it grants no production-write or profile-activation authority. The historical `deploy-career-profile-remediation.yml` and `Invoke-CareerProfileDllRelease.ps1` remain unchanged. Never use them for this package.

## Reviewed application source and exact scope

Application source: `6182e405ced08c24cf5f8738f1172c8df5197d3e`.
The authoritative five-file checksum/size/repository-path/destination manifest is `scripts/deployment/career-profile-admin-ui.approved.json`. Destinations are the existing FTP site root: `/admin.html`, `/admin.js`, `/admin.css`, `/career-admin.js`, `/StackMeet.Api.dll`. Only the last entry is binary/backend. The others are static/frontend. The DLL's repository path is a build output, not a tracked binary.

The complete backend delta from previously deployed `1e2c0211aa00d39d55e444c42565c4111592fe78` is the reviewed identity name-quality helper, persistence guards, two admin controllers, authorization middleware, and the four listed frontend assets. There are no csproj, migration, runtime/dependency, environment/configuration or other wwwroot changes in that delta.

No web.config, appsettings JSON, secrets, database, uploads, migrations, test binaries, PDBs, source maps or unrelated static files may enter the payload. Rollback artifacts contain only prior versions of the governed files plus hash-only metadata. Protected configuration contents are temporarily read for hashing and deleted from scratch, never uploaded or retained in artifacts.

## Source control and provenance gates

Push the feature and release-tooling branch, obtain review/CI and merge into protected master BEFORE dispatching. The reviewed feature SHA must remain reachable from master; do not squash away that exact SHA. If the project requires squash merge, review and explicitly rebind the source/manifest through a separate change instead of bypassing the gate.

Dispatch `.github/workflows/deploy-career-profile-admin-ui.yml` from master. Both operations check GitHub's authoritative repository, protected branch, exact source existence, source-to-master ancestry and current master equals the executing workflow SHA. Deploy also requires a completed successful workflow_dispatch run of this same workflow, a successful (not skipped) preflight job, identical workflow head and matching manifest run ID. Any master change after preflight requires a new preflight.

## Preflight: read-only production access

Use operation `preflight`; leave every deploy field at defaults. The workflow uses production environment approvals and the established PROD_FTP_* secrets/variables. Its `naditrack-production` concurrency lock matches the historical production workflows, with cancel-in-progress false. Do not manually run other FTP deployment tools concurrently.

The script verifies the exact source checkout, archives ONLY tracked source into the reviewed deterministic SourceLink-relative directory, restores and runs all 24 .NET suites, all 84 JavaScript suites and storage smoke, and performs the final Release CI build using SDK 10.0.400 and explicit SourceRevisionId. New offline release-tooling tests also run in the workflow and general CI. Test databases are disposable LocalDB; no production database secret is supplied.

The generated five file hashes/sizes must equal the approved manifest, including DLL `2D9981445CDBA4196B70622112C909FD062949682787B74B92D52F0C622AF5CA`. A package with different hashes must fail; do not edit the allow-list or suppress the check to make it pass. The deterministic archive path matters for reproducing the reviewed DLL's PDB/SourceLink checksum.

Preflight successfully lists the FTP root, downloads all existing governed targets, records missing-new-file state, downloads/hashes all root appsettings JSON variants and web.config, and checks the currently approved live DLL hash `9CD4BFC317E185711AF1BFF0155A3210B1A82E526F5F8B53891ECC4E16F65924`. Failed RETR is an error, never evidence that a file is absent. Ambiguous filename case/duplicates abort. Only career-admin.js may be absent.

It publishes `career-admin-ui-preflight-<run_id>` with EXACT root members: `payload/` (five), `baseline/` (four or five existing targets), `manifest.json`, `rollback-manifest.json`. The manifest binds source, workflow, run, all target hashes and the rollback-manifest hash. Config hashes live in rollback metadata; config contents never enter the artifact.

Record the NEW `RELEASE_MANIFEST_SHA256` emitted by that successful run. The approved policy manifest and prior locally prepared package manifest are NOT substitutes for this new preflight hash/run ID. This engineering task does not dispatch preflight or capture a fresh FTP baseline.

## Deploy: separate production approval required

Do not deploy during release engineering. Once separately approved, confirm the hosting control is exclusively NADITrack Site On/Off. Stop NADITrack only and manually confirm it no longer serves requests. The shared pool must remain running. Never stop/recycle/restart the shared pool or another site. Do not create app_offline or change hosting-wide settings.

Dispatch from the same master with:

- operation: deploy
- preflight_run_id: successful NEW preflight run
- expected_manifest_sha256: exact hash printed by that preflight
- site_confirmation: SITE-STOPPED-NADITRACK-ONLY
- deployment_confirmation: DEPLOY-CAREER-PROFILE-ADMIN-UI

Deploy never rebuilds. It downloads the named same-repository artifact, checks exact file lists and checksums, downloads a fresh complete backup, verifies each backup hash against preflight, and rechecks live/config/master immediately before the first write. Manual site confirmation is an operator attestation; the script has no hosting or shared-pool control.

While the site stays stopped, static files upload in manifest order and the DLL last. Each upload is downloaded and verified, then the whole release and protected configuration hashes are checked. Wait for BOTH `CAREER_PROFILE_APPLICATION_DEPLOYMENT=PASS` and `SITE_STATE=STOPPED-MANUAL-START-REQUIRED` before manually starting NADITrack only. Keep the site stopped on any error or ambiguous job/runner outcome.

## Whole-set rollback

The artifact records all five logical prior file states. Typically four physical backups exist because career-admin.js was absent. An existing career-admin.js is instead backed up as a fifth file. Never fabricate a backup for a nonexistent file.

A write attempt is tracked before STOR, including partial failures. On any upload/hash/whole-set verification failure the script attempts restoration of EVERY existing target, static first and DLL last, and removes only career-admin.js when the baseline proves it was absent. This single deletion is allow-listed rollback of a newly introduced release file; it cannot delete any other file. Every restoration is attempted even if another fails. The full previous file set, presence states and protected config hashes must then match. A successful automatic rollback still fails the deploy job and requires operator review; do not treat it as deployment success. An unverified rollback is CRITICAL: keep the site stopped.

The deploy run retains `career-admin-ui-rollback-<run_id>` even on failure. The successful preflight's identical-hash baseline is also retained in case of runner loss. If startup/smoke fails after a successful transfer, stop NADITrack only and obtain a controlled restore of the complete prior set from those backups. Do not rerun deploy, restore a subset, troubleshoot through business-data changes, or use shared-pool operations.

Read-only rollback verification is available on a runner with the exact preflight bundle, matching GitHub context and approved FTP environment:

`Invoke-NadiTrackApplicationRelease.ps1 -Operation VerifyRollback -PackageDirectory <bundle> -PreflightRunId <run> -ExpectedManifestSha256 <hash>`

This operation only downloads/hashes files. It does NOT restore files. There is deliberately no unattended post-start rollback/hosting controller in this workflow.

## Post-start read-only verification

After NADITrack-only manual restart, record HTTP status, functional contract and source version. Perform GET requests only; never submit a Career Profile mutation, account change, migration or password reset.

| Read-only check | Required result |
|---|---|
| `/` | 200, normal home page |
| `/admin.html` and normal login page | 200, normal rendering, no new authentication failure |
| `/admin.js`, `/admin.css`, `/career-admin.js` | 200; hashes match payload, account for cache-busting query strings |
| `/?competitionId=18` | Existing competition view works; status remains Active |
| `/api/public/competitions/TEST/results` | 200, normal existing results; use public CompetitionCode TEST, not numeric 18 |
| `/api/public/stackers/malformed` | Privacy-safe 404, never 500 |
| `/api/public/stackers/NDT-ZZZZZZZ` | Unknown ID: privacy-safe 404, never 500 |
| `/Stackers/malformed` | 404 |
| `/Stackers/NDT-ZZZZZZZ` | Valid-ID public shell may return 200 by design; its API must return 404 without private data |
| `/api/public/competitions/TEST/profile-links` | 200 and zero links |
| Anonymous `/api/admin/career-profile/capabilities` | 401/403 |
| Same capabilities GET using owner's existing System Admin browser session | 200, seven boolean authority fields; no token/cookie capture |

Owner inspects the authenticated Career Profile section without clicking mutation controls: bounded stacker search/select, current link/identity, create and link controls, private/public state, publish/unpublish/unlink, NadiTrackId and profile URL display. Controls must obey capabilities and private defaults. Where no identity is selected or names are unusable, mutation controls must remain disabled. No public URL should be invented for a private/unlinked identity. Use the owner's normal existing authenticated session; do not request or copy credentials/tokens.

If separately authorized read-only DB checks are available, compare identity/link/public counts (expected zero), Stacker 62 unchanged/unlinked, competition 18 Active, and result 378/379/380 row fingerprints against the prior audit. Do not mutate data to achieve an expected count. Report any mismatch rather than correcting it.

There is no eligible finalized-competition activation candidate. Do not create identities, link Stacker 62, correct its stored last name '-', publish/unpublish/unlink, finalize Competition 18, or change results 378–380. Deployment readiness does not imply activation readiness.
