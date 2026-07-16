# SQL-Native Core Phase 1A Test Results

Date: 2026-07-12

## Database migration and schema

`20260711174057_AddCompetitionAndStacker` was generated and applied successfully. `InitialCreate` was not edited. SQL Server verification returned these tables:

- `dbo.CompetitionState`
- `dbo.Competition`
- `dbo.Stacker`
- `dbo.__EFMigrationsHistory`

Migration history contains `20260711145521_InitialCreate` and `20260711174057_AddCompetitionAndStacker`.

## Repeatable API test

`tests/phase-1a-api.ps1` starts with unique codes, verifies responses, and deletes all records it creates. Against the configured SQL Server it passed all 24 checks:

- Competition: create (201), list (200), get (200), unknown get (404), invalid request (400), update (204), duplicate-code rejection (409), delete (204).
- Stacker: create (201), list (200), get (200), unknown Stacker (404), update (204), create/update duplicate-code rejection (409), same code in another Competition (201), delete (204), and unknown Competition rejection (404).
- Relationship guard: deleting a Competition containing Stackers returns 409; all Stackers must be deleted first.
- UTC: Competition and Stacker create responses both returned `createdAt` and `updatedAt` with a `Z` UTC suffix.

## Regression verification

| Command | Result |
| --- | --- |
| `dotnet restore StackMeet.sln --configfile NuGet.Config` | Passed |
| `dotnet build StackMeet.sln -c Release --no-restore` | Passed, 0 warnings and 0 errors |
| JavaScript syntax checks (`app.js` and all `js/**/*.js`) | Passed |
| `node js/storage/storage-smoke.test.js` | Passed |
| `node tests/characterization.test.js` | Passed: 17 scenarios |

No frontend runtime files were changed.
