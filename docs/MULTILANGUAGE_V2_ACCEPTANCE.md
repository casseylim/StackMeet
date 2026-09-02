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

Manual/runtime result: HOLD / PARTIAL. The local Development application started successfully at `http://localhost:48804` (HTTPS profile also available at `https://localhost:48803`) using the isolated `NADITrack_Phase3F_Acceptance` LocalDB database and existing migrations only. Health/version checks passed (`/api/health`, `/api/version`). A disposable competition and separate CompetitionManager/SystemAdmin accounts were created through the supported admin API; credentials and signing material are intentionally not recorded here. Login runtime smoke covered EN → MS → zh-Hans → EN language selection: `NADITrack Login`, `Log Masuk NADITrack`, and `NADITrack 登录` rendered with matching translated chrome and titles. The authenticated CompetitionManager session opened the disposable competition and rendered Dashboard and Settings in English, Malay, and Simplified Chinese; competition name, venue, dates, and configured values remained unchanged domain data. Direct hash navigation to several other operator sections did not activate those sections in this session, and disposable participant/result creation, Public Results runtime routes, privacy before/after snapshots, dynamic refresh persistence, and the complete route matrix therefore remain unexecuted. Static tests are not represented as browser E2E evidence.

The Release build was independently run from a normal user PowerShell session using isolated temporary NuGet, packages, and artifacts paths and passed for the solution, `StackMeet.Api`, and `CoreIntegrityIntegrationTests`. The earlier Codex-only access-denied errors are recorded as execution-environment restrictions; no repository workaround was made.

### Batch 1 runtime evidence

The recovered local-only handoff was used with the existing fixture; no competition, account, or database was recreated. The app was reached at the local HTTPS endpoint and the disposable CompetitionManager session was reopened. Participants, Doubles, and Relay views were exercised through the authenticated UI in all three supported languages, including language changes followed by view/tab rerenders. Relay member search and Member 1–4 / Optional Member 5–6 controls rendered in Malay and Chinese; the fixture contained no team records, so team-domain values and edit-existing-team states were not executable. The zh-Hans → English return and reload restored English operator chrome.

Batch 1 results: Participants EN PASS (list/search/edit data path not executable because the fixture has zero participants); Participants MS PASS (localized controls and empty/list presentation; no participant data to compare); Participants zh-Hans PASS (localized controls and empty/list presentation; no participant data to compare). Doubles EN/MS/zh-Hans PASS for available empty/tab controls; existing-team edit/type/status rerender NOT EXECUTABLE because the fixture has zero teams. Doubles direct tab-rerender PASS for the available empty-state path. Relay EN/MS/zh-Hans PASS for available empty/member-control presentation; existing-team edit/status/domain-member rerender NOT EXECUTABLE because the fixture has zero relay teams. Relay member-control rerender PASS. Language return/persistence PASS for zh-Hans → English and reload. No localization or domain mutation defect was observed in this batch. A complete domain BEFORE/AFTER comparison is NOT EXECUTABLE for participant/team/result fields absent from the minimal fixture; competition name, venue, dates, and configured values remained unchanged.

### Batch 1B fixture enrichment

Using the recovered runtime and normal operator UI, six disposable participants were created: `Phase Alpha One`, `Phase Bravo Two`, `Phase Charlie Three`, `Phase Delta Four`, `Phase Echo Five`, and `Phase Foxtrot Six`. The set covers both genders and mixed Paid/Checked-In values; one record contains the reserved fake privacy-test contact values. IDs `1.1`–`1.6`, names, Malaysia country, generated `Open` division, and stored values were visible in the participant list. A valid disposable Doubles team was created from participants `1.1` and `1.2`, displayed as team `2.1` with unchanged member values, normal type, complete status, and `14U` division. Relay member controls were populated and a valid persisted relay team was then created through the normal UI: team `3.1`, `Phase Relay Alpha`, members `1.3`–`1.6`, `Open`/`Open`, status `Ready`, and private location presentation. Existing-team Doubles and Relay views rendered in EN/MS/zh-Hans, including member/search controls and rerender checks. A complete participant/team domain before/after comparison remains PARTIAL because the original pre-enrichment snapshot did not contain the later-created records; no mutation was observed in the captured values. Batch 1B/1C: HOLD / PARTIAL pending full edit-mode and before/after evidence.
Batch 1D established the post-enrichment baseline: competition `PHASE3F_ACCEPTANCE`, name `Phase 3F Multilanguage Acceptance`, venue `Phase 3F Acceptance Lab`; participant `1.1` `Phase Alpha One`, Malaysia, `Open`, M, Special `No`, Paid `Yes`, Checked-In `Yes`; Doubles `2.1`, members `1.1`/`1.2`, Normal/Complete/`14U`; Relay `3.1`, `Phase Relay Alpha`, members `1.3`–`1.6`, Ready, Timed Relay `Open`, Head-to-Head `Open`. The user completed the remaining participant edit/validation/reopen sequence manually in EN/MS/zh-Hans and reported PASS for each language, EN → MS → zh-Hans → EN return, reload persistence, and no participant/domain mutation. Accordingly, BATCH 1 = PASS.

### Batch 2 status

The user completed the remaining Awards and Finals runtime acceptance. Awards EN/MS/zh-Hans passed, including Places/Top rerender, Trophy/Medal rerender, zh-Hans → English reload persistence, unchanged award values, and no unresolved placeholders. Finals Final Sheet/data entry and the multilingual Results Report passed in EN/MS/zh-Hans. BATCH 2 = PASS. Batch 3 Print Center and Reports acceptance remains pending.

### Batch 3 runtime status

Batch 3 was started against the existing populated fixture. Print Center controls and an individual preliminary-sheet preview were generated in Malay; the generated preview localized headings, attempts, instructions, judge/table labels, IDs, divisions, names, and domain values. The same preview was regenerated in Simplified Chinese with no unresolved placeholder tokens and no stale English output observed. Reports were opened with populated results and Malay report controls/rendered headers; a complete EN/MS/zh-Hans regeneration and before/after report comparison was not completed. A visible Print Center empty-preview message remained English (`Preview` / `Choose a print item to generate a printable preview.`) while no item was selected; this is recorded as a presentation localization defect requiring follow-up. Batch 3 = HOLD.
The narrow empty-preview fix was retested on the rebuilt runtime: EN, MS, and zh-Hans all displayed their explicit localized heading/message pairs. The Individual Preliminary preview was regenerated EN → MS → zh-Hans → EN; names/IDs and divisions remained present, and no `{placeholder}` tokens were observed. Reports were regenerated in the three languages with populated domain rows visible, but exact result/time and row-count before/after comparison remains incomplete. Batch 3 remains HOLD pending the complete report integrity matrix.
The controlled report comparison then captured the same populated Finals report in EN/MS/zh-Hans/EN: stage Finals, division Open, event groups 3-3-3, 3-6-3, and Cycle, returned rows 24, first IDs `1.1`, `1.2`, `1.3`, first places `1`, `2`, `3`, and first 3-3-3 times `7.123s`, `7.200s`, `7.300s`. These IDs, places, times, and domain rows were unchanged across the regenerated language runs, with no placeholder tokens observed. The print comparison retained participant/domain values and no placeholders in the generated preview. Batch 3 remains HOLD because the full reports filter/preset and print five-field comparison has not been independently completed.
The final Individual Preliminary print comparison used participant `1.1` and was regenerated EN → MS → zh-Hans → EN. Participant ID, `Phase Alpha One`, `Open`, `Independent / Malaysia`, and events `3-3-3`, `3-6-3`, `Cycle` were present and unchanged in all four states; no placeholder tokens were observed. Print five-field comparison: PASS. Reports representative integrity: PASS. BATCH 3 = PASS.

### Batch 4 status

The local public Results route was opened unauthenticated at `/PHASE3F_ACCEPTANCE/Results`. The populated API baseline contained 36 results, 6 stackers, 1 doubles team, and 1 relay team; the disposable participant email and phone were absent. The public Preliminary EN → MS → zh-Hans → EN round-trip preserved IDs, ranks, times, names, events, and divisions. Public Results defects were discovered and narrow presentation fixes were applied, including selector preservation, status/type display, and safe domain-value handling. The invalid-login MS/zh-Hans defect was reproduced and the AuthSession canonical-error fix was applied, but post-fix online runtime verification remains pending. Login language/chrome switching and Admin multilingual smoke passed; the final operator title/domain smoke remains pending. OBS-AUTH-01 remains unresolved. BATCH 4 = HOLD / PARTIAL.

## Defects and fixes

The Phase 3F scope contains presentation-only fixes for the observed Public Results and invalid-login localization defects. Post-fix runtime verification is still pending. Business-logic, SQL, SignalR, package, ranking, scoring, qualification, or domain-model defects were not changed.

## Release decision

RELEASE READINESS: HOLD. Automated acceptance is green, but post-fix online AuthFix verification, the final operator title/domain smoke, and OBS-AUTH-01 disposition remain pending. The known localization defects are documented above; Phase 3F is not closed.

`web.config` remains untouched. `FinalsReportEngine.js` remains untouched. No deployment, publish, PR, merge, or certificate work is authorized by this acceptance document.
