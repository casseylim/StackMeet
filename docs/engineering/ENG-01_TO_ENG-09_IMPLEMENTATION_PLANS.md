# ENG-01 through ENG-09 implementation plans

Proposed sequence for owner review. No sprint is approved by this audit. Start ENG-06's evidence collection and ENG-08's advisory triage alongside ENG-01. Resolve the scoring interpretation with the competition owner immediately. Keep release recovery on a separate governance track. Implement changes on individual review branches, one behavior boundary at a time. Each PR must attach before/after synthetic evidence and a rollback rehearsal.

| Order | Workstream | Accountable reviewer | Entry dependency |
|---|---|---|---|
| 1 | ENG-01 integrity | Database and competition leads | Baseline rule fixtures and legacy client inventory |
| 1 in parallel | ENG-06 test enforcement | QA and release engineering | ENG-00 suite ledger |
| 1 in parallel | ENG-08 advisory triage | Security and release engineering | Resolved package graph and advisory list |
| 2 | ENG-02 security | Security and privacy owners | Publication/access decisions, ENG-06 negative-test harness |
| 3 | ENG-03 frontend | Frontend and QA leads | Stable save intent and browser fixtures |
| 4 | ENG-04 storage authority | Database and operations leads | ENG-01 transaction contract and recovery evidence |
| 5 | ENG-05 synchronization | Client and API leads | Revision/authority contracts from ENG-01 and ENG-04 |
| 6 | ENG-07 public portal | Portal, privacy and competition leads | Publication and invalidation rules, load baseline |
| 7 | ENG-09 activity policy | Activity and competition leads | Approved rule vectors, stable contracts and ENG-06 parity gate |

### ENG-01 — Protect committed results and state

Trace existing clients that omit ExpectedRevision before tightening contracts. Define create/update/delete/retry outcomes, including deleted-and-recreated keys. Keep CompetitionResultsController.cs:40's transaction and the logical index at StackMeetDbContext.cs:208. Move lifecycle and participant membership checks to a consistent lock order shared by all mutators. Bind authoritative audit identity to authenticated context. Add two-connection LocalDB tests for simultaneous upserts, stale deletion, whole-batch rollback, closure and participant removal during save.

**Exit.** Every case yields one legal serial outcome, documented conflict detail and a stable revision. Legacy saved competitions open and certified snapshots match fixture hashes. **Rollback.** Retain the previous client contract during compatibility rollout. Revert code through a reviewed PR. Any irreversible schema work needs a separate decision.

### ENG-02 — Define access, publication and session boundaries

Enumerate the 71 controller operations plus hub membership, generated routes and static assets. Annotate each with actor, scope, lifecycle and expected publication state. Have the privacy owner distinguish unlisted, link-accessible, archived and explicitly public profiles. Add declarative or shared policy checks in small route families. Cover public payload fields, account session revocation, bounded hash parsing and maintenance attribution. Inventory stored browser credentials, upload metadata retention and security headers before proposing any transport change.

**Exit.** Synthetic anonymous, viewer, data-entry, manager, system-admin, legacy and maintenance matrices cover same/cross-competition cases. Every sensitive field has an explicit publication test. Existing authorized public profiles and anonymous results still render. **Rollback.** Review one route family at a time. Revert policy changes as a unit with their call sites. Keep emergency governance intact.

### ENG-03 — Extract browser responsibilities and repair verified accessibility defects

Begin at the delegated action handler in wwwroot/app.js:4774. Characterize route selection, dirty-state retention, save scheduling, focus and localization. Extract one action family, then repeat for registration, score entry and reporting. Introduce explicit inputs/outputs at existing provider boundaries. Keep language keys, storage formats, public URLs and print templates stable. Address the header reflow, contrast and label/name defects in dedicated UI commits.

**Exit.** All characterization cases and three-locale browser flows pass. At 320, 390 and 1,280 CSS pixels, status changes keep controls reachable and produce no page-level overflow. Keyboard-only scoring and printing preserve edited values. Re-run axe and manually close incomplete checks. **Rollback.** Keep existing render and provider entry points during extraction. Each commit must be reversible independently.

### ENG-04 — Define storage authority and recovery units

Map each field in participant, team, result, qualification, identity, asset and publication aggregates to its authoritative store, legacy fallback and backup representation. Document SQL precedence in PublicResultsController.cs:85 and JSON membership checks in the state controller. Measure query plans and lock holding time on representative team graphs. Shorten stale-request work only after proving it cannot change validation outcomes. Design a read-only reconciliation report before considering any approved migration.

**Exit.** Legacy JSON-only and SQL-backed fixtures project identically where contracts require parity. No mutation path writes conflicting authorities. Restore rehearsals verify SQL rows, XML, asset bytes, protected settings and snapshot digests against a single recovery point. **Rollback.** Keep legacy readers until reconciliation passes. Future schema proposals need forward/reverse compatibility and separate governance.

### ENG-05 — Recover reliably from retries and disconnects

Document baseline revision, client pending edits, last applied event and authoritative refresh transitions. Exercise the provider save queue and the public client's reconnect handler at results.js:1722. Rejoin subscriptions before resynchronizing. Define when to replay idempotent intent, prompt an operator or discard an obsolete notification. Preserve unsaved drafts across authentication renewal and conflict resolution.

**Exit.** Two-client tests cover duplicate, delayed and missing messages, dropped response after commit, reconnect after delete, failed refresh and close-during-edit. Client/server converge on identical saved results and the UI identifies each remaining unsaved value. **Rollback.** Keep full GET and periodic refresh available. Disable incremental processing independently.

### ENG-06 — Make regression evidence repeatable

Use the suite ledger as the initial manifest. Compare discovery against all console test projects, top-level JS tests, storage smoke and offline PowerShell cases. Preserve existing explicitly governed CI and release gates. Isolate build directory from running binaries, archive exit status per suite, and collect API line/branch coverage separately from canonical browser URLs and VM harnesses. Add Edge scenarios for public and authenticated workflows, print layouts, locales and accessibility.

**Exit.** A missing suite or failed case fails CI. Reports identify source SHA, runner/runtime, fixture, numerator and denominator. Establish thresholds only after stabilizing measurements. A broken rule fixture, publication leak fixture or stale-write fixture must fail its designated gate. **Rollback.** Run new reporting beside existing CI until equivalent discovery is demonstrated. Revert collector changes without hiding test failures.

### ENG-07 — Reduce public refresh cost

Start with the five-query read path at PublicResultsController.cs:19–90. Profile response shaping, participant lookup and browser rendering separately. Compare current whole response against version-based conditional GET and bounded deltas, including deletion, unpublication, archive and profile withdrawal. Any cache key must capture all publication-affecting inputs. Keep the full-refresh fallback and use the same six synthetic sizes to evaluate each alternative.

**Exit.** Demonstrate lower bytes and p95 on matched repeat runs, bounded working set and zero projection mismatches. Cached and uncached paths must honor withdrawal immediately according to the agreed policy. Record query and lock metrics with full operator mix before capacity claims. **Rollback.** Turn off caching/deltas and return to authoritative full GET. Invalidate cached public views on reversal.

### ENG-08 — Modernize dependencies under release governance

Use the dependency graph to identify each direct parent of the five affected packages. Identify design-time/runtime use, hosting runtime overrides and vendor SignalR provenance. Resolve high-severity matches promptly with the smallest supported patch set. Separate the application package update from compiler/SDK changes. Test actual runtime servicing, package resolution, SQL provider behavior and the pinned release reproducibility environment independently.

**Exit.** Attach before/after advisory scan and package graph. Every outstanding advisory has a recorded exposure decision, owner and deadline. All suites pass, controlled offline builds reproduce their reviewed artifacts, and existing five-file approvals remain bound to their original source. **Rollback.** Revert package graph and runtime changes together as appropriate. A new binary requires its own governed release evidence.

### ENG-09 — Isolate sport policy behind the activity contract

Inventory scoring, age/division, team membership, qualification, ties, awards and printing used by both public and operator clients. Resolve RULE-1 with the rules owner before unifying helpers. Separate policy version and immutable certified evidence. Use ActivityModuleRegistry.cs:9's compatibility default for older competitions and retain rejection of unknown codes. Exercise a second synthetic module with only its declared capabilities.

**Exit.** Owner-approved individual/doubles/relay examples produce the same expected result across UI, API, reports and public projections. Legacy IDs, public profile contracts and certified snapshot hashes remain stable. The second module cannot fall through to stacking-specific scoring. **Rollback.** Route stacking back to its previous versioned implementation while preserving all historical assignments and evidence.
