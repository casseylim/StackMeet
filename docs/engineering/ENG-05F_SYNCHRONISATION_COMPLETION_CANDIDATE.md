# ENG-05F — Synchronization completion candidate

Scope: development and synthetic regression completion, not operational sign-off. No production connection, master merge or deployment. Hosted CI for this candidate is pending.

## Validated package chain

All three jobs (Build and test, Regression evidence and API coverage, Offline DLL reproducibility analysis) passed for the exact heads below. Each PR is a stacked draft, not a deployed release.

| Package | Draft PR | Validated head | CI run |
|---|---|---|---|
| ENG-05A baseline | #107 | 0eb1a7b2c15127026c87574070611a463c0366c1 | [45964721024](https://github.com/casseylim/StackMeet/actions/runs/45964721024) |
| ENG-05B state draft protection | #108 | c83656cfc3ddfef70ed8f0a083519862d58af8ef | [45982916639](https://github.com/casseylim/StackMeet/actions/runs/45982916639) |
| ENG-05C operator synchronization | #109 | c9ed17f9046f38c30bde869041a3540dedd06b72 | [46013136663](https://github.com/casseylim/StackMeet/actions/runs/46013136663) |
| ENG-05D public synchronization | #110 | d3d1488f9bf34b799193538a65ee7d22b333d7db | [46017180928](https://github.com/casseylim/StackMeet/actions/runs/46017180928) |
| ENG-05E integrated faults | #111 | ddfb4ce5f41483dee79394bfc612bb73f24e2b80 | [46097033691](https://github.com/casseylim/StackMeet/actions/runs/46097033691) |

Latest base build job 140830675969 explicitly logged all ENG05A/B/C/D/E positive recovery checks, including ENG05E state lost response, queue cancellation, synthetic token replacement, draft disposition; two operators and public convergence; close guard. CoreIntegrity LocalDB integration harness also passed. Repeated ENG04E synthetic recovery: 7,065,600 byte backup; 11 competitions, 560 stackers, 549 results; backup/verification 565.546 ms, restore/verification 1272.740 ms. No production RPO/RTO or capacity inference.

Initial A failure was a branding-signature assertion; B failures were missing locale coverage and extraction without async declaration; C failure was unnecessary context access on no pending work. Corrected workflows passed; no test skipped. D/E passed on their initial candidate.

## Revision and recovery transitions

| Boundary | Accepted baseline | Pending intent / failure | Recovery |
|---|---|---|---|
| JSON state | Provider accepted ETag; application state snapshot | Queue generation, tombstones, local draft, review-required flag | Observe without rebasing; successful POST acknowledges captured generation; uncertain/stale/authorization failure blocks automatic write; explicit review or confirmed manual retry |
| SQL results | Per-row expected revision and result-list revision; editor baseline | Draft inputs, review-required flag, blocked ResultApi cache after unknown or committed-refresh-failed outcome | GET/review, compare saved and draft values, explicit acceptance; stale writes rejected; no blind retry |
| Operator notifications | Applied state watermark and SQL result revision | Separate pending state/result revisions; editor/epoch protection | Rejoin authorized subscription before independent reads; failed read remains pending; bounded coordinator and 3-second fallback on active routes |
| Public projection | Successful full payload plus timestamp/branding signature | Coalesced pending read, join generation, request deadline | Rejoin before read; compare full projection including deletion; failure clears stale data; 15-second fallback; 10-second request deadline |
| Browser exit | Initial scoring input snapshot | Changed/saving/review-required editor or queued/failed state | Best-effort browser warning; no automatic submission or persistent draft storage |

## Integration completion additions

Extend ENG-05E shared-authority scenario with a new successful result write and deliberately suppress all notifications. Negative controls prove both A and public remain stale before any recovery. Drive the same read-only paths used by fallback polling; require identical results across both operators, viewer and authority, and exactly one additional POST (the explicit write). Require close warning during final editor save, dirty final input, and queued state; require no warning for clean final input and clean state. These are new pending assertions, not verified yet. No application/runtime, schema, release configuration or policy changes in ENG-05F.

## Exit assessment against original ENG-05 plan

| Exit requirement | Evidence / disposition |
|---|---|
| Duplicate, delayed and missing messages | Duplicate/delayed and coalescing verified in A/C/D; F missing-event parity negative controls pending CI; actual scheduler/SignalR transport remains untested |
| Response dropped after commit | State and result barriers verified in A/B/E, exact synthetic POST counts and stale conflict; no blind replay |
| Reconnect after delete | A late deleted snapshot guard, C rejoin and independent SQL refresh, D same-timestamp deletion and rejoin, E shared-authority deletion parity; real combined transport reconnect/delete pending |
| Failed refresh | B failed review preserves local state; C pending revisions; D unavailable results clear; E failed result review and recovery verified |
| Close during edit | E dirty/review state warning verified in synthetic listener; F final/saving/clean controls pending; real browser close/logout unverified |
| Client/server converge on identical saved results | E deterministic two operator/one viewer equality verified; real SQL-backed browser convergence pending |
| UI identifies each remaining unsaved value | Existing scoring review builds saved/draft row list; state review is conservative accept/discard, not field-level comparison; full DOM workflow and field-level state review remain incomplete |
| Preserve drafts across authentication renewal | E in-place synthetic token replacement only; real AuthSession reauthentication and logout/reload draft continuity not established; durable drafts not implemented |
| Rollback: full GET and periodic refresh | Full GET and periodic fallback retained; incremental result-write processing not independently switchable today, so original rollback exit remains partial; reviewed code rollback required |

Do not mark the original ENG-05 operational exit complete solely because the candidate CI passes.

## Carry-forward owner gates (proposed roles)

- Client/product owner: real two-operator and public browser tests with controlled disconnect, dropped response after commit, reconnect after delete, missed notification, expiry/relogin, dirty scoring/state, close/logout, and explicit review. Verify draft value visibility and account/competition isolation. Agree durable recovery design separately; no sensitive persistent draft storage by default.
- Security/privacy owner: verify current account/session revocation and publication contract. Unlisted public HTTP links remain allowed where existing API permits; archived/missing endpoint fails closed after verification. Review public outage visibility tradeoff, actual authorization and withdrawal; no stronger revocation promise.
- Engineering/performance owner: measure independent operator GET rate, full public projection serialization, memory, p95 and actual browser workload in ENG-07; establish bounded recovery and safe fallback. Earlier pinned-release identity transaction contention remains unresolved despite successful rerun; retain evidence and investigate.
- QA: ENG-03 browser sizes 320/390/1280, keyboard, accessibility, English/Bahasa Malaysia/Simplified Chinese, print/Excel and historical Finals-only presentation. ENG-06 to improve repeatable suite inventory and evidence without weakening existing gates.
- Infrastructure/data/release owner: ENG-04 operational authority reconciliation and complete coordinated restore remain open, including legacy roster ambiguity, XML/full release/runtime recovery, real backup/key custody/RPO/RTO; ENG-00 FTP root/admin.html preflight remains unresolved. No release until exact artifact inventory, isolated recovery, verified release and rollback plan, and explicit owner acceptance.

## Non-negotiable deployment safeguard

Production IIS web.config must NEVER be overwritten, deleted, or replaced by a package, synchronization, deployment, or rollback. Securely back up original, capture SHA-256 before deployment, exclude every web.config from relevant production upload/rollback manifests and destructive synchronization, verify unchanged production bytes via identical SHA-256 after deployment and rollback. Stop before release if safe exclusion or checksum verification cannot be established; halt on mismatch. This is a documented mandatory release gate, not proof of operational execution. Overall/All-Around use Finals only; preliminary reporting separate. Historical certified bytes and digests immutable. No master merge or production deployment without explicit owner approval.

## Next: ENG-06

After ENG-05F CI review and owner authorization, begin suite discovery and evidence baseline against existing suite ledger, console test projects, top-level JS, storage smoke and offline PowerShell. Preserve existing workflows and canonical release gates. Add repeatable reporting and negative-control evidence before proposing thresholds or mandatory browser coverage; do not bypass tests or imply production release approval.
