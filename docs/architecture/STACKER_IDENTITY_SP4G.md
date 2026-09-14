# NADITrack Stacker Identity v1 — SP-4G Persisted Finals Ranking Snapshot & Activation Boundary

Status: implementation candidate

## Purpose

SP-4G turns the SP-4F ranking-governance contract into a durable persistence boundary without changing the current operator Finals report and without publishing historical placement.

The phase preserves two distinct facts:

1. which versioned Finals ranking policy was explicitly selected for a Sport Stacking competition; and
2. the immutable source evidence needed to reproduce a finalized ranking later.

The snapshot stores source evidence, **not browser-calculated ranks**. A later server-side projection phase may derive permanent placement only from this frozen evidence and the matching reviewed rule version.

## Safety boundary

SP-4G deliberately does not:

- wire `FinalsRankingGovernanceService` into the current admin controller or application runtime;
- switch `FinalsReportEngine.js` to `governed-finals-v2`;
- expose a public or operator endpoint for changing the ranking version;
- publish rank, podium, medal or award history;
- mutate existing competition results or competition-state JSON;
- automatically capture a snapshot when status changes;
- deploy production code or apply the new migration to production.

This prevents a schema/persistence phase from silently changing current competition behavior.

## Rule-version persistence

The canonical rule identifiers remain:

- `legacy-finals-v1`;
- `governed-finals-v2`.

The server-side persistence boundary mirrors the SP-4F identifiers exactly.

Rules:

- no governance record means the competition remains historically unversioned and resolves to `legacy-finals-v1`;
- an explicit stored version must be one of the two reviewed identifiers;
- unknown versions fail closed in both service validation and a database CHECK constraint;
- explicit rule selection is accepted only while a competition is `Draft` or `Active` and not archived;
- rule selection cannot change after a finalized source snapshot exists.

SP-4G has no controller/UI route to invoke rule selection. Therefore persisting `governed-finals-v2` through the service is a data-layer capability only and does not make the existing operator Finals report use v2.

## Governed-v2 certification remains blocked

SP-4F defined `governed-finals-v2`, but the current operator Finals engine still uses the frozen legacy behavior.

For that reason, SP-4G explicitly refuses to capture a finalized historical source snapshot when the selected rule is `governed-finals-v2`.

This is intentional. NADITrack must not certify a v2 historical ranking unless a later phase can prove that officials actually operated the competition under the same v2 rule.

A later operator-engine activation phase must make the live Finals ranking path version-aware before governed-v2 snapshot certification can be enabled.

## Persistence model

Migration `20260914093000_FinalsRankingGovernanceSp4g` creates:

`dbo.FinalsRankingGovernance`

There is one row per competition, keyed by `CompetitionId`.

The row contains:

- `RuleVersion`;
- `RuleSelectedAt`;
- `RuleSelectedByUserId`;
- `SnapshotSchemaVersion`;
- `SourceStateRevision`;
- `SourceResultsRevision`;
- `SnapshotJson`;
- `SnapshotSha256`;
- `SnapshotCapturedAt`;
- `SnapshotCapturedByUserId`.

### Why this table is migration-managed

The evidence table is intentionally migration-managed rather than a normal EF-tracked aggregate in SP-4G.

The potentially large immutable snapshot should not be hydrated whenever ordinary `Competition` entities are queried. `FinalsRankingGovernanceService` owns the narrow SQL access boundary instead. This also makes the one-way transition from mutable rule selection to immutable finalized evidence explicit.

Future phases may introduce a dedicated read projection without turning the evidence payload into normal mutable application state.

## Database invariants

The migration enforces:

- primary key: one governance record per competition;
- restrictive FK to `Competition` so frozen evidence is not cascade-deleted;
- supported rule-version CHECK constraint;
- snapshot-completeness CHECK constraint: snapshot metadata is either entirely absent or the required provenance fields are all present;
- trigger `TR_FinalsRankingGovernance_ImmutableSnapshot`.

The trigger allows the one transition from an uncaptured governance row to a captured snapshot. Once `SnapshotCapturedAt` is non-null, any later UPDATE or DELETE of that row throws SQL error `51041` and the statement is rolled back.

This gives snapshot immutability a database boundary rather than relying only on application convention.

## Snapshot eligibility

`CaptureFinalizedSnapshotAsync` requires:

- an existing competition;
- Sport Stacking activity compatibility (`ActivityModuleCode` blank/legacy-compatible or `sport-stacking`);
- competition status `Closed` or `Archived`, or a non-null archive timestamp;
- an existing valid `CompetitionState` JSON object;
- no previously captured snapshot;
- a rule version that can be historically certified in the current phase.

An unversioned finalized historical competition is captured as `legacy-finals-v1` atomically with creation of its governance row.

Missing or malformed competition-state provenance fails closed.

## Frozen source evidence

The snapshot schema identifier is:

`finals-ranking-source-v1`

The canonical snapshot JSON freezes:

### Competition provenance

- competition code and key;
- resolved activity-module code;
- finalized status;
- start/end date;
- `CompetitionState.StateRevision`;
- `Competition.ResultsRevision`.

### Competition-state snapshot

The full valid competition-state JSON object is embedded as internal evidence.

This is important because the competition-state participant/team `division` fields preserve the competition-time division snapshot that SP-4F declared authoritative for historical placement.

The snapshot is internal persistence evidence and is **not a public-profile payload**. Future public projection must continue to apply the existing privacy boundary and must never expose registration/contact fields merely because they exist inside the internal state evidence.

### Durable Finals results

All SQL `CompetitionResult` rows with `Stage = Finals` are frozen in deterministic participant/event order with:

- public result ID;
- participant type;
- participant code;
- event code;
- raw `AttemptsJson`;
- result-level penalty;
- result revision.

Prelims and SOC rows are not part of the Finals ranking evidence snapshot.

## Integrity hash

The canonical serialized evidence payload is hashed with SHA-256.

`SnapshotSha256` stores the uppercase 64-character hexadecimal digest. The integration test recomputes the digest from persisted `SnapshotJson` and requires an exact match.

The hash is evidence-integrity metadata; it is not a signature and does not replace database access controls or backup integrity.

## Concurrency

Selection and capture run in serializable transactions.

The service locks the competition row and, for capture, the competition-state row. Existing result writes already lock the competition row before changing `CompetitionResult` and `ResultsRevision` and reject writes to Closed/Archived competitions.

Therefore a finalized snapshot captures a consistent competition status, state revision and durable results revision boundary rather than racing a normal result mutation.

## Tests

`tests/StackerFinalsRankingGovernancePersistenceTests` uses an isolated generated LocalDB database and verifies:

- migration creates the governance table and immutability trigger;
- supported rule selection persistence and normalization;
- unknown rule versions fail closed;
- rule selection remains mutable only before finalization/capture;
- finalized legacy snapshot capture;
- competition-time division state is preserved;
- durable Finals result evidence is preserved;
- SHA-256 integrity metadata matches the payload;
- a second capture is rejected;
- database UPDATE/DELETE after capture is rejected by the trigger;
- unversioned historical competition captures as legacy v1;
- governed-v2 certification is blocked in SP-4G;
- non-Sport-Stacking competitions are rejected;
- missing/malformed competition-state provenance blocks capture;
- database rule-version and snapshot-completeness constraints reject invalid direct writes;
- test database cleanup is prefix-guarded and LocalDB-only.

`tests/stacker-identity-v1-sp4g.static.test.js` additionally proves the persistence service is not wired into the current admin/runtime path and that `FinalsReportEngine.js` remains unchanged by the new governance persistence contract.

## Next boundary

After SP-4G, the next safe step is an operator-engine activation phase that makes the live Finals report explicitly consume the selected rule version while preserving legacy-v1 behavior for unversioned/existing competitions.

Only after that activation path is proven should NADITrack enable governed-v2 snapshot certification and then build permanent server-side placement projection from immutable snapshots.

## Deployment safety

SP-4G performs **no production deployment** and the migration is not applied to production as part of this development phase.

The production deployment hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later release must back up and hash the existing production `web.config`, exclude it from the deployment payload, deploy only intended application/static/database artifacts through a separately approved procedure, and verify that the production `web.config` hash remains unchanged afterward.
