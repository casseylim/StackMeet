# NADITrack Stacker Identity v1

Status: SP-2 historical identity linking foundation

## Purpose

NADITrack needs one permanent Sport Stacking identity per person so competition entries and results from different competitions can contribute to one career profile.

The core distinction is:

> A competition Stacker is an entry. A NADITrack Stacker is a person.

SP-0A defined the permanent identity contract. SP-0B added deterministic candidate matching. SP-0C added the explicit resolution gate. SP-1 introduced durable permanent identities, competition-entry links, secure public-ID issuance, and transactional storage. SP-2 adds the safe historical-linking/backfill boundary for existing competition Stackers while preserving the existing public competition Stacker API.

## Identity authority

- `NadiTrackId` is the permanent public NADITrack athlete identifier.
- One person keeps the same NADITrack ID for life.
- The NADITrack ID is immutable once issued.
- Database primary keys remain internal and are never substituted for the public NADITrack ID.
- `WssaId` is optional external-reference metadata only. It is not the NADITrack primary identity.
- Existing competition-scoped `Stacker.WssaId` remains untouched for backward compatibility.

## Public identifier format and issuance

The v1 public format is:

`NDT-XXXXXXX`

The seven-character body uses a restricted uppercase alphabet that excludes visually ambiguous `0`, `O`, `1`, `I`, and `L` characters.

SP-1 issues identifiers with `CryptographicNadiTrackIdGenerator`, using cryptographically strong randomness. IDs are random rather than sequential/enumerable. The persistence service validates every generated value against `NadiTrackIdRules`, checks for a collision, retries up to 32 times, and relies on the database unique index as the final concurrency authority.

## Domain and persistence boundary

`SportStackerIdentity` represents the permanent person-level Sport Stacking identity and is persisted in `dbo.SportStackerIdentity`.

`Stacker` remains the existing competition-scoped registration snapshot. It continues to own competition-specific data such as `StackerCode`, division, payment/check-in state, and the name/club/country values recorded for that competition. Permanent-profile work must not rewrite those historical snapshots.

`StackerIdentityLink` is persisted in `dbo.StackerIdentityLink`. It associates one competition `Stacker` row with one permanent `SportStackerIdentity` and records provenance, resolution reason, review note, link time, and optional operator user ID.

The persisted constraints enforce:

- unique `SportStackerIdentity.NadiTrackId`;
- at most one permanent identity link per competition `Stacker` row;
- many competition `Stacker` rows may link to the same permanent identity;
- restrictive foreign keys prevent silent cascade loss of identity history;
- WSSA uniqueness remains intentionally deferred until legacy-data audit confirms it is safe.

## Matching policy — SP-0B

Evidence strength is intentionally different from merge authority:

- exact explicitly supplied NADITrack ID: authoritative identity lookup;
- WSSA external ID: Strong candidate;
- exact normalized name + birth date: Strong candidate;
- email or phone: Strong candidate subject to operator review and privacy rules;
- name + country + club: Possible candidate;
- name alone: Possible candidate discovery only.

NADITrack must never auto-merge two identities based on name alone. A Strong candidate is still not authoritative unless the NADITrack ID itself was explicitly supplied and resolved exactly.

An explicit NADITrack ID is handled fail-closed:

- malformed ID -> `InvalidNadiTrackId`;
- valid but unknown ID -> `NadiTrackIdNotFound`;
- one exact ID -> `ExactNadiTrackIdMatch` and authoritative selection;
- duplicate stored permanent IDs -> integrity exception.

## Resolution policy — SP-0C

`StackerIdentityResolutionPolicy` is the pure safety gate between matching and persistence.

Only a decision with status `Approved` may be persisted. The rules remain:

- an exact NADITrack ID match must resolve to `LinkExisting`;
- malformed or unknown explicit IDs cannot silently fall through to `CreateNew`;
- a selected existing identity must actually be present in the current match result;
- Strong and Possible non-authoritative candidates require explicit `CandidateConfirmed` confirmation;
- a Possible/manual match additionally requires a nonblank review note;
- if no candidates exist, `CreateNew` may proceed normally;
- if candidates exist, `CreateNew` requires the separate duplicate-override confirmation plus a nonblank reason;
- confirmation of an existing candidate and permission to create a duplicate are intentionally separate flags.

Approved provenance is recorded as `NADITRACK_ID`, `WSSA_ID`, `NAME_AND_BIRTH_DATE`, `EMAIL`, `PHONE`, `MANUAL`, or `CREATED_NEW` according to the reviewed decision.

## Transactional persistence — SP-1

`StackerIdentityPersistenceService` accepts only an SP-0C `Approved` decision. It rejects blocked, confirmation-required, malformed, inconsistent, or unrecognized approval states before writing.

Persistence runs inside a serializable database transaction. It loads the target competition Stacker, rejects a second link for an already-linked entry, revalidates the selected persisted identity for `LinkExisting`, and atomically creates the permanent identity plus link for `CreateNew`.

New permanent identities are seeded from the competition registration snapshot but start with `IsPublicProfile = false`. Creating or linking a permanent identity never rewrites the competition snapshot.

The persisted link includes:

- `MatchMethod`;
- `ResolutionReasonCode`;
- `ResolutionNote` when required;
- `LinkedAt`;
- optional `LinkedByUserId`.

SP-1 recognizes only the reviewed SP-0C approval reason codes and therefore does not provide a second bypass around the resolution policy.

## Historical linking/backfill — SP-2

SP-2 adds `StackerIdentityBackfillService` as a review/apply foundation for existing and historical competition Stackers.

Historical backfill is intentionally split into two operations.

### Discovery is read-only

`DiscoverAsync` inventories unlinked Stackers and recomputes candidates against the current permanent identity data. It may be scoped to one competition or across competitions and is bounded to at most 500 returned items.

Each unlinked Stacker is classified only as:

- `ReviewRequired` when one or more Strong/Possible candidates exist; or
- `NoCandidates` when no current candidate exists.

There is deliberately no `AutoLink` state. Even one unique Strong candidate remains review-required.

Discovery does not create identities or links. Candidate summaries intentionally omit email and phone values even when those values contributed to matching evidence.

### Apply is one entry at a time

`ApplyAsync` accepts one competition Stacker decision at a time. Before any write, it reloads current persisted data and recomputes the match result. A stale discovery report is therefore informational only and cannot be replayed as authority.

The recomputed result is passed through SP-0C. Only an approved resolution is passed to SP-1.

This means historical linking preserves all existing safeguards:

- explicit valid existing NADITrack ID is authoritative;
- unknown or malformed explicit NADITrack ID fails closed;
- Strong/Possible candidates require confirmation;
- Possible/manual links require a review note;
- Create New despite candidates requires duplicate override plus note;
- already-linked Stackers are surfaced as `AlreadyLinked` and are not rewritten;
- no-candidate historical entries may deliberately create a new permanent identity only through the normal SP-0C/SP-1 path.

Detailed SP-2 design notes are in `docs/architecture/STACKER_IDENTITY_SP2.md`.

## Historical snapshot rule

Permanent-profile updates must not rewrite historical competition registration snapshots.

Example: if an athlete changes club in 2028, the permanent profile may show the current club while a 2026 competition continues to show the club registered in 2026.

Competition results continue to reference the competition-scoped participant identity exactly as they do today. Career aggregation will resolve those entries through the identity link in a later phase.

## Privacy

A public athlete profile is opt-in. The existence of `IsPublicProfile` does not make private attributes public.

Birth date, email, phone, parent/guardian information, home address, and other sensitive registration details must never become public merely because a career profile is enabled. Permanent identities are private by default. Public/minor-profile policy remains deferred to the dedicated profile/privacy phase.

## Migration and deployment boundary

SP-1 introduced the identity schema. SP-2 introduces no additional schema migration in this slice; it works through the existing SP-1 tables and constraints.

All migration/integration testing is performed only against isolated generated LocalDB databases in CI. No production migration is applied by SP-2 development or merge.

SP-2 deliberately does **not**:

- add `NadiTrackId` to the existing `Stacker` DTO/controller contract;
- expose a public or organizer backfill HTTP endpoint yet;
- add a frontend bulk-backfill screen;
- auto-link Strong candidates;
- auto-create identities for every unmatched historical Stacker;
- rewrite historical Stacker snapshots;
- introduce WSSA-ID uniqueness;
- publish private athlete attributes;
- aggregate career results;
- change public results;
- deploy anything to production.

## Phase sequence

- SP-0A: permanent identity domain foundation and invariants — complete.
- SP-0B: existing/new stacker search and duplicate-matching workflow — complete.
- SP-0C: duplicate-resolution and identity-linking policy hardening — complete.
- SP-1: persistent NADITrack ID issuance and storage — complete.
- SP-2: historical competition Stacker discovery and explicit linking/backfill — current.
- SP-3: public career profile and personal bests.
- SP-4: tournament history, progress, and historical aggregation.
