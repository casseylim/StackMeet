# Competition Admin Phase 1 Report

Date: 2026-07-13
Workspace: `C:\CodeX\StackMeet`

## Completed

- Added additive EF model support for `Competition.CompetitionKey`, `Competition.PasswordHash`, archive metadata, and `CompetitionState.CreatedAt`.
- Generated safe additive EF migration: `20260713034108_CompetitionAdminPhase1`.
- Generated SQL review script: `outputs/migrations/CompetitionAdminPhase1.sql`.
- Added admin-only API under `/api/admin/competitions`.
- Added separate admin authorization using `X-StackMeet-Admin-Key`.
- Replaced shared web login password with competition-specific password hashes.
- Kept `X-StackMeet-Api-Key` support for controlled server maintenance.
- Added bearer-token competition isolation for `/api/state/{competitionKey}` and `/api/competitions/{id}/...` routes.
- Added standalone admin interface: `/admin.html`.
- Added static safety test and runtime smoke-test script.

## Data Preservation

No live database migration was applied.

The migration does not drop, truncate, delete, or recreate existing tables/data in `Up()`.

Existing data handling in migration:

- `CompetitionState.CreatedAt` is backfilled from existing `CompetitionState.UpdatedAt`.
- `Competition.CompetitionKey` is backfilled from existing `Competition.CompetitionCode`.
- Existing `DEFAULT` `CompetitionState.JsonData` is not modified.
- Existing `Stacker` rows are not modified or deleted.

## Runtime Secrets

Set outside Git/source control:

```powershell
$env:Security__AdminKey = "<strong-admin-key>"
$env:Security__SessionSigningKey = "<long-random-signing-key>"
```

Optional maintenance key remains server-side only:

```powershell
$env:Security__ApiKey = "<strong-maintenance-api-key>"
```

## Validation

Passed:

- `dotnet build StackMeet.sln -c Release --no-restore`
- `node --check admin.js`
- `node --check app.js`
- `node --check js/auth/AuthSession.js`
- `node --check js/storage/ApiProvider.js`
- `node --check js/storage/Repository.js`
- `node --check js/storage/StackerApi.js`
- `node js/storage/storage-smoke.test.js`
- `node tests/competition-admin-phase1.static.test.js`
- `node tests/characterization.test.js` with 26 scenarios

Not run locally because live DB/runtime secrets were not provided in this turn:

- `tests/competition-admin-runtime-smoke.ps1`
- Live SQL backup
- Live migration application
- Live post-migration data comparison

## Migration Review

Review file before approval:

```text
outputs/migrations/CompetitionAdminPhase1.sql
```

Expected additive operations:

- Add `CompetitionState.CreatedAt`
- Add `Competition.ArchivedAt`
- Add `Competition.ArchivedBy`
- Add `Competition.CompetitionKey`
- Backfill `Competition.CompetitionKey`
- Alter `Competition.CompetitionKey` to non-null
- Add `Competition.PasswordHash`
- Create unique index on `Competition.CompetitionKey`

Important: do not apply this SQL to live until the online database is backed up and reviewed.

## Rollback

Before live deployment:

1. Back up SQL database.
2. Back up current published app files.
3. Apply reviewed migration SQL manually only after approval.
4. Deploy app files.
5. Verify admin login, competition login, and `DEFAULT` state read.

Rollback path:

1. Restore previous app files.
2. Restore SQL database backup if migration was applied and must be reverted.
3. Do not use EF `Down()` against live without a backup review because it removes the newly added admin columns.

## Important Risks

- Existing competitions need passwords set through admin API before competition-user login can succeed.
- `DEFAULT` remains readable only as a compatibility `CompetitionState`; it is not automatically converted into a new `Competition` master record.
- Runtime DB isolation tests still require a configured local/live SQL instance and admin key.
- `dbo.Stacker` currently remains competition-linked by `CompetitionId`; no new junction table was added in Phase 1.
## Pre-Migration Review Checklist

Date: 2026-07-13

Reviewed files:

- `backend/StackMeet.Api/Migrations/20260713034108_CompetitionAdminPhase1.cs`
- `outputs/migrations/CompetitionAdminPhase1.sql`
- `backend/StackMeet.Api/Migrations/StackMeetDbContextModelSnapshot.cs`

Findings:

- No destructive operations in migration `Up()` such as `DROP TABLE`, `DROP COLUMN`, `TRUNCATE`, or `DELETE`.
- Migration is additive: adds `CreatedAt`, archive metadata, `CompetitionKey`, `PasswordHash`, and a unique index.
- Existing `CompetitionState.CreatedAt` is backfilled from `UpdatedAt`.
- Existing `Competition.CompetitionKey` is backfilled from `UPPER(CompetitionCode)`.
- `PasswordHash` is nullable, so existing competitions survive migration but require admin password setup before competition-user login.
- `DEFAULT` `CompetitionState.JsonData` is not modified by the migration.

Material risks to check before applying:

- Duplicate existing `CompetitionCode` values would become duplicate `CompetitionKey` values and fail the unique index.
- Blank/null existing `CompetitionCode` values would fail when `CompetitionKey` becomes non-null.
- Existing `CompetitionState` rows without a matching `CompetitionCode` are compatibility orphans; `DEFAULT` may be one unless a `DEFAULT` master record exists.
- Any existing `CompetitionCode` over 100 characters would fail the new `CompetitionKey` length.

Read-only SQL gate:

- Run `PRE_MIGRATION_CHECK.sql` against the target DB before applying `outputs/migrations/CompetitionAdminPhase1.sql`.
- All failure-count checks should return `0`, except the password setup count is informational.
- Confirm row counts for `Competition`, `CompetitionState`, and `Stacker` before backup/migration.

Do not apply migration until:

1. `PRE_MIGRATION_CHECK.sql` is reviewed.
2. Online database backup is completed.
3. SQL review script is approved.
4. Admin key and session signing key are configured outside Git.
