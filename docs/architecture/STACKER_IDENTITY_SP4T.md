# NADITrack Stacker Identity v1 — SP-4T Production Release Readiness

Status: implementation branch; no production deployment performed.

## Purpose

SP-4T reconciles the current protected-master application delta with the existing governed Production Release v2 mechanism.

It does not deploy anything. It makes the release manifest, workflow hard gates, runbook, and static CI agree on the exact application files that would be eligible for a later separately approved production release.

## Starting authority

SP-4T started from protected master:

`24288a3c2f23be8f79ae927e86a66fcf9ee70488`

The approved live source baseline remains:

`ba80538a912a3dd0f708fdd65a8823ed7988dbd6`

The production workflow still requires the exact protected-master SHA supplied at manual dispatch and rejects a moved master.

## Delta review finding

The original Production Release v2 manifest was prepared before PR #62.

PR #62 — **Harden team result to Stacker integrity** — later modified:

`backend/StackMeet.Api/wwwroot/app.js`

That browser-side change is part of the same integrity boundary as the server protections because it prevents operators from moving/changing team members with saved results and coordinates team-result deletion before team removal.

The governed Release v2 preflight compares every changed `wwwroot` path between the approved live source and the requested target master against the manifest allowlist.

Therefore, leaving `wwwroot/app.js` outside the manifest would correctly make current-master preflight fail closed.

SP-4T resolves that mismatch explicitly instead of weakening the preflight.

## Governed application fileset

Production Release v2 now contains exactly **11 application files**:

- the existing 10 reviewed Release v2 files;
- plus `backend/StackMeet.Api/wwwroot/app.js`.

The current distribution is:

- 6 files expected to already exist on production;
- 5 files expected to be new/absent on the approved live baseline.

`/StackMeet.Api.dll` remains deploy order 100 and therefore remains the final application file uploaded.

The two SP-4S browser files:

- `/wwwroot/profile/index.html`;
- `/wwwroot/profile/profile.js`;

were already part of the governed Release v2 manifest. Their later SP-4S content changes therefore do not require new remote paths, only the exact target-master hashes produced by preflight.

## Database scope unchanged

SP-4T adds no migration.

Release v2 remains restricted to exactly these three additive EF migrations:

1. `20260906033000_AddCompetitionActivityModuleCode`
2. `20260910103000_StackerIdentityPersistenceV1`
3. `20260914093000_FinalsRankingGovernanceSp4g`

The GitHub workflow still generates an idempotent migration package but never executes SQL against production.

## Workflow safety retained

The Release v2 workflow remains:

- `workflow_dispatch` only;
- protected by exact target-master SHA;
- bound to the approved live source SHA;
- protected by a read-only FTP fileset fingerprint;
- protected by expected current live DLL SHA-256;
- protected by exact preflight manifest SHA-256;
- protected by manual schema confirmation;
- protected by manual app-pool-stop confirmation;
- protected by exact path confirmation;
- protected by the final deployment phrase.

No `push`, `pull_request`, or scheduled trigger is added.

No production SQL execution is added.

No automatic IIS/app-pool operation is added.

## Configuration hard stop

The existing hard rules remain unchanged:

- `web.config` cannot enter the governed release delta;
- `appsettings*.json` cannot enter the governed release delta;
- application csproj changes invalidate Release v2;
- unexpected checked-in SQL invalidates Release v2.

The production `web.config` must never be overwritten.

## Verification enhancement

Post-start verification now also performs a read-only HTTP GET for:

`/app.js`

This confirms that the newly governed browser-integrity asset is reachable after a later release.

The verification job remains read-only.

## CI guards

SP-4T updates the existing Production Release v2 static guard and adds:

`tests/stacker-identity-v1-sp4t-production-release-readiness.test.js`

The SP-4T guard proves:

- release version is the reviewed 2026-09-24 revision;
- approved live source SHA is unchanged;
- exactly 11 application files are governed;
- `app.js`, the SP-4S profile assets, and the DLL are present;
- `app.js` is treated as an existing production file;
- the DLL remains last;
- exactly three migrations remain approved;
- release workflow remains manual-only;
- all deployment confirmation phrases remain present;
- no production SQL executor is introduced;
- configuration-file protections remain;
- runbook counts match the manifest/workflow.

## Deliberately not performed

SP-4T does not run:

- Release v2 `preflight`;
- Release v2 `ftp-readcheck`;
- Release v2 `deploy`;
- Release v2 `verify`;
- production database migration;
- production FTP write;
- production IIS/app-pool operation;
- production data write.

Any production operation remains a separate explicit approval step after SP-4T is merged and protected-master CI is green.
