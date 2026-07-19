# SQL-Native Core Phase 1B Registration Design

## Scope

Only Individual Stackers are SQL-native. The selected SQL competition is `CompetitionId=7` (`STACKMEET-P1B-2026`). The browser takes an explicit `?competitionId=7` URL parameter first, then uses session state for the same browser session. If no valid selection exists and exactly one SQL Competition exists, it is selected; otherwise the Stackers screen presents the minimal SQL Competition Setup form.

## Transitional split

| Authority | Data |
| --- | --- |
| SQL-native API | Competition and Individual Stackers |
| CompetitionState JSON | Doubles, Relays, Results, Finals, Awards, Settings, XML, and reports not yet migrated |

`saveState()` now clones the legacy state and removes `stackers` before POSTing `/api/state/DEFAULT`. On startup, any legacy `stackers` array is discarded and replaced from `GET /api/competitions/{competitionId}/stackers`. There is no writable JSON stacker collection.

The runtime maps the SQL `StackerCode` to the existing UI `id`. Division and age remain calculated in the frontend against the existing competition start date. To preserve the established form without a second writable copy, the Phase 1B migration adds SQL fields for region, email, phone, custom division, paid, and checked-in status.

## Client behavior

`js/storage/StackerApi.js` uses only same-origin relative API URLs. It supports Competition list/create for selection and the five required Stacker endpoints. The Stackers screen refreshes when opened and includes a manual Refresh button. Refresh does not run over an open edit form.

Add, edit, delete, and CSV import await server confirmation. The sidebar shows Saving, Saved, or Save Failed. CSV rows are posted one by one; conflict responses are counted as skipped and no legacy teams/results are cleared.

## Rollback

1. Restore the previous IIS package or replace `wwwroot/app.js`, `wwwroot/index.html`, and `wwwroot/js/storage/StackerApi.js` with their pre-Phase 1B versions.
2. The added SQL columns are additive and do not affect CompetitionState. Roll back `AddStackerRegistrationFields` only after the frontend rollback and only if no registration data in those columns is needed.
3. `CompetitionState` remains independently operational throughout.
