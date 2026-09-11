# NADITrack Stacker Identity v1

Status: SP-3A career profile read model and personal best foundation

## Purpose

NADITrack needs one permanent Sport Stacking identity per person so competition entries and results from different competitions can contribute to one career profile.

The core distinction is:

> A competition Stacker is an entry. A NADITrack Stacker is a person.

SP-0A defined the permanent identity contract. SP-0B added deterministic candidate matching. SP-0C added the explicit resolution gate. SP-1 introduced durable permanent identities, competition-entry links, secure public-ID issuance, and transactional storage. SP-2 added the safe historical-linking/backfill boundary. SP-3A adds the first privacy-safe, read-only career projection and finalized personal-best calculation while still stopping before HTTP/frontend publication.

## Identity authority

- `NadiTrackId` is the permanent public NADITrack athlete identifier.
- One person keeps the same NADITrack ID for life.
- The NADITrack ID is immutable once issued.
- Database primary keys remain internal and are never substituted for the public NADITrack ID.
- `WssaId` is optional external-reference metadata only. It is not the NADITrack primary identity.
- Existing competition-scoped `Stacker.WssaId` remains untouched for backward compatibility.

## Public identifier format and issuance

The v1 public format is `NDT-XXXXXXX`.

The seven-character body uses a restricted uppercase alphabet that excludes visually ambiguous `0`, `O`, `1`, `I`, and `L` characters. SP-1 issues identifiers with cryptographically strong randomness. The persistence service validates generated values, retries collisions up to 32 times, and relies on the database unique index as the final concurrency authority.

## Domain and persistence boundary

`SportStackerIdentity` represents the permanent person-level Sport Stacking identity in `dbo.SportStackerIdentity`.

`Stacker` remains the competition-scoped registration snapshot. It continues to own competition-specific data such as `StackerCode`, division, payment/check-in state, and the name/club/country values recorded for that competition. Permanent-profile work must not rewrite historical snapshots.

`StackerIdentityLink` associates one competition `Stacker` row with one permanent `SportStackerIdentity` and records provenance, resolution reason, review note, link time, and optional operator user ID.

Persisted constraints enforce unique `NadiTrackId`, at most one permanent identity link per competition Stacker, many competition Stackers per permanent identity, and restrictive foreign keys. WSSA uniqueness remains intentionally deferred until legacy-data audit confirms it is safe.

## Matching policy — SP-0B

Evidence strength is intentionally different from merge authority:

- exact explicitly supplied NADITrack ID: authoritative identity lookup;
- WSSA external ID: Strong candidate;
- exact normalized name + birth date: Strong candidate;
- email or phone: Strong candidate subject to operator review and privacy rules;
- name + country + club: Possible candidate;
- name alone: Possible candidate discovery only.

NADITrack must never auto-merge two identities based on name alone. A Strong candidate is still not authoritative unless the NADITrack ID itself was explicitly supplied and resolved exactly.

An explicit NADITrack ID is fail-closed: malformed -> `InvalidNadiTrackId`; valid but unknown -> `NadiTrackIdNotFound`; one exact ID -> authoritative selection; duplicate stored permanent IDs -> integrity exception.

## Resolution policy — SP-0C

`StackerIdentityResolutionPolicy` is the pure safety gate between matching and persistence. Only `Approved` decisions may persist.

An exact NADITrack ID match must resolve to `LinkExisting`; malformed or unknown explicit IDs cannot fall through to `CreateNew`; selected identities must be current candidates; Strong/Possible candidates require explicit confirmation; Possible/manual matches also require an audit note; and CreateNew in the presence of candidates requires a separate duplicate-override confirmation plus reason.

Approved provenance is recorded as `NADITRACK_ID`, `WSSA_ID`, `NAME_AND_BIRTH_DATE`, `EMAIL`, `PHONE`, `MANUAL`, or `CREATED_NEW` according to the reviewed decision.

## Transactional persistence — SP-1

`StackerIdentityPersistenceService` accepts only an SP-0C `Approved` decision. Persistence runs inside a serializable transaction. It rejects a second link for an already-linked competition Stacker, revalidates a selected persisted identity for `LinkExisting`, and atomically creates the permanent identity plus link for `CreateNew`.

New permanent identities are seeded from the competition registration snapshot but start with `IsPublicProfile = false`. Creating or linking a permanent identity never rewrites the competition snapshot.

## Historical linking/backfill — SP-2

`StackerIdentityBackfillService` separates read-only discovery from one-entry-at-a-time apply.

Discovery inventories unlinked Stackers, recomputes candidates against current permanent identity data, and classifies each item only as `ReviewRequired` or `NoCandidates`. There is deliberately no `AutoLink` state. Even one unique Strong candidate remains review-required. Candidate summaries omit email and phone values.

Apply reloads current data and recomputes the match result before any write, so a stale discovery report is informational only. The recomputed result passes through SP-0C and only an approved resolution reaches SP-1. Already-linked Stackers are returned idempotently without rewrite.

Detailed SP-2 notes are in `docs/architecture/STACKER_IDENTITY_SP2.md`.

## Career profile and personal best read model — SP-3A

`SportStackerCareerProfileService` introduces a read-only public projection keyed by the permanent NADITrack ID.

The public projection is available only when `SportStackerIdentity.IsPublicProfile` is explicitly true. Malformed IDs, unknown IDs, and private identities all return no public profile so public callers cannot use this boundary as a private-profile existence oracle.

Career appearances and PB candidates are restricted to publicly listed, finalized competitions: `IsPubliclyListed = true` and `Closed`, `Archived`, or already archived by timestamp. Active/provisional competitions do not silently become career records.

Career aggregation follows:

`SportStackerIdentity -> StackerIdentityLink -> Stacker -> CompetitionResult`

Only `Individual` results contribute to individual PBs. Both Prelims and Finals may contribute. PB timing mirrors the existing `BestResultEngine.js`: the lowest valid attempt (`> 0` and `< 999`) plus an applicable penalty (`> 0` and `< 999`). Scratch-only, malformed, or unsupported result rows are ignored rather than breaking the profile.

The public contract includes NADITrack ID, display name, country, optional club/region, finalized public competition count/date range, and PB provenance. It deliberately excludes birth date, email, phone, gender, WSSA ID, payment/check-in data, and other registration-only information.

SP-3A is read-only and adds no schema migration. It deliberately exposes no public HTTP endpoint and no frontend profile route yet. Detailed design is in `docs/architecture/STACKER_IDENTITY_SP3A.md`.

## Historical snapshot rule

Permanent-profile updates must not rewrite historical competition registration snapshots. If an athlete changes club later, the permanent profile may show the current club while an older competition continues to show the club registered for that event.

Competition results continue to reference the competition-scoped participant identity exactly as they do today. SP-3A resolves career data through identity links without altering those historical rows.

## Privacy

A public athlete profile is opt-in. `IsPublicProfile` never implies that private attributes are publishable.

Birth date, email, phone, parent/guardian information, home address, and other sensitive registration details must never become public merely because a career profile is enabled. Permanent identities are private by default. SP-3A additionally minimizes the public read model so those private fields are not present in its contract.

Profile ownership, editing, minors/guardian consent, photos, and final publication UX remain deferred to separately reviewed phases.

## Migration and deployment boundary

SP-1 introduced the identity schema. SP-2 and SP-3A introduce no additional schema migrations.

All integration testing uses isolated generated LocalDB databases in CI. No production migration or deployment is part of these development phases.

SP-3A deliberately does **not**:

- alter the existing `Stacker` HTTP contract;
- expose a career-profile HTTP endpoint;
- add a frontend profile page/route;
- alter profile visibility flags;
- rewrite historical Stackers/results;
- aggregate Doubles/Relay career statistics;
- introduce WSSA-ID uniqueness;
- publish private athlete attributes;
- deploy anything to production.

## Phase sequence

- SP-0A: permanent identity domain foundation and invariants — complete.
- SP-0B: existing/new stacker search and duplicate-matching workflow — complete.
- SP-0C: duplicate-resolution and identity-linking policy hardening — complete.
- SP-1: persistent NADITrack ID issuance and storage — complete.
- SP-2: historical competition Stacker discovery and explicit linking/backfill — complete.
- SP-3A: privacy-safe public career read model and finalized personal bests — current.
- SP-3B: reviewed public profile endpoint/route and presentation.
- SP-4: tournament history, progress, and broader historical aggregation.
