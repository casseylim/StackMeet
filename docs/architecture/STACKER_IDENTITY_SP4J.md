# NADITrack Stacker Identity v1 — SP-4J Governed Finals v2 Snapshot Certification Activation

Status: complete

## Purpose

SP-4J activates one narrow capability: a finalized Sport Stacking competition that explicitly selected `governed-finals-v2` may certify an immutable Finals ranking source snapshot **only when the SP-4I evidence rules pass inside the same serializable capture transaction**.

SP-4J still performs no historical placement calculation or publication. The certified artifact remains source evidence: governing rule version, reviewed operator-contract provenance, competition-time state/division provenance, durable SQL Finals rows, exact revisions, raw attempts/penalties, capture actor/time, and SHA-256 integrity.

## Explicit certification seam

`FinalsRankingGovernanceService` adds:

`CertifyGovernedV2SnapshotAsync(...)`

This is intentionally separate from the original `CaptureFinalizedSnapshotAsync(...)` method.

The original generic method retains the SP-4G governed-v2 fail-closed block. This prevents an old or generic caller from silently acquiring new v2-certification behavior merely because the application was upgraded.

Only the explicit SP-4J certification method may cross the v2 boundary.

## Transactional certification rule

Certification runs in the same serializable transaction used by the immutable snapshot capture boundary.

Inside that transaction SP-4J:

1. locks the competition row;
2. verifies the activity is Sport Stacking;
3. verifies the competition is finalized;
4. locks and reads the Finals ranking governance record;
5. requires an explicit persisted `governed-finals-v2` selection;
6. rejects an already-captured snapshot;
7. locks the authoritative CompetitionState row;
8. reads the durable SQL Finals result rows;
9. re-evaluates the SP-4I source-evidence rules;
10. serializes the deterministic snapshot;
11. hashes the exact payload with SHA-256;
12. writes the one-time immutable governance snapshot;
13. commits only after the captured record can be re-read.

The database trigger introduced by SP-4G still blocks UPDATE and DELETE after capture.

## Versioned source-evidence provenance

SP-4J keeps the legacy evidence payload frozen as `finals-ranking-source-v1` and introduces `finals-ranking-source-v2` only for explicit governed-v2 certification.

The v2 envelope additionally persists:

- `ruleVersion = governed-finals-v2`; and
- `operatorContractVersion = sp4h-event-finals-v1`.

This matters because a future historical-placement projector must know not only which policy identifier was selected, but which reviewed operator implementation contract the certification attests was active. The operator-contract value is inside the canonical JSON payload and therefore covered by the stored SHA-256 hash.

Legacy v1 snapshots retain their original payload shape and do not gain an operator-contract field.

## Shared evidence validator

SP-4J extracts the SP-4I source checks into one internal validator used by both:

- the advisory `FinalsRankingCertificationReadinessService`; and
- the transactional `CertifyGovernedV2SnapshotAsync` path.

The source-evidence blockers remain:

- `competition-state-missing`
- `competition-state-malformed`
- `state-revision-invalid`
- `result-revision-inconsistent`
- `result-attempts-malformed`

The certification method reports these stable blocker codes when the locked source evidence is not certifiable.

The higher-level preconditions are enforced directly by the locked certification transaction: competition existence/activity/finalization, explicit v2 selection, supported rule version, and one-time snapshot state.

## Why the generic v2 block remains

Earlier phases deliberately required `CaptureFinalizedSnapshotAsync` to fail closed for `governed-finals-v2`. SP-4J does not erase that historical safety contract.

Keeping the old method blocked provides an explicit activation boundary:

- legacy/unversioned snapshot capture behavior is unchanged;
- old callers cannot accidentally start certifying v2;
- v2 certification is discoverable in code review as a separate method call;
- the SP-4J integration harness can prove both paths independently.

## Empty Finals dataset

A finalized explicit v2 competition with valid CompetitionState evidence and zero Finals result rows may certify an empty Finals source snapshot.

This is evidence that no durable Finals rows existed at the certified revision. It does not fabricate ranks or placements.

## Deliberately unchanged in SP-4J

SP-4J adds:

- no controller/API for certification;
- no automatic capture on competition close/archive;
- no rule-selection UI/API;
- no schema migration;
- no historical placement calculation;
- no public placement, podium, medal, award or record-holder claim;
- no All-Around governance change;
- no Prelims governance change;
- no Chess scoring or activity behavior change.

The operator Finals path remains the SP-4H version-aware event-level implementation.

## Tests

`StackerFinalsRankingCertificationActivationTests` runs against an isolated generated SQL Server LocalDB database and verifies:

- SP-4I readiness is positive before a valid v2 certification;
- the original generic v2 capture path remains blocked;
- the explicit SP-4J method captures a valid governed-v2 snapshot;
- v2 uses `finals-ranking-source-v2` and freezes `sp4h-event-finals-v1` operator-contract provenance;
- legacy capture retains `finals-ranking-source-v1` without the v2 provenance field;
- rule version, source revisions and capture actor are persisted;
- raw attempts and penalties are frozen;
- SHA-256 matches the exact immutable payload;
- readiness reports `snapshot-already-captured` afterward;
- a second certification is rejected;
- malformed attempts fail with `result-attempts-malformed` and write no snapshot;
- impossible result revisions fail with `result-revision-inconsistent` and write no snapshot;
- missing state fails with `competition-state-missing`;
- an empty valid Finals dataset may certify;
- the explicit v2 method rejects a legacy competition;
- the original legacy snapshot path remains functional.

A static guard keeps the explicit method internal to the data/service layer and preserves the production-safety rules.

## Next boundary

After SP-4J is proven and merged, the next safe ranking-governance phase is **SP-4K — Immutable Historical Finals Placement Projection**.

SP-4K should project placement server-side from the certified immutable snapshot and its stored rule version plus operator-contract provenance. It should not trust browser-calculated ranks and should not publish placement publicly until the projection has its own compatibility/integrity tests.

Medal/award publication remains later because award configuration and award-governance provenance require their own reviewed boundary.

## Deployment safety

SP-4J performs **no production deployment**, applies **no production database migration**, and writes **no production data** as part of this development phase.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved artifacts, and verify the production `web.config` hash remains unchanged afterward.
