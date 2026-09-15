# NADITrack Stacker Identity v1

Status: SP-4K Immutable Historical Finals placement projection implementation candidate

## Purpose

NADITrack uses one permanent Sport Stacking identity per person so competition entries and results from different competitions can contribute to one career profile.

> A competition Stacker is an entry. A NADITrack Stacker is a person.

The identity roadmap is intentionally incremental. Existing Sport Stacking registration, result, qualification and reporting behavior remains authoritative and is not redesigned by the permanent-identity work.

## Identity authority

- `NadiTrackId` is the permanent public NADITrack athlete identifier.
- One person keeps the same NADITrack ID for life.
- The NADITrack ID is immutable once issued.
- Database primary keys remain internal and are never substituted for the public NADITrack ID.
- `WssaId` is optional external-reference metadata only and is not the NADITrack primary identity.
- Existing competition-scoped `Stacker.WssaId` remains untouched for backward compatibility.

The v1 public identifier format is `NDT-XXXXXXX`, using a restricted uppercase alphabet that excludes visually ambiguous characters.

## Permanent identity and linking

`SportStackerIdentity` is the person-level identity. `Stacker` remains the competition-scoped registration snapshot.

`StackerIdentityLink` associates a competition Stacker with one permanent identity and preserves resolution provenance. Historical competition snapshots are never rewritten merely because permanent profile information changes later.

SP-0B defines deterministic candidate matching. SP-0C defines the explicit resolution gate. StackMeet must never auto-merge two identities based on name alone. Candidate strength is not merge authority: name-only matches are never auto-merged, and even Strong candidates require the reviewed decision boundary unless an explicit NADITrack ID resolved authoritatively.

SP-1 persists approved decisions transactionally. New identities are private by default (`IsPublicProfile = false`). SP-2 provides review-required historical discovery/backfill with no automatic linking state.

## Privacy-safe public career projection

`SportStackerCareerProfileService` is the read-only public career projection keyed by NADITrack ID.

A public profile exists only when `SportStackerIdentity.IsPublicProfile = true`. Malformed, unknown and private identities share the same public not-found boundary.

Career data is restricted to competitions that are:

- `IsPubliclyListed = true`; and
- `Closed`, `Archived`, or have a non-null archive timestamp.

Active/provisional and non-public competitions do not become permanent public career records.

Only Individual results contribute to the Individual career features described below. Doubles and Relay career aggregation remain separate future work.

The public contract excludes birth date, email, phone, gender, WSSA ID, payment/check-in data, internal database IDs and other registration-only attributes.

## SP-3A — Personal Bests

SP-3A introduced finalized public career totals and Personal Bests for 3-3-3, 3-6-3 and Cycle.

Both Prelims and Finals may contribute. Timing follows the established result semantics used by the project:

- valid attempt: greater than zero and less than 999;
- raw best: lowest valid attempt;
- applicable penalty: greater than zero and less than 999;
- official time: raw best plus applicable penalty.

Malformed or non-valid rows do not fabricate a PB.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP3A.md`.

## SP-3B — Public endpoint and profile route

SP-3B exposes the reviewed career projection at:

- `GET /api/public/stackers/{NadiTrackId}`;
- `/Stackers/{NadiTrackId}`.

The public page is isolated under `wwwroot/profile/`, uses `cache: 'no-store'`, `credentials: 'omit'`, DOM element creation and `textContent`, and carries `noindex,nofollow` while ownership/minors-consent governance remains deferred.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP3B.md`.

## SP-4A — Tournament History

SP-4A adds finalized public tournament appearances and best valid Individual performance per event within each competition.

An appearance remains visible even when no valid Individual time exists. Active/private competitions and Doubles rows cannot enter the history.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4A.md`.

## SP-4B — Tournament History presentation

SP-4B presents the SP-4A projection on the permanent public profile without adding a second browser-side timing engine.

The production release-safety rule was also formalized here: production `web.config` must be preserved, excluded from deployment payloads, and verified unchanged by hash after any later production release.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4B.md` and `docs/architecture/STACKER_IDENTITY_SP4B_RELEASE_NOTE.md`.

## SP-4C — Career Progression

SP-4C provides chronological per-event progression across finalized public competitions.

For each tournament-best performance it records whether the performance established a new PB, the running PB after that point, and the exact improvement when a strict improvement occurs. Ties do not create duplicate PB milestones and slower performances remain visible without changing the PB.

The server owns progression calculations; the browser only renders the reviewed projection.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4C.md`.

## SP-4D — Finals Career Aggregation

SP-4D adds factual Finals-stage career history per Individual event:

- Finals appearance count;
- valid Finals-result count;
- first and latest Finals dates;
- best valid Finals official performance;
- chronological Valid / Scratch / Missing / Invalid Finals history.

SP-4D intentionally does **not** publish placement, podium, medal, award, record-holder, All-Around or global-ranking claims. Historical placement requires a separately reviewed compatibility contract for division reconstruction, penalty treatment, tie-breaking and award configuration so the permanent profile cannot disagree with the existing Finals report.

A best Finals performance is distinct from the SP-3A career PB. A career PB may legitimately come from Prelims.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4D.md`.

## SP-4E — Finals Ranking Compatibility Contract

SP-4E characterizes the existing Finals placement implementation without changing it or publishing historical placement.

The compatibility test locks the existing Finals-stage boundary, participant-type/division/event grouping, valid-only ranking, best/second/third-attempt tie key, equal-rank behavior, and category/gender filtering scope.

The phase also records two penalty-semantic compatibility findings that block safe historical placement publication today:

- shared `BestResultEngine.rankingTime()` applies a finite penalty, while `FinalsReportEngine.finalTieKey()` orders raw valid attempts without it;
- an otherwise-valid attempt is classified Valid before a `999` penalty is considered by the current shared classifier.

A permanent rank also requires explicit scope and historical division provenance. SP-4E therefore keeps placement, podium, medals and awards unpublished until those decisions are reviewed and versioned explicitly.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4E.md`.

## SP-4F — Versioned Finals Ranking Governance

SP-4F freezes current operator behavior as `legacy-finals-v1` and defines a separate corrected future contract, `governed-finals-v2`, without activating it.

The version boundary protects historical competitions:

- missing/blank version resolves to `legacy-finals-v1`;
- explicit v1 remains v1;
- explicit v2 selects the governed contract only after a future activation/persistence phase;
- unknown non-blank versions fail closed;
- no date or software version can silently upgrade a competition.

Governed v2 treats a result-level `999` penalty as Scratch even when valid attempts are present. Its ranking key uses official best time (`raw best + finite penalty`) first, then second-best and third-best raw valid attempts. Equal complete keys preserve competition ranking (`1, 1, 3`).

A permanent placement must carry an explicit participant type, competition-snapshot division, event, category and gender scope; a bare rank is forbidden. For legacy history, the competition-state participant division snapshot used by the operator competition is authoritative when available. If that historical snapshot is missing or ambiguous, permanent placement remains unpublished instead of being reconstructed from current permanent-identity data.

SP-4F adds an isolated policy module and tests only. It does not wire v2 into `app.js` or add a schema field or publish placement.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4F.md`.

## SP-4G — Persisted Finals Ranking Snapshot & Activation Boundary

SP-4G adds the durable evidence boundary required before historical placement can ever be certified.

A dedicated `FinalsRankingGovernance` persistence record stores an explicitly selected ranking rule version and, after finalization, one immutable source snapshot containing the competition-time state/division evidence plus durable SQL Finals results and exact state/results revisions.

The snapshot stores ranking **inputs**, not calculated ranks. Its canonical payload is protected by SHA-256 and, after capture, a database trigger blocks UPDATE and DELETE of the captured governance record.

Historical unversioned competitions still resolve to and freeze as `legacy-finals-v1`. `governed-finals-v2` selection can be represented in the data layer, but SP-4G refuses to certify/capture a v2 finalized snapshot because the operator Finals engine was not yet version-aware at that phase.

SP-4G deliberately added no rule-selection UI/runtime activation and does not publish placement.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4G.md`.

## SP-4H — Operator Finals Ranking Version Activation

SP-4H activates the persisted rule only for the operator's event-level Finals ranking path.

The authenticated read-only rule projection exposes the effective version for accessible Sport Stacking competitions. The browser loads the reviewed `FinalsRankingPolicy.js` contract and `FinalsReportEngine` applies that persisted version to Finals classification, tie keys, event placement and organization-credit inputs.

Unversioned competitions remain `legacy-finals-v1`. Missing/unknown selected-competition rule state fails closed rather than silently changing ranking semantics.

SP-4H intentionally keeps Prelims on legacy-compatible behavior and keeps All-Around outside the activation scope. It adds no rule-selection endpoint, does not unblock governed-v2 historical snapshot certification, and does not publish permanent placement/podium/medal claims.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4H.md`.

## SP-4I — Governed Finals v2 Certification Readiness

SP-4I adds an internal, read-only readiness assessor for governed-v2 historical snapshot certification.

The assessor verifies that the competition is finalized Sport Stacking, that `governed-finals-v2` was explicitly selected, that no snapshot already exists, that authoritative competition-state provenance is present and valid, and that durable SQL Finals results have internally consistent revisions and numeric attempts JSON.

The readiness result carries stable blocker codes plus the reviewed operator contract identifier `sp4h-event-finals-v1`, state revision, results revision and Finals result count. An empty Finals dataset may still be ready because the certified artifact is the source evidence, not fabricated placement.

SP-4I does **not** remove the existing v2 capture block. A positive readiness result is advisory; the next activation phase must re-evaluate the same conditions inside the serializable snapshot transaction to avoid a time-of-check/time-of-use gap.

SP-4I adds no public/controller endpoint, no schema migration, no historical rank publication and no production deployment.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4I.md`.

## SP-4J — Governed Finals v2 Snapshot Certification Activation

SP-4J adds an explicit certification seam for finalized competitions that persisted `governed-finals-v2` before finalization.

The new certification path re-evaluates the SP-4I source-evidence rules after acquiring the existing serializable competition/governance/state locks and before writing the immutable source snapshot. The original generic snapshot method deliberately remains fail-closed for v2, so old callers cannot acquire certification behavior implicitly.

A successful v2 certification persists the governing rule, exact state/results revisions, competition-time state/division provenance, durable SQL Finals rows with raw attempts and penalties, capture actor/time and the SHA-256 of the canonical immutable payload. The database immutability trigger remains authoritative after capture.

SP-4J adds no controller/API, no automatic close/archive capture, no schema migration and no historical placement, podium, medal or award publication.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4J.md`.

## SP-4K — Immutable Historical Finals Placement Projection

SP-4K adds a server-side, read-only historical Finals placement projector for certified `governed-finals-v2` evidence.

The projector accepts only `finals-ranking-source-v2` snapshots whose SHA-256, rule version, source revisions and embedded `sp4h-event-finals-v1` operator-contract provenance are intact. It derives placement from the immutable snapshot only and deliberately does not read current `CompetitionState`, `CompetitionResult`, `Stacker` or permanent-identity data.

Every projected rank is bound to one explicit cohort: Individual participant type, competition-snapshot division, event, category and gender. Category/gender filters are applied before ranking, matching the reviewed operator contract. Governed-v2 classification and tie rules remain server-owned, including finite-penalty official-best ordering, penalty-999 Scratch precedence and competition ranking gaps such as `1, 1, 3`.

If competition-time participant metadata is insufficient to prove cohort membership, SP-4K fails closed instead of reconstructing history from current data. Legacy `finals-ranking-source-v1` evidence also fails closed because it does not contain the SP-4J operator-contract provenance required by this projector.

SP-4K does **not** expose the projection through a controller or public profile, does not persist calculated placement, and does not publish podium, medal, award or record-holder claims. Public historical placement remains deferred until the identity-linked career read-model boundary is separately reviewed.

Detailed design: `docs/architecture/STACKER_IDENTITY_SP4K.md`.

## Historical snapshot rule

Permanent-profile updates must not rewrite historical competition registration snapshots. Competition results continue to reference competition-scoped participant identities exactly as they do today. Career aggregation resolves those historical rows through explicit identity links.

## Deferred governance and features

The following remain outside Stacker Identity v1 phases completed to date unless separately reviewed:

- profile ownership/editing;
- minors/guardian consent workflow;
- profile photos/media;
- athlete directory/search and search-engine indexing;
- Doubles/Relay permanent career statistics;
- WSSA-ID uniqueness enforcement;
- public historical placement/medal/podium publication;
- All-Around career standing;
- record governance;
- global or national ranking systems.

## Migration and deployment boundary

SP-1 introduced the identity schema. SP-2, SP-3A, SP-3B, SP-4A, SP-4B, SP-4C, SP-4D, SP-4E and SP-4F add no further schema migration. SP-4G introduces the isolated `FinalsRankingGovernance` persistence migration. SP-4H, SP-4I, SP-4J and SP-4K add no schema migration and only consume, assess, certify or project from that already-reviewed persistence boundary.

These development phases do **not** apply the SP-4G migration to production, deploy production code, or mutate production data.

Integration tests use isolated generated LocalDB databases where required. Characterization/governance phases execute or model existing production semantics without mutating production data.

**Hard production rule:** the existing production `web.config` must never be overwritten, replaced or regenerated by application deployment. Any later production release must back it up, record its hash, exclude it from the deployment payload, and verify the hash remains unchanged afterward.

## Phase sequence

- SP-0A: permanent identity domain foundation and invariants — complete.
- SP-0B: candidate matching/search boundary — complete.
- SP-0C: duplicate-resolution policy — complete.
- SP-1: persistent NADITrack ID issuance/storage — complete.
- SP-2: historical linking/backfill foundation — complete.
- SP-3A: privacy-safe career read model and Personal Bests — complete.
- SP-3B: public profile endpoint and presentation route — complete.
- SP-4A: finalized public Tournament History read model — complete.
- SP-4B: Tournament History public presentation and release-safety rule — complete.
- SP-4C: Career Progress / PB progression — complete.
- SP-4D: Finals Career aggregation and presentation — complete.
- SP-4E: Finals Ranking compatibility characterization — complete.
- SP-4F: Versioned Finals Ranking governance foundation — complete.
- SP-4G: Persisted Finals Ranking snapshot and activation boundary — complete.
- SP-4H: Operator Finals Ranking version activation — complete.
- SP-4I: Governed Finals v2 certification readiness — complete.
- SP-4J: Governed Finals v2 snapshot certification activation — complete.
- SP-4K: Immutable Historical Finals placement projection — implementation candidate.
