# ENG-03A — Frontend architecture and characterization (draft)

Baseline: 86245335b4186a465f3a8735d26dee2899695cba (ENG-02G CI-passed candidate). No application, API, data, schema, route, certificate or release changes.

## Current architecture

At baseline app.js contains 7040 lines (including terminal empty line), 332607 JavaScript string code units and 481 line-start named function declarations (not an AST complexity metric). The main delegated click handler recognizes 66 distinct explicit action comparisons. Six HTML shells load 43 script references, including repeated shared libraries. The existing classic script globals and their ordering are part of the compatibility contract.

| Boundary / current implementation | Dependencies and ownership | Proposed extraction |
|---|---|---|
| Bootstrap / initializeApplication (end of app.js) | AuthSession.requireLogin, Repository, SQL roster/results, i18n, render, SignalR | Keep composition root; no business-rule changes |
| State normalization / persistence (approx. 347–746) | Legacy JSON, ETag, deleted records, audit, save queue, repository | ENG-03B explicit state access and lifecycle seam; preserve existing merge/revision behavior |
| SQL roster/results and editor state (747–1150) | StackerApi, ResultApi, immutable editor baseline, result revision, blocked uncertain writes | Keep separate from full-state storage; scoring extraction only after behavior parity |
| Sync (1168–1371) | timers, SignalR, current competition epoch, active editor, polling fallback | Defer retry/reconnect semantic changes to ENG-05 |
| Navigation/render (1399–1650) and delegated click (5070 onward) | route, reportTab, dirty editor guard, focus, activity capabilities, localization, save/render flags | ENG-03C action-family extraction, retain delegated entry point |
| Participant and team UI (1995–2319; mutation helpers later in file) | SQL roster, legacy teams, shared membership, divisions, edit fields | ENG-03D isolate individual/doubles/relay adapters without changing authority |
| Prelims/finals (2748–3512) | ResultApi, BestResultEngine, baseline and confirmation flow, team eligibility, print | ENG-03E isolate editor controllers; do not alter rule semantics |
| Reporting / printing / export (3513–3856 and later) | FinalsReportEngine, report presets, DOM, locale, certificates, print state | ENG-03F extract read-only actions and printing seams, preserve markup |
| Independent pages | admin.js + career-admin.js, account.js, results/results.js + profile-links.js, profile/profile.js, all/all.js | Keep independent shell contracts; shared utilities only after parity |

## Existing dependency edges

- index.html loads config, i18n core/locales/preference/localization, AuthSession, storage adapters (ApiProvider before Repository), BestResultEngine before FinalsReportEngine, CompetitionPackage before CompetitionPackageAssembler, SignalR before app.js.
- admin.html loads localization, career-admin.js then admin.js; account.html loads localization before account.js.
- Public results loads its locale dictionaries, local SignalR vendor, results renderer then profile-links decorator. Public career profile and public directory each load their own renderer.
- js/printing/TimeSheetPrinting.js exists but is not loaded by any reviewed shell; it contains overlapping print implementation. Do not activate it or delete it until references and release contracts are audited.
- Final scoring, public result projection and career reporting have separate rule/version contracts. No consolidation of scoring or historical evidence before ENG-09 owner decisions.

## Characterization and first extraction gate

The [architecture matrix](../../tests/frontend-architecture-matrix.json) records six shell load orders and the 66 delegated action names. The new Node test checks script existence/order/loading mode and evaluates the actual delegated handler source in an isolated VM using stubbed dependencies. Thirteen scenarios characterize route acceptance/cancellation, report tab selection, scoring save scheduling, read-only exports/reporting, direct print and asset upload. This is not DOM/browser behavior verification. Two legacy print/preview actions still schedule full-state saves; preserve that observation in baseline, then fix separately after confirming no intended side effect. Other legacy actions with default save behavior need review; unknown action behavior is not changed here.

## Plan and acceptance

ENG-03B begins with small state-access boundaries, not a rewrite or framework migration. Subsequent packages isolate one responsibility at a time with explicit inputs/outputs and compatibility facades; each extraction updates the reviewed inventory deliberately. Keep public URLs, storage formats, translation keys, report layout and release artifact inventory stable. Re-run all existing suites plus VM tests. Before owner acceptance, test actual browsers at 320/390/1280 CSS pixels, EN/MS/zh-Hans, keyboard-only scoring, draft retention, focus, accessibility/contrast, print and download, reconnect and CSP. No production acceptance is implied by Node characterization; historical rules and outstanding ENG-02 risks remain.

Rollback: revert the individual draft commit; no deployed code or data has changed.
