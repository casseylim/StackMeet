# Multilanguage v2 Phase 3F Acceptance

## Scope

Final acceptance and release-readiness review for the authenticated operator UI and the existing Public Results portal in English (`en`), Bahasa Malaysia (`ms`), and Simplified Chinese (`zh-Hans`). No new browser automation dependency or business-logic change is included.

## Baseline and environment

- Starting SHA: `642de4e1c60123a9415322c3148d779c6416796d`
- Branch: `feature/multilanguage-v2-phase3f`
- Environment: Windows, PowerShell, Node.js bundled runtime, .NET Release toolchain
- LocalDB: CI-authoritative unless a local preflight is available

## Automated evidence

The Phase 3F regression test verifies exact locale-key parity, nonblank values, placeholder parity, language preference hooks, route/render localization hooks, Phase 3E dynamic-render guards, domain-data protection, Public Results assets, and discoverability of the Phase 3B–3E regression tests.

Required automated checks:

| Check | Result | Evidence |
| --- | --- | --- |
| Phase 3F acceptance guards | PASS | `tests/multilanguage-v2-phase3f.test.js` |
| Phase 3E, 3D, 3C, 3B, 2, 1, coverage | PASS | Existing regression tests |
| Competition report engine / characterization | PASS | Existing regression tests |
| Complete JavaScript suite | PASS | All `tests/*.test.js` |
| JavaScript syntax sweep | PASS | Source JavaScript files |
| .NET Release build | PASS | `dotnet build StackMeet.sln -c Release` |
| `git diff --check` | PASS | Working-tree diff |

## Manual/runtime acceptance matrix

Manual/runtime evidence must cover language transitions English → Malay → Chinese → English, route navigation, dynamic rerenders, refresh persistence, and absence of unexpected mixed-language chrome across Dashboard, Settings, Participants, Doubles, Relay, Preliminary, Finals, Awards, Print Center, Reports, Public Results, and Phase 3B login/account/admin screens.

Domain snapshots must confirm unchanged participant names, organizations, competition and venue values, divisions, configured events, team values, IDs, dates, times, results, stored Yes/No values, Doubles/Relay type/status values, and Trophy/Medal values. Public Results must be checked for protected PII exposure. Certificate generation remains paused.

Manual/runtime result: PENDING — requires interactive operator acceptance evidence; static tests must not be represented as browser E2E evidence. No browser-level E2E claim is made here.

## Defects and fixes

No defects have been accepted or fixed under Phase 3F at this checkpoint. Business-logic, SQL, authentication, SignalR, package, ranking, scoring, qualification, or domain-model defects must be reported separately and are out of Phase 3F scope.

## Release decision

RELEASE READINESS: PENDING until the manual/runtime matrix is completed. Automated acceptance is green; target conclusion remains: Remaining untranslated authenticated UI: NONE; Critical multilingual defects: NONE; Domain-data mutation caused by language switching: NONE.

`web.config` remains untouched. `FinalsReportEngine.js` remains untouched. No deployment, publish, PR, merge, or certificate work is authorized by this acceptance document.
