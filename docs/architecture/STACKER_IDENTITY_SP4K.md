# NADITrack Stacker Identity v1 — SP-4K Immutable Historical Finals Placement Projection

Status: implementation candidate

## Purpose

SP-4K adds the first server-side historical Finals placement projector for NADITrack Stacker Identity.

It does **not** publish placement. It converts one already-certified SP-4J immutable governed-v2 source snapshot into one explicitly scoped placement projection that a later identity/career phase may consume.

The projector's primary rule is simple:

> Historical placement must be derived from immutable competition-time evidence, never from current live participant/result state and never from a browser-calculated rank.

## Accepted evidence

SP-4K accepts only governance records that have a completed immutable snapshot with all of the following:

- `SnapshotSchemaVersion = finals-ranking-source-v2`;
- `RuleVersion = governed-finals-v2`;
- `operatorContractVersion = sp4h-event-finals-v1` inside the canonical JSON envelope;
- exact snapshot SHA-256 matching the stored governance hash;
- source state/results revisions matching the snapshot competition provenance.

Legacy `finals-ranking-source-v1` snapshots fail closed in SP-4K. Those snapshots do not contain the SP-4J operator-contract provenance needed to prove which reviewed operator implementation the evidence certifies.

## Snapshot-only read boundary

`FinalsHistoricalPlacementProjectionService` reads the immutable `FinalsRankingGovernance` evidence through `FinalsRankingGovernanceService.GetAsync(...)`.

It deliberately does **not** read current:

- `CompetitionState` rows;
- `CompetitionResult` rows;
- `Stacker` rows;
- current permanent-profile data.

The embedded `competitionState` and `finalsResults` from the certified snapshot are the complete historical source for this phase.

This means later edits to live competition state or live result rows cannot rewrite an SP-4K historical placement projection.

## Explicit placement scope

A permanent historical rank is meaningless without its cohort. SP-4K therefore requires every projection request to provide exactly one scope:

- participant type;
- explicit competition-snapshot division;
- event;
- category;
- gender.

For Stacker Identity v1, SP-4K intentionally supports only:

- `participantType = Individual`;
- explicit division other than blank/`all`;
- `event = 3-3-3`, `3-6-3`, or `Cycle`;
- `category = normal`, `special`, or `mixed`;
- `gender = all`, `M`, or `F`.

Doubles and Timed Relay historical career placement remain separate future work.

## Why category and gender belong to the rank

The reviewed operator Finals engine applies category/gender filters **before** placement grouping and ranking.

Therefore the same athlete can legitimately have a different rank under, for example:

- `Individual | Open | Cycle | normal | M`; and
- `Individual | Open | Cycle | mixed | all`.

SP-4K never stores or returns a bare rank divorced from this scope.

## Competition-time participant provenance

For each Individual result in the requested event, SP-4K requires the immutable CompetitionState snapshot to provide enough participant metadata to determine cohort membership:

- participant id;
- explicit division;
- gender (`M` or `F`);
- special flag (`Yes` or `No`).

If that competition-time metadata is missing or ambiguous, the projector fails closed with `participant-metadata-incomplete` rather than inferring from current SQL Stacker data or current permanent identity data.

This is intentionally stricter than live display fallback behavior because a historical permanent rank must prove cohort completeness.

## Governed-v2 calculation

SP-4K implements the reviewed governed-v2 event-level Finals semantics server-side:

1. result-level penalty `>= 999` is Scratch, even when valid attempts exist;
2. valid attempts are numeric values `> 0` and `< 999`;
3. no numeric attempts => Missing;
4. all attempts exactly `999` => Scratch;
5. otherwise no valid attempt => Invalid;
6. finite applicable penalty is `> 0` and `< 999`;
7. governed v2 ranking key is:
   - official best = best raw valid attempt + finite penalty;
   - second raw valid attempt;
   - third raw valid attempt;
8. only Valid rows receive placement;
9. equal complete ranking keys share rank;
10. competition ranking gaps are preserved (`1, 1, 3`).

Non-valid rows remain in the scoped projection with a null rank.

## Projection contract

`FinalsHistoricalPlacementProjection` carries:

- `ProjectionVersion = sp4k-historical-finals-placement-v1`;
- competition id/code/key;
- ranking rule version;
- snapshot schema version;
- operator-contract version;
- immutable snapshot SHA-256;
- source state/results revisions;
- snapshot capture time;
- the complete explicit placement scope;
- projected rows with participant code/name, status, attempts, applied penalty, official best, rank, and factual shared-rank membership.

The projection is read-only and is not persisted in SP-4K.

## Fail-closed blockers

SP-4K uses stable blocker prefixes for integrity failures:

- `snapshot-not-captured`
- `snapshot-schema-unsupported`
- `snapshot-rule-unsupported`
- `snapshot-hash-mismatch`
- `snapshot-payload-malformed`
- `snapshot-provenance-mismatch`
- `scope-invalid`
- `participant-metadata-incomplete`
- `duplicate-logical-result`

## Tests

`StackerFinalsHistoricalPlacementProjectionTests` runs against an isolated generated SQL Server LocalDB database and verifies:

- explicit governed-v2 certification is required first;
- source-v2 and operator-contract provenance are preserved;
- finite penalty changes the governed-v2 primary comparator;
- penalty `999` overrides otherwise-valid attempts to Scratch;
- equal complete keys produce `1, 1, 3` competition ranking;
- normal/special/mixed category filtering happens before ranking;
- gender filtering happens before ranking;
- live CompetitionState mutation after certification cannot change historical cohort/name/division evidence;
- live CompetitionResult mutation after certification cannot change historical rank or official time;
- `division=all` is rejected;
- Doubles projection is rejected in this Stacker Identity phase;
- legacy source-v1 evidence fails closed;
- incomplete competition-time participant metadata blocks projection;
- uncaptured competitions cannot project placement.

A static guard also proves that the projector has no current CompetitionState/CompetitionResult/Stacker read path and is not wired into controllers, public profile presentation, or application startup.

## Deliberately unchanged in SP-4K

SP-4K adds:

- no public API for historical placement;
- no public profile placement field;
- no browser placement calculation;
- no controller/startup activation;
- no placement persistence table;
- no podium claim;
- no medal/award claim;
- no record-holder claim;
- no All-Around ranking change;
- no Prelims ranking change;
- no Doubles/Relay permanent career ranking;
- no Chess behavior change;
- no schema migration.

## Next boundary

After SP-4K is proven and merged, the next safe Stacker Identity ranking phase is **SP-4L — Identity-Linked Finals Placement Career Read Model**.

That phase should associate immutable SP-4K Individual placement rows with permanent NADITrack identities through reviewed historical identity links and finalized/public competition eligibility. It should remain server-owned and separately prove privacy and compatibility before any public-profile presentation is enabled.

Medals, awards and record-holder claims remain later because their award/record provenance requires separate governance.

## Deployment safety

SP-4K performs **no production deployment**, applies **no production database migration**, and writes **no production data** as part of this development phase.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved artifacts, and verify the production `web.config` hash remains unchanged afterward.
