# NADITrack Stacker Identity v1 — SP-4I Governed Finals v2 Certification Readiness

Status: implementation candidate

## Purpose

SP-4I answers one narrow question before any historical v2 snapshot is allowed:

> Does a finalized Sport Stacking competition contain enough durable, internally consistent evidence to certify a future `governed-finals-v2` ranking snapshot reproducibly?

The phase is a **readiness assessment only**. It does not remove SP-4G's v2 snapshot-capture block, publish historical placement, add a ranking-selection UI/API, or deploy anything to production.

## Why the existing evidence can be sufficient

SP-4H made the operator's **event-level Finals** path consume the same persisted ranking rule stored by SP-4G. When `governed-finals-v2` is explicitly selected, the reviewed operator contract uses that version and fails closed if the rule is missing or unsupported.

SP-4G already stores the evidence required for deterministic reconstruction:

- the selected ranking-rule version;
- the competition-state JSON used for competition-time participant/team division provenance;
- the exact competition-state revision;
- durable SQL Finals result rows;
- the exact competition results revision;
- raw attempt JSON and result-level penalty;
- a canonical serialized source snapshot protected by SHA-256;
- one-time immutable snapshot persistence enforced by the database trigger.

The certification target is therefore the **governing rule plus immutable ranking inputs**, not proof that a human operator viewed a particular browser rendering. A later historical projection can deterministically apply the versioned policy to the certified source evidence.

## Operator contract provenance

SP-4I identifies the reviewed operator activation contract as:

`sp4h-event-finals-v1`

This contract means:

- event-level Finals consumes the persisted rule;
- unversioned competitions remain `legacy-finals-v1`;
- explicit `governed-finals-v2` uses the reviewed v2 classification and tie ordering;
- missing/unknown selected-competition rules fail closed;
- Prelims remain legacy-compatible;
- All-Around remains outside this governed-v2 activation boundary.

SP-4I does not claim that Prelims or All-Around are governed by v2.

## Readiness boundary

`FinalsRankingCertificationReadinessService` is an internal, read-only advisory service. It does not write governance, state, results, or snapshots.

A competition is ready only when all of the following are true:

1. the competition exists;
2. it is a Sport Stacking competition;
3. it is finalized (`Closed`, `Archived`, or has an archive timestamp);
4. `governed-finals-v2` was explicitly persisted in `FinalsRankingGovernance`;
5. no immutable ranking snapshot has already been captured;
6. an authoritative `CompetitionState` row exists;
7. the state revision is positive;
8. state JSON is a valid JSON object;
9. every durable SQL Finals result revision is positive and does not exceed `Competition.ResultsRevision`;
10. every durable SQL Finals attempts payload is a numeric JSON array.

An empty Finals-result set is allowed. It certifies an empty Finals source dataset rather than fabricating placement.

## Stable blocker codes

The readiness result returns stable machine-readable blocker codes:

- `competition-not-found`
- `activity-not-sport-stacking`
- `competition-not-finalized`
- `governed-finals-v2-not-explicitly-selected`
- `unsupported-rule-version`
- `snapshot-already-captured`
- `competition-state-missing`
- `competition-state-malformed`
- `state-revision-invalid`
- `result-revision-inconsistent`
- `result-attempts-malformed`

This is deliberately more useful than a single Boolean because the next activation phase can fail closed with an auditable reason rather than guessing why certification is unsafe.

## Concurrency and source consistency

SP-4I is an advisory check and intentionally does not claim transactional certification. The actual SP-4G snapshot capture already uses a serializable transaction and locks the competition and competition-state rows while it reads durable Finals results.

Normal SQL result writes also lock the competition row and are rejected when a competition is Closed/Archived. Therefore the future activation phase must **re-run or embed the same readiness conditions inside the snapshot-capture transaction** before permitting governed-v2 certification. A positive SP-4I assessment by itself must never be treated as an already-certified snapshot.

This avoids a time-of-check/time-of-use gap.

## Deliberately unchanged in SP-4I

SP-4I does **not**:

- remove the explicit `governed-finals-v2` block in `CaptureFinalizedSnapshotAsync`;
- add a controller or public endpoint for readiness/certification;
- add a rule-selection UI or API;
- capture a governed-v2 snapshot;
- calculate or persist historical rank;
- publish placement, podium, medal, award, record-holder, All-Around or global-ranking claims;
- change Prelims or All-Around semantics;
- add a schema migration;
- mutate production data.

## Tests

`StackerFinalsRankingCertificationReadinessTests` uses an isolated generated SQL Server LocalDB database and verifies:

- an active v2 competition is blocked until finalized;
- a finalized explicit v2 competition with valid state/results evidence is ready;
- state/results revision provenance is reported;
- an empty Finals dataset may still be ready;
- unversioned/legacy competitions are not misrepresented as v2;
- missing or malformed state fails closed;
- non-positive state revisions fail closed;
- impossible result revisions fail closed;
- malformed attempts JSON fails closed;
- non-Sport-Stacking competitions fail closed;
- SP-4I readiness does not bypass the existing SP-4G v2 snapshot-capture block.

A static regression guard also requires the readiness boundary to remain internal and requires the production deployment safeguards to remain documented.

## Next boundary

If SP-4I passes review and CI, the next safe phase is **SP-4J — Governed Finals v2 Snapshot Certification Activation**.

SP-4J may remove the v2 capture block only if the readiness rules are re-evaluated inside the same serializable snapshot transaction. It still should not publish permanent historical placement in the same phase.

Historical placement projection should remain a later, separately reviewed read-model phase after a governed-v2 snapshot can be certified immutably.

## Deployment safety

SP-4I performs **no production deployment**, applies **no production database migration**, and writes **no production data**.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved binaries/static artifacts, and verify the production `web.config` hash remains unchanged afterward.
