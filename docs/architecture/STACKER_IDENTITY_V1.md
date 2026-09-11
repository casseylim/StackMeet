# NADITrack Stacker Identity v1

Status: SP-4D Finals career aggregation candidate

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

SP-0B defines deterministic candidate matching. SP-0C defines the explicit resolution gate. Candidate strength is not merge authority: name-only matches are never auto-merged, and even Strong candidates require the reviewed decision boundary unless an explicit NADITrack ID resolved authoritatively.

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
- historical placement/medal/podium publication;
- All-Around career standing;
- record governance;
- global or national ranking systems.

## Migration and deployment boundary

SP-1 introduced the identity schema. SP-2, SP-3A, SP-3B, SP-4A, SP-4B, SP-4C and SP-4D add no further schema migration.

Integration tests use isolated generated LocalDB databases. These development phases do not deploy production code or mutate production data.

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
- SP-4D: Finals Career aggregation and presentation — implementation candidate.
