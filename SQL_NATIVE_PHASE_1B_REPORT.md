# SQL-Native Core Phase 1B Report

## Modified files

- `app.js`: SQL competition selection, SQL-authoritative runtime stackers, async CRUD, refresh, and CSV routing.
- `index.html`: minimal SQL competition setup and Refresh controls; loads `StackerApi.js`.
- `js/storage/StackerApi.js`: dedicated same-origin API client.
- `backend/StackMeet.Api/Models/Stacker.cs`, DTOs, DbContext, and controller: registration-form persistence fields.
- `backend/StackMeet.Api/Migrations/20260711175415_AddStackerRegistrationFields.cs`: additive Phase 1B schema migration.
- `tests/characterization.test.js` and `tests/phase-1a-api.ps1`: updated client loading and registration-field verification.
- `backend/StackMeet.Api/wwwroot`: synchronized frontend; `backend/StackMeet.Api/publish`: refreshed IIS package.

## Transitional architecture

`CompetitionId=7` (`STACKMEET-P1B-2026`) is the selected SQL-native registration competition. Individual Stackers load and mutate through `/api/competitions/7/stackers`. `CompetitionState` remains the store for the non-migrated modules. Its save payload explicitly excludes stackers, preventing a legacy save from overwriting or reintroducing them.

## Import behavior

Existing CSV parsing is retained. Each valid row is sent sequentially to SQL; the UI reports imported, skipped (duplicate StackerCode), and failed totals. Import no longer replaces the stacker list or clears Doubles/Results.

## Known limitation and deployment gate

The IIS package is built and hash-verified, but the workspace has no production IIS hostname or deployment authority. Hosted two-browser validation therefore remains outstanding. Do not mark Phase 1B complete until that test passes on the deployed package.
