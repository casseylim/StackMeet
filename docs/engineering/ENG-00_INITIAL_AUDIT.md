# ENG-00 — Engineering baseline, first-pass audit
Date: 2026-11-24
Baseline: a3042efca304feab8e77f87ff20fc24675461e9c
Status: PARTIAL — static inspection and historical CI verification; not an accepted full audit.

## Inventory
511 tracked blobs, 151 C# files, 114 JS files, 163 Markdown files, 26 csproj files, 144 files under tests/. Counts include generated migrations, tests and vendor files. app.js: 319191 bytes, 6732 split lines, 467 function declarations by regex, 70 lines containing innerHTML; results.js: 81878 bytes, 1822 split lines, 75 function declarations, zero lines containing innerHTML. These counts are not complexity or coverage measurements.

## Architecture and positive controls
ASP.NET Core net8.0, EF Core SQL Server 8.0.8, SignalR, browser JS, IIS/PowerShell releases. Core application retains JSON state alongside SQL-native participant/results records. State replacement uses revision preconditions and serialized transactions; results use bounded batches and logical uniqueness. DbContext has unique competition key, user email, participant and logical result indexes. ActivityModuleRegistry provides an activity extension point; sport-specific rules remain in CompetitionResultRules. Preserve existing ranking certification, historical projection, identity links, public publication contracts, languages and release governance. Prefer modular monolith and incremental extraction, not a rewrite.

## Test and deployment evidence
Historical CI on exact baseline: https://github.com/casseylim/StackMeet/actions/runs/37910866912 — both build/test (42 steps) and offline DLL reproducibility (13 steps) successful. Reproducibility job also checks out a separately pinned release source; do not confuse that with baseline binary verification.
Career Profile Admin UI release: https://github.com/casseylim/StackMeet/actions/runs/37912223752 — preflight failed at exact source tests, five-file package and read-only FTP baseline; deploy skipped. Root cause not verified. Do not rerun deployment or weaken preflight. Review logs in authorized environment with secrets redacted.
No new tests, coverage, benchmark, dependency vulnerability scan, browser or SQL load tests executed in this audit. No production access.

## Improvement register
Paths relative to backend/StackMeet.Api unless specified. Priorities are proposed, not verified exploitability ratings. Security details should be reviewed privately before public disclosure.

| ID | Priority | Evidence | Impact and action | Acceptance |
|---|---|---|---|---|
| ENG-01-A | P0 | Controllers/CompetitionResultsController.cs:89,116 | Strengthen update/delete concurrency contract; distinguish create from update and reject stale operations. Review client compatibility first. | Two clients editing same result cannot silently overwrite; stale delete/recreate rejected; retry semantics defined; batch atomicity preserved. |
| ENG-01-B | P1 | Controllers/CompetitionStateController.cs:88,92 | Review actor attribution and lifecycle validation within transaction; derive authoritative actor from authenticated identity, preserve maintenance audit semantics. | Actor attribution cannot be overridden by untrusted input; concurrent closure and save covered by SQL integration tests. |
| ENG-02-A | P0 | Program.cs; Hubs/ResultsHub.cs | Review access boundaries for all endpoints and realtime subscriptions. Preserve published anonymous access; migrate toward explicit authorization policies. | Permission matrix covers private/public, legacy, manager, data entry, viewer, admin and maintenance access. |
| ENG-02-B | P1 | Services/PasswordHashService.cs; wwwroot/js/auth/AuthSession.js | Review password hash upgrade, validation bounds, session protection and revocation; evaluate CSP and cookie/CSRF design. | Existing credentials remain valid; malformed hash tests; logout/revocation and browser security tests pass. |
| ENG-03-A | P1 | wwwroot/app.js | Extract cohesive UI, state, registration, scoring orchestration, synchronization and reporting modules behind existing contracts. Review dynamic HTML escaping; no confirmed XSS claim. | Characterization tests and browser workflows unchanged; smaller modules with explicit dependencies. |
| ENG-04-A | P1 | Controllers/CompetitionStateController.cs; wwwroot/js/storage/ApiProvider.js; Data/StackMeetDbContext.cs | Measure full-state payload, repeated parsing and lock durations; plan SQL authority by aggregate. | No dual-write ambiguity, preserved XML backup/restore, measured query and lock baseline, migration plan separately approved. |
| ENG-05-A | P1 | Controllers/CompetitionResultsController.cs; wwwroot/js/storage/ApiProvider.js | Validate notification recovery, retry and state conflict UX. | Disconnect/reconnect, missed notification, concurrent edit, failed write and recovery tests. |
| ENG-06-A | P1 | .github/workflows/ci.yml; tests/ | Consolidate suite discovery and reporting; add coverage and actual browser tests alongside static checks. | All existing suites run, machine-readable reports and coverage archived. |
| ENG-07-A | P2 | Controllers/PublicResultsController.cs:37,66,90 | Benchmark full result projections and public portal refresh before introducing cache/delta changes. | Measured p50/p95 latency, SQL query count, payload and browser render time; publication semantics unchanged. |
| ENG-08-A | P1 | StackMeet.Api.csproj | Verify dependency advisories and framework support; plan supported patch/framework upgrades with release tooling compatibility. | Restore/build/test and reproducibility checks pass; no unresolved critical advisories. |
| ENG-09-A | P1 | Services/CompetitionResultRules.cs; Activities/ActivityModuleRegistry.cs | Move sport-specific policy behind activity contracts only after characterization. | Stacking results identical; non-stacking module cannot inadvertently use stacking rules. |
| REL-01 | P0 release gate | Actions run 37912223752 | Investigate failed preflight without production mutation. | Root cause documented and approved preflight passes before any release. |

## Remaining ENG-00 work and acceptance
- Complete per-file source inventory and dependency graph; run analyzers for complexity, duplication, unused code, nullability; do not delete merely because a symbol appears unused.
- Review all endpoints, upload/storage, public identity privacy, secret history and dependency advisories privately; verify negative authorization tests.
- Execute existing suites on isolated Windows/SQL LocalDB and browser E2E environment; capture commit, runtime, commands, results and coverage. Do not claim passing based solely on static inspection.
- Validate business rules using approved fixtures: individual/doubles/relay, qualification, ties, awards, historical certified finals, career profiles, multilingual and printing; reconcile server/client results.
- Benchmark synthetic competitions of 100/3, 300/5, 500/10, 1000/20, 3000/50, 5000/100 participants/operators. Record p95, error rates, correctness, memory, locks, request sizes, reconnection. No production data or load.
- Review UI accessibility, responsive layout, keyboard navigation, translations and print output.
- Verify backup/restore and rollback in disposable environment; no live production credentials or access.
- Publish detailed baseline, security review (private as needed), performance report, regression report, dependency map, risk register and approved roadmap. Mark unmeasured values unknown.
- Review findings with owner. ENG-00 remains open until evidence is complete or limitations explicitly accepted.

## Sprint sequence
ENG-01 data integrity; ENG-02 security; ENG-03 frontend modularity; ENG-04 database authority/performance; ENG-05 sync recovery; ENG-06 test consolidation; ENG-07 public portal performance; ENG-08 dependency modernization; ENG-09 multi-activity. Each sprint requires separate branch, evidence, tests, rollback plan and owner approval. No production deployment, schema migration, master merge or certified historical recalculation authorized by this audit.
