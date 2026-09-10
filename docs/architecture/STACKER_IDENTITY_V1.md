# NADITrack Stacker Identity v1

Status: SP-1 persistent identity issuance and storage

## Purpose

NADITrack needs one permanent Sport Stacking identity per person so competition entries and results from different competitions can contribute to one career profile.

The core distinction is:

> A competition Stacker is an entry. A NADITrack Stacker is a person.

SP-0A defined the permanent identity contract. SP-0B added deterministic existing/new-stacker candidate matching. SP-0C added the explicit resolution gate. SP-1 introduces the first durable persistence boundary: permanent identities, competition-entry links, secure public-ID issuance, and transactional storage. The existing public competition Stacker API remains unchanged.

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

SP-1 issues identifiers with `CryptographicNadiTrackIdGenerator`, using `RandomNumberGenerator.GetInt32` for every body character. IDs are therefore random rather than sequential/enumerable. The persistence service validates every generated value against `NadiTrackIdRules`, checks for an existing collision, and retries up to 32 times. The database unique index on `SportStackerIdentity.NadiTrackId` remains the final uniqueness authority under concurrency.

## Domain and persistence boundary

`SportStackerIdentity` represents the permanent person-level Sport Stacking identity and is persisted in `dbo.SportStackerIdentity`.

`Stacker` remains the existing competition-scoped registration snapshot. It continues to own competition-specific data such as `StackerCode`, division, payment/check-in state, and the name/club/country values recorded for that competition. SP-1 adds only an internal, JSON-ignored navigation to its identity link; it does not add `NadiTrackId` to the competition Stacker model or public DTO.

`StackerIdentityLink` is persisted in `dbo.StackerIdentityLink`. It associates a competition `Stacker` row with one permanent `SportStackerIdentity` and records link provenance, the SP-0C resolution reason code, an optional review note, link time, and optional operator user ID.

SP-1 database constraints enforce:

- unique `SportStackerIdentity.NadiTrackId`;
- at most one `StackerIdentityLink` per competition `Stacker` row;
- many competition `Stacker` rows may link to the same permanent identity;
- restrictive foreign keys from the link to both permanent identity and competition Stacker, preventing silent cascade loss of identity history;
- optional WSSA uniqueness is intentionally **not** introduced yet; that remains blocked until legacy-data audit confirms it is safe.

## Registration workflow target

When an organizer adds a stacker to a competition:

1. Search for an existing NADITrack identity first.
2. Exact NADITrack ID is authoritative for selecting the requested identity.
3. Other evidence can produce duplicate candidates or strong suggestions but must not silently merge people.
4. Resolve the match result explicitly: link an existing identity or deliberately continue as a new person.
5. Only an SP-0C `Approved` decision may cross the SP-1 persistence boundary.
6. If an existing identity is selected, link the competition `Stacker` snapshot to that persisted identity.
7. If the organizer deliberately creates a new person, issue a new permanent NADITrack ID and persist the identity and link atomically.

The target UX remains:

`Add Stacker -> Existing NADITrack Stacker / New Stacker -> duplicate check -> explicit resolution -> competition entry`

SP-1 supplies the persistence service behind that target workflow. It does not yet expose a new public HTTP endpoint or change the current `StackersController` create/update behavior. A later integration phase can wire the reviewed flow into the user interface and API without weakening these rules.

## Matching and duplicate-detection policy

Evidence strength is intentionally different from merge authority:

- exact NADITrack ID: authoritative identity lookup;
- unique WSSA external ID: strong candidate;
- exact normalized name + birth date: strong candidate;
- email or phone: strong candidate subject to operator review and privacy rules;
- name + country + club: possible candidate;
- name alone: candidate discovery only.

NADITrack must never auto-merge two identities based on name alone. Strong candidates other than an explicitly supplied NADITrack ID remain subject to confirmation until a later reviewed matching policy says otherwise.

Link provenance values record how an association was established; they are audit metadata and do not themselves authorize an automatic merge.

## SP-0B matching boundary

`StackerIdentityMatcher` is a pure in-memory policy component. Persistence adapters can supply identity candidates without moving matching rules into controllers or SQL queries.

An explicit NADITrack ID is handled fail-closed:

- malformed ID -> `InvalidNadiTrackId`;
- valid but unknown ID -> `NadiTrackIdNotFound`;
- one exact ID -> `ExactNadiTrackIdMatch` and authoritative selection;
- duplicate stored permanent IDs -> integrity exception.

When no NADITrack ID is supplied, the matcher produces ranked candidates only:

- WSSA ID, exact name + birth date, email, and phone are `Strong` evidence;
- exact name with matching country + club is `Possible` evidence;
- exact name alone is `Possible` evidence;
- every non-authoritative candidate requires operator confirmation;
- no evidence permits the New Stacker path to continue to resolution.

Name comparison is case-insensitive and whitespace-normalized. Email comparison is case-insensitive. Phone matching ignores formatting characters and requires at least seven digits on both sides.

## SP-0C resolution boundary

`StackerIdentityResolutionPolicy` is the pure safety gate that consumes an SP-0B match result plus explicit operator intent. Only a decision with status `Approved` may be consumed by SP-1.

The resolution rules are deliberately fail-closed:

- an exact NADITrack ID match must resolve to `LinkExisting`; it cannot be overridden into `CreateNew`;
- malformed or unknown explicitly supplied NADITrack IDs remain blocked and cannot silently fall through to New Stacker;
- an existing candidate can be linked only if the selected NADITrack ID was actually returned by the matcher;
- every `Strong` or `Possible` non-authoritative candidate requires explicit `CandidateConfirmed` confirmation;
- a `Possible` match additionally requires a nonblank review/audit note;
- if there are no candidates, `CreateNew` may proceed normally;
- if one or more candidates exist, `CreateNew` requires the separate `CreateNewOverrideConfirmed` flag plus a nonblank reason;
- `CandidateConfirmed` and `CreateNewOverrideConfirmed` are intentionally separate so confirming a candidate can never accidentally authorize creation of a duplicate permanent identity;
- duplicate or invalid permanent NADITrack IDs in candidate data are treated as integrity failures.

Approved link provenance is selected from the strongest available auditable evidence (`NADITRACK_ID`, `WSSA_ID`, `NAME_AND_BIRTH_DATE`, `EMAIL`, or `PHONE`). A weaker operator-reviewed candidate uses `MANUAL`; a genuinely new identity uses `CREATED_NEW`.

## SP-1 transactional persistence boundary

`StackerIdentityPersistenceService` accepts only an SP-0C `Approved` decision. It rejects blocked, confirmation-required, malformed, inconsistent, or unknown approval states before writing anything.

Persistence runs inside a serializable database transaction. The service first locks the logical operation through the transaction, loads the target competition Stacker, and rejects a second identity link for an already-linked Stacker. For `LinkExisting`, it reloads the selected permanent identity by internal key and verifies the public NADITrack ID still matches the reviewed decision. For `CreateNew`, it validates the registration snapshot, issues a random NADITrack ID, creates the permanent identity, and creates the identity link in the same transaction.

The first permanent profile is seeded from the competition registration snapshot, but `IsPublicProfile` is always `false` by default. Creating the permanent identity does not rewrite the competition snapshot.

The link persists enough provenance to audit the decision later:

- `MatchMethod` — `NADITRACK_ID`, `WSSA_ID`, `NAME_AND_BIRTH_DATE`, `EMAIL`, `PHONE`, `MANUAL`, or `CREATED_NEW` as approved by policy;
- `ResolutionReasonCode` — the stable SP-0C approval reason;
- `ResolutionNote` — required for manual/possible-match review and for Create New despite duplicate candidates;
- `LinkedAt` and optional `LinkedByUserId`.

The persistence service recognizes only the reviewed SP-0C approval reason codes:

- `APPROVED_EXACT_NADITRACK_LINK`;
- `APPROVED_CONFIRMED_CANDIDATE_LINK`;
- `APPROVED_NO_DUPLICATE_CANDIDATES`;
- `APPROVED_CREATE_NEW_DESPITE_CANDIDATES`.

This prevents callers from manufacturing arbitrary "approved" reason strings to bypass the policy boundary.

## Historical snapshot rule

Permanent-profile updates must not rewrite historical competition registration snapshots.

Example: if an athlete changes club in 2028, the permanent profile may show the current club while a 2026 competition continues to show the club registered in 2026.

Competition results continue to reference the competition-scoped participant identity exactly as they do today. Career aggregation will resolve those entries through the identity link in a later phase.

## Privacy

A public athlete profile is opt-in. The existence of `IsPublicProfile` does not make private attributes public.

Birth date, email, phone, parent/guardian information, home address, and other sensitive registration details must never become public merely because a career profile is enabled. SP-1 creates every permanent identity with the public-profile flag disabled. Public/minor-profile policy remains deferred to the dedicated profile/privacy phase.

## Migration and deployment boundary

SP-0A intentionally did **not** persist permanent identities; SP-0B and SP-0C stayed pure. SP-1 deliberately supersedes that temporary no-persistence boundary by committing the first identity schema and persistence service.

The SP-1 migration is a repository artifact only during this phase. It is validated against an isolated generated LocalDB database in CI and is **not applied to production** as part of SP-1 development or merge.

SP-1 deliberately does **not**:

- add `NadiTrackId` to `Stacker`, `StackerDtos`, or the existing Stacker HTTP contract;
- alter current `StackersController` registration behavior;
- backfill or automatically link historical competition stackers;
- make WSSA ID unique;
- publish private athlete attributes;
- change public results;
- deploy anything to production.

## Phase sequence

- SP-0A: permanent identity domain foundation and invariants — complete.
- SP-0B: existing/new stacker search and duplicate-matching workflow — complete.
- SP-0C: duplicate-resolution and identity-linking policy hardening — complete.
- SP-1: persistent NADITrack ID issuance and storage — current.
- SP-2: link existing/historical competition stackers to permanent identities.
- SP-3: public career profile and personal bests.
- SP-4: tournament history, progress, and historical aggregation.
