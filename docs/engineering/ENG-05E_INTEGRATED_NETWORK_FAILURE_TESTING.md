# ENG-05E — Integrated network failure testing candidate

Base: ENG-05D d3d1488f9bf34b799193538a65ee7d22b333d7db; [CI 46017180928](https://github.com/casseylim/StackMeet/actions/runs/46017180928) passed all three jobs, including CoreIntegrity LocalDB and positive ENG05D GAP-03/GAP-04 assertions. New hosted CI pending.

## Shared synthetic authority

New tests/eng05e-integrated-network-failure.test.js composes two independent operator VM contexts and a public-viewer context against one deterministic in-memory authority. Uses actual ApiProvider, Repository and ResultApi classes and extracted production state-save queue, explicit review, operator coordinator, SQL snapshot application, public refresh and error rendering functions. Synthetic HTTP responses, auth header/token checks, revision checks and commit counters model the protocol; this is not a real database, full browser, login UI or physical transport test. Production APIs are not contacted.

Scenarios in one continuous competition:
- Initial operator/public projection parity.
- State POST commits then drops response. Second queued write cancels, ETag stays at accepted baseline, no duplicate commit. Failed state review during expired-token simulation retains draft. In-place synthetic replacement token permits authoritative read, cancellation keeps changed local draft, explicit confirmed replacement converges on saved state. Other operator receives authoritative state independently.
- Dirty scoring editor retains typed input across another client's lost result response and pending notification. Unknown outcome blocks duplicate result POST. Stale competing save is rejected. Failed review does not unblock unknown outcome, later successful explicit review accepts latest baseline.
- Public GET captured before deletion; notification while in flight triggers trailing GET, which removes deleted row even with constant timestamp. Operator reconciliation after editor exit converges with authority and public projection. Public 404 clears cached payload; valid subsequent poll restores the correct projection.
- Assert exactly one state commit and two result commits with four total POST attempts (including one rejected stale write). No blind retry or replay.
- Verify browser-close guard for failed state, changed score input, review-required editor and clean state.

## Small runtime improvement

Factor protectUnsavedCompetitionWork into a named beforeunload listener; extend existing queued/failed-state warning to scoring editors that are saving, review-required or have inputs differing from their initial snapshot. The guard uses the existing editor snapshot logic and does not save automatically, alter data or persist drafts. The browser controls whether a generic warning is shown; forced closure can still lose in-memory work. Existing route-change confirmation remains unchanged.

## Limitations and acceptance

The scenario uses an in-place token replacement double, not actual reauthentication via AuthSession, session revocation, token issuance or account switch. Full login recovery UX and durable draft handling remain open. The shared authority approximates server optimistic concurrency; real SQL transactions and security contracts are covered separately by existing CI but are not connected to this browser simulation. Result-editor draft disposition is simulated by explicitly clearing the test editor; the complete DOM review button workflow and real close/logout require browser acceptance. A passing test does not establish real transport latency, operational recovery or production readiness. No new schema, permissions, public access policy or ranking logic. ENG-05F will consolidate evidence and remaining release gates.

Mandatory: Overall/All-Around use Finals only, preliminary reports separate, certified historical snapshots and digests immutable. Production IIS web.config must NEVER be overwritten in deployment, sync or rollback: explicit exclusion, secure backup and identical SHA-256 before/after required. No master merge or deployment without explicit owner approval.
