# ENG-03G — Frontend integration completion candidate (draft)

Base: d4e5ef53bf6781dda52f32c350dd61009bbb4a52, ENG-03F corrected CI run [45618236518](https://github.com/casseylim/StackMeet/actions/runs/45618236518), all three jobs passed. ENG-03G hosted checks pending. This is not owner acceptance or production readiness.

| Package | Verified baseline / delivered boundary |
|---|---|
| ENG-03A | 203dd1c — ordered shell and action inventory |
| ENG-03B | fce0bdd — state projection and merge boundary |
| ENG-03C | 1dd0bb2 — navigation and six print actions, remove five accidental saves |
| ENG-03D | 71422c5 — nine participant/team helpers |
| ENG-03E0 | 3900b64 — owner Finals-only Overall/All-Around rule; separate Prelims intact |
| ENG-03E | 851044f — seven scoring-entry helpers |
| ENG-03F | d4e5ef5 — eight export/report/active print templates, corrected legacy static test |
| ENG-03G | this branch — cross-module composition and targeted public UI repairs |

Seven preceding packages have validated hosted CI. New test loads all first-party operator shell scripts in actual order, suppresses authenticated startup and excludes SignalR transport. Synthetic data exercises real providers' construction, locale preference, roster/scoring helpers, Finals-only calculation in both operator report paths, preliminary report availability, baseline revision projection, read-only print/export and navigation cancellation. It prohibits network I/O and verifies fixture state remains unchanged. Existing separate suites still cover async saves, public portal, qualification, browser-security static checks and API contracts. No claim of full user journey or actual browser verification.

Inventory: six HTML shells, 47 script references (43 at ENG-03A baseline), 66 delegated action names (60 app plus six module actions). app.js remains 6,908 lines including terminal empty line; down 132 lines from 7,040. This is boundary extraction, not a rewrite. DOM controllers, mutations, editor/save lifecycle, public rendering, sync and sport policy still need controlled follow-on work. Older unreferenced TimeSheetPrinting.js stays inactive.

## Targeted UI repair candidate

ENG-00 recorded English/Malay public header overflow at 390px and contrast/name issues. Allow header flex wrapping, constrain controls, move language control to its own row on small screens, darken section nav and not-started state text, restore visible keyboard focus. Use visible text for brand and section-card accessible names; hide decorative arrow from accessibility tree. Static checks protect these changes. No claim that all 18 contrast nodes or seven name violations are resolved. No axe/browser execution here. Public paths, scoring, API/data and certified snapshots unchanged.

## Release acceptance checklist (all browser/runtime items outstanding)

| Required check | Acceptance evidence |
|---|---|
| Operator and public 320 / 390 / 1280 CSS px × EN/MS/zh-Hans | screenshots and document scrollWidth <= viewport; connecting/live/offline labels, populated and empty data; no clipped controls |
| Accessibility | repeat ENG-00 axe WCAG A/AA; no new violations, close baseline contrast/name findings and manual incomplete item; screen-reader and zoom check |
| Keyboard scoring | actual focus, Enter, scratch, missing, stale revision review/cancel, dirty navigation and reconnect; no silent data loss |
| Registration and reporting | Individual/Doubles/Relay, legacy imports, Finals-only overall incl. faster Prelims, missing/scratch, no prelim fallback; certified historical snapshot digest unchanged |
| Print/export | A4 portrait and landscape populated sheets, QR/branding, three languages, print dialog cleanup, Excel-compatible .xls and CSV; spreadsheet formula interpretation risk separately assessed |
| Security/runtime | IIS CSP, revoked sessions, role scope, authentication, mixed traffic and reconnect, dependency/advisory residuals |
| Historical displays | compare old versus Finals-only displayed rankings in approved synthetic/historical fixtures; owner sign-off before publication |
| Rollback | approved file inventory, restore coherent prior assets and smoke test; preserve live configuration and historical data |

## Coordinated release inventory

Do not release an isolated app.js or index.html. Stage matching index.html/app.js and js/storage/Repository.js, js/NavigationActions.js, js/ParticipantTeams.js, js/results/ScoringEntry.js, js/reports/ReportPresentation.js; matching results/index.html/results.js/results.css and EN/MS/zh-Hans dictionary files. Other cumulative changes from ENG-01/02/06/08 remain subject to separate release manifest review; this list is a frontend dependency set, not a full deploy allowlist.

Mandatory owner requirement: production IIS web.config must remain byte-for-byte identical. Exclude from upload/sync/rollback inventory, no regeneration or deletion, secure backup and verify SHA-256 before and after. Stop if preservation cannot be guaranteed. No secrets in evidence. Owner-approved deployment allowlist, preflight, rollback rehearsal and explicit release approval are required. No master merge or production deployment. ENG-04 may start from validated draft candidate only after CI, with unresolved ENG-03 acceptance gates tracked.
