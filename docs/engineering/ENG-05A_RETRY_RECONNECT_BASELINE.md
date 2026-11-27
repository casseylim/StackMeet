# ENG-05A — Retry, disconnect and synchronization baseline candidate

Base: ENG-04F 04c3e6c38828b1a1cfc52f814805da2bc3d9cae9. [CI 45917593026](https://github.com/casseylim/StackMeet/actions/runs/45917593026) passed all three jobs; build job 140240499146 logged PASS ENG04F reconciliation/recovery integration and negative control, and the full CoreIntegrity LocalDB harness passed. All six ENG-04 development packages are CI-validated. Operational exit criteria remain partially satisfied, as documented in ENG-04F. This package changes only tests and documentation. New hosted CI pending.

## Authority and synchronization boundaries

- CompetitionState JSON: ApiProvider owns the accepted If-Match ETag. Initial load adopts it; ordinary later load does not; reloadLatest explicitly adopts the latest ETag. Repository owns additive merge and generation-aware tombstones. app.js saveState serializes writes through pendingSave and captures state/tombstones at execution time. Failed saves do not acknowledge tombstones. An accepted ETag is not proof the operator has reviewed every field of the matching server baseline.
- SQL results: ResultApi owns competition-scoped full snapshot cache, generation invalidation, minimum revision, write-in-progress guard and uncertain-outcome review barrier. Server owns dataset/row revisions and atomic batch commit. Confirmed rejection, unknown outcome, confirmed commit with failed refresh and reviewed retry must remain distinct. Editor baseline captures immutable row keys/revisions and drafts in app.js; review requires explicit acceptance. Never treat an event or timeout as commit acknowledgment.
- Notifications: CompetitionChanged and ResultsChanged are separate hints, not authoritative data or durable delivery. Ignore wrong competition and obsolete result revisions; do not advance an applied watermark on an unsuccessful refresh. State and result revisions are independent; missing or delayed events require authoritative GET. Operator polling runs every three seconds on relevant routes, skips active time-entry inputs; public polling every 15 seconds. Rejoin the authorized competition group before resynchronizing. No automatic write replay on reconnect.
- Public projection: public results.js refreshes the entire projection on either event, uses no-store HTTP, and compares lastUpdatedAt/branding version. Public subscription failure falls back to HTTP. Keep full GET and existing publication/access boundaries. Overall/All-Around remains Finals only, prelims separately.

## New characterization suite

tests/retry-reconnect-eng05a.test.js exercises actual ApiProvider and ResultApi with synthetic in-memory HTTP responses and actual extracted notification/refresh functions using Node VM and deterministic SignalR doubles. Existing complete JavaScript regression discovery includes this file; no CI workflow modifications.

| Scenario | Current baseline expectation |
|---|---|
| State POST commits but response lost | Old ETag retained; ordinary load doesn't rebase; stale retry rejected by mock revision check; explicit reload adopts latest |
| SQL result POST commits but response lost | Unknown outcome, no second automatic POST; second stale client rejected; failed review remains blocked; explicit accepted review and subsequent valid edit converge |
| Delayed GET after delete | Older snapshot cannot resurrect deleted row |
| Confirmed SQL POST with failed authoritative GET | Committed-refresh-failed; write blocked until explicit review |
| Operator events | Wrong competition and obsolete revisions ignored, active editor queues result refresh, join before state refresh |
| Public events | In-flight notifications characterized; subsequent authoritative poll removes deleted row; reconnect joins before GET |

The following are characterization observations, not acceptable future behavior or tests to preserve as desired contracts. Replace these assertions with positive recovery assertions when the corresponding correction is implemented.

| Gap | Current source observation / effect | Follow-up |
|---|---|---|
| GAP-01 | Operator CompetitionChanged advances currentCompetitionRevision before refresh/apply succeeds. Same revision then ignored on failure. Polling may recover later; no immediate guaranteed retry. | Separate seen/pending/applied state, retry after failed read, prevent stale apply. |
| GAP-02 | Operator reconnect joins then loads/applies JSON state; handler has no independent SQL result refresh. applyRemoteCompetitionState may return early for unchanged JSON, and its refreshSqlStackers path is not an unconditional result GET. | Independent authoritative SQL result refresh on reconnect and fallback polling, including missed delete. |
| GAP-03 | Public refreshInFlight returns immediately on concurrent trigger, so events during GET may be lost until next poll. | Bounded trailing refresh/coalescing; no overlapping requests; retain periodic full GET. |
| GAP-04 | Public failed background GET, including 404 after previous render, leaves prior visible projection while reporting reconnecting. | Review withdrawal semantics and fail-closed behavior with ENG-02/ENG-07 publication policy; add negative tests before changes. |
| GAP-05 | Global state save queue/remote reload can adopt newer ETag independently of reviewed operator baseline; queued remote state and result refresh failure need explicit lifecycle coverage. | Characterize queue/rebase race, pending state and draft ownership, auth renewal and close during edit. |

## Proposed staged work (subject to owner direction)

- ENG-05A: this baseline and current-behavior characterization.
- ENG-05B: state save queue and ETag/draft lifecycle; prevent blind rebase/replay, preserve exact tombstones and unsaved intent through rejection or unknown outcome.
- ENG-05C: operator notification/reconnect and polling; seen versus applied revisions, independent result resync, bounded retry, competition epoch and editor protection.
- ENG-05D: public reconnect/coalescing and failed refresh/withdrawal behavior; authorized join before GET, no stale publication, preserve fallback.
- ENG-05E: integrated two-client fault scenarios including dropped post-commit response, duplicate/delayed/missing events, reconnect after delete, close during edit and auth renewal, convergence and visible unsaved entries.
- ENG-05F: completion evidence and owner gates; actual browser/network interruption and production runtime acceptance remain separate.

No claim of real SignalR, browser, TCP disconnect or server-side network fault testing from this suite. Mock OCC is not a replacement for existing LocalDB atomic-write regression. GAP-04/05 are source observations not yet dynamically reproduced; do not claim fixed. No application behavior changes or retries added in ENG-05A. All ENG-04 residual risks and ENG-03 browser/accessibility/report acceptance gates carry forward. No master merge or production deployment. Production IIS web.config must never be overwritten by upload/sync/rollback; secure backup and identical pre/post SHA-256 required. Certified historical snapshot bytes/digests immutable; Overall/All-Around Finals only.

## CI assertion correction

Initial workflow [45960913857](https://github.com/casseylim/StackMeet/actions/runs/45960913857) passed offline reproducibility but failed Build and test and Regression evidence/API coverage at the new public reconnect assertion. All preceding transport and operator assertions passed and GAP-01 through GAP-03 were reproduced. The application computes its public version signature as lastUpdatedAt, logoUrl and bannerUrl joined with pipe separators. With no branding, the expected fixture signature is v3||, not v3. Correct the test expectation and independently require the latest payload timestamp, empty result list and rendered projection after reconnect. No application changes, skipped tests or weakened synchronization guards. Corrected hosted CI pending.
