# StackMeet Code Audit — 2026-07-31

## Scope

This audit covers the browser application, extracted JavaScript modules, ASP.NET Core API, public results portal, administration console, SQL assets, mirrored hosted files, and automated tests.

## Runtime module map

| Module | Purpose | Runtime status |
|---|---|---|
| `config.js` | Supplies branding and the default competition-key setting before application startup. | Loaded by `index.html`. |
| `js/auth/AuthSession.js` | Stores and validates the browser login session and creates authenticated API headers. | Loaded and used by API clients. |
| `js/storage/ApiProvider.js` | Loads and saves the complete competition state through the API, with a local-file fallback for approved test use. | Loaded and used by `Repository`. |
| `js/storage/Repository.js` | Provides the application-facing persistence boundary and selects the active competition provider. | Loaded and used by `app.js`. The reset/XML/validation methods are reserved interface placeholders and intentionally throw until migrated. |
| `js/storage/StackerApi.js` | Calls SQL-native competition and individual-stacker endpoints. | Loaded and used by `app.js`. |
| `js/results/BestResultEngine.js` | Classifies attempts as valid, scratch, invalid, or missing and supplies shared ranking values. | Loaded and used by entry and report code. |
| `js/reports/FinalsReportEngine.js` | Builds Preliminary and Finals projections, rankings, filters, qualification views, all-around results, and organization credits. | Loaded and used by the Competition Reports UI. |
| `app.js` | Legacy-compatible composition root for state, workflows, rendering, reports, XML, exports, and printing. | Primary browser runtime. |
| `admin.js` | Runs protected competition administration workflows. | Loaded by `admin.html`. |
| `results/results.js` | Runs the read-only public results portal and SignalR refresh. | Loaded by the public results page. |
| `backend/StackMeet.Api` | Hosts static files and implements login, competition state, administration, SQL-native stackers, public results, health, version, and SignalR endpoints. | Builds successfully on .NET 8. |

## Findings and action

### Corrected

- Removed seven private leaf helpers from `app.js` that had no caller: notification rendering, three superseded award helpers, the unused current-preset getter, and two superseded report-table wrappers.
- Kept the source and hosted `app.js` copies identical after cleanup.
- Restored the missing `.attempt-table` first-column width rule in hosted `styles.css`; source and hosted styles now match.
- Added concise module-purpose comments to the main application, branding, authentication, result, report, administration, and public-results JavaScript modules.
- Updated stale characterization expectations for relay editability, the repaired awards planner, and XML schema version 2.
- Updated business-rule memory that still described the old awards failure and XML version 1.

### Intentionally retained

- The older Competition Report builder functions remain in `app.js`. Their former controls are absent from `index.html`, but Sprint 11 documentation says the legacy builder is intentionally retained. Removing that subsystem requires an explicit product decision.
- The former Competition Report handlers are guarded by `legacyCompetitionReportUiEnabled = false`. Static regression coverage verifies that no current HTML control exposes the legacy UI and that its handlers remain disabled during the observation period.
- `Repository.reset`, `importXml`, `exportXml`, and `validate` remain unimplemented interface placeholders. Current runtime calls only `load`, `save`, and `setCompetitionKey`; XML still lives in `app.js`.
- `FinalsReportEngine` contains a small fallback copy of result classification logic. This is deliberate resilience when `BestResultEngine` is not loaded, although the normal page loads the shared engine first.
- Root frontend files are mirrored in `backend/StackMeet.Api/wwwroot` for deployment. These are deployment copies, not accidental duplication, and must remain byte-identical where documented.
- Entity Framework migration snapshots and designer files are generated artifacts and should not be manually deduplicated or annotated.

## Verification

- All 28 JavaScript files pass syntax checking.
- The .NET 8 Release build succeeds with zero warnings and zero errors.
- Storage smoke tests pass.
- The characterization suite passes all 26 scenarios.
- Preliminary save-pipeline tests pass.
- Competition report tests pass.
- Public results representative-data and static safety tests pass.
- Competition administration static safety tests pass.
- Source/hosted checks confirm synchronized application, HTML, configuration, auth, persistence, result, report, and style assets.

## Remaining risk

`app.js` is still a large composition root with hundreds of functions and several historical workflows. Broad extraction or deletion would carry high regression risk. Continue modularization behind characterization tests, one boundary at a time, following `docs/architecture/MIGRATION_PLAN.md`.
