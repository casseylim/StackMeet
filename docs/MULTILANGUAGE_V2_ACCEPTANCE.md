# Multilanguage v2 Phase 3F Acceptance

## Final status

Phase 3F acceptance is closed.

- `PHASE3F_STATIC_FRONTEND=CLOSED`
- `PHASE3F_PRESENTATION_HOTFIX=CLOSED`
- `HEADER_LANGUAGE_UI=CLOSED`
- `LOGIN-LOC-01=CLOSED`
- `OBS-AUTH-01=CLOSED`
- `BACKEND_BINARY_PREFLIGHT=READY`

This document records release-readiness evidence only. It does not authorize or perform a production deployment.

## Scope

Final acceptance and release-readiness review for the authenticated operator UI and the existing Public Results portal in English (`en`), Bahasa Malaysia (`ms`), and Simplified Chinese (`zh-Hans`). No new browser automation dependency or business-logic change is included.

Baseline:

- Starting `master` SHA: `642de4e1c60123a9415322c3148d779c6416796d`
- Phase 3F implementation head before this documentation correction: `39e33af1d42cab4df305932b75e462764765343f`
- Branch: `feature/multilanguage-v2-phase3f`
- Environment: Windows, PowerShell, Node.js, .NET 8 Release toolchain
- LocalDB: CI-authoritative integration-test environment

## Automated evidence

Required automated checks:

| Check | Result | Evidence |
| --- | --- | --- |
| Phase 3F acceptance guards | PASS | `tests/multilanguage-v2-phase3f.test.js` |
| Phase 3E, 3D, 3C, 3B, 2, 1 and coverage | PASS | Existing regression tests |
| Competition report engine / characterization | PASS | Existing regression tests |
| Complete JavaScript suite | PASS | All `tests/*.test.js` |
| JavaScript syntax checks | PASS | CI workflow |
| .NET 8 Release build | PASS | CI workflow |
| Core Integrity LocalDB integration tests | PASS | CI workflow |
| Storage smoke tests | PASS | CI workflow |
| Core Integrity static guards | PASS | CI workflow |

GitHub CI push run `33715535596` passed on Phase 3F implementation head `39e33af1d42cab4df305932b75e462764765343f`. The final pull-request head must also pass CI before merge.

## Runtime acceptance summary

Runtime acceptance covered language transitions English → Malay → Simplified Chinese → English, route navigation, dynamic rerenders, refresh persistence, and domain-data preservation across the authenticated operator interface and the Public Results portal.

Domain-value checks preserved participant names, organizations, competition and venue values, divisions, configured events, team values, IDs, dates, times, results, stored values, Doubles/Relay values, and awards data without indiscriminate translation.

### Batch 1 — Participants, Doubles and Relay

PASS.

The acceptance fixture was enriched with disposable participants and valid Doubles/Relay teams so both empty and populated states could be exercised. Participant, Doubles and Relay views were checked in EN/MS/zh-Hans, including language changes followed by rerenders and return to English. Captured domain values remained unchanged.

Representative baseline included:

- competition `PHASE3F_ACCEPTANCE`
- participant IDs `1.1`–`1.6`
- Doubles team `2.1`
- Relay team `3.1`

No participant or team domain mutation was observed.

### Batch 2 — Awards and Finals

PASS.

Awards and Finals runtime acceptance passed in EN/MS/zh-Hans. Places/Top rerender, Trophy/Medal presentation, Finals data-entry presentation, multilingual Results Report generation, language return, reload persistence, and domain-value preservation were exercised without unresolved placeholders.

### Batch 3 — Print Center and Reports

PASS.

The Print Center empty-preview localization defect was fixed and retested in EN/MS/zh-Hans. Individual Preliminary preview regeneration passed EN → MS → zh-Hans → EN with participant/domain values preserved.

The controlled Finals report comparison used the same populated report across language changes and preserved representative IDs, places, event groups and times. No unresolved placeholder tokens were observed.

### Batch 4 — Public Results and authentication presentation

PASS.

The local Public Results route was exercised unauthenticated with a populated API fixture. The baseline contained 36 results, 6 stackers, 1 Doubles team and 1 Relay team. Reserved disposable privacy-test email/phone values were absent from the public response.

The Public Results EN → MS → zh-Hans → EN round trip preserved IDs, ranks, times, names, events and divisions. Narrow presentation fixes covered selector preservation, translated status/type chrome, nested static translation behavior and safe organization/domain-value handling.

The invalid-login Malay/Simplified-Chinese localization defect was fixed at the presentation boundary while preserving canonical API errors internally. Post-fix login localization verification passed. Header language UI and final operator presentation checks passed. `LOGIN-LOC-01` and `OBS-AUTH-01` are closed.

## Defects and fixes

Phase 3F contains presentation/localization fixes only. It does not alter scoring, ranking, qualification, SQL ownership, SignalR semantics, Competition Package behavior, database schema, migrations, or domain models.

Closed observations include:

- Public Results selector/nested-control localization
- Public Results status/type and table-heading localization
- safe `Independent` presentation without translating arbitrary organization data
- Print Center empty-preview localization
- canonical login-error handling with UI-boundary translation
- authenticated browser-title localization
- authenticated header language-control presentation

## Backend binary preflight

The production deployment review separately established backend binary compatibility and an exact one-file deployment manifest for `StackMeet.Api.dll`.

This acceptance document does not change that deployment scope and does not install a deployment workflow.

## Release decision

`RELEASE READINESS: PASS`

Phase 3F application acceptance is closed and may be merged to `master` after the exact pull-request head passes CI.

Production deployment remains a separate controlled activity. A merge does not authorize deployment.

Permanent production rules remain:

- production `web.config` must never be overwritten, replaced, regenerated, deleted, copied from publish output, or deployed
- `appsettings.json` / `appsettings.*.json` remain outside the deployment scope
- no production database migration or write is part of the current deployment
- no `dotnet publish`
- no Web Deploy
- no generic FTP sync or mirror
- exact deployment manifest only
- manual myASP.NET application-pool stop/start only
- pre-upload RETR backup and live SHA256 verification
- post-upload RETR SHA256 verification
- verified rollback on any deployment failure
- post-start health/public-results validation is a separate manual verification step

`FinalsReportEngine.js` remains untouched. Certificate Phase 5C remains paused pending a Syncfusion license.
