# NADITrack Stacker Identity v1

Status: SP-0C resolution foundation

## Purpose

NADITrack needs one permanent Sport Stacking identity per person so competition entries and results from different competitions can contribute to one career profile.

The core distinction is:

> A competition Stacker is an entry. A NADITrack Stacker is a person.

SP-0A defined the permanent identity contract. SP-0B added deterministic existing/new-stacker candidate matching. SP-0C adds the explicit resolution gate between a match result and any future persistence write. Current competition persistence and public APIs remain unchanged.

## Identity authority

- `NadiTrackId` is the permanent public NADITrack athlete identifier.
- One person keeps the same NADITrack ID for life.
- The NADITrack ID is immutable once issued.
- Database primary keys remain internal and are never substituted for the public NADITrack ID.
- `WssaId` is optional external-reference metadata only. It is not the NADITrack primary identity.
- Existing competition-scoped `Stacker.WssaId` remains untouched during SP-0A/SP-0B/SP-0C for backward compatibility.

## Public identifier format

The v1 public format is:

`NDT-XXXXXXX`

The seven-character body uses a restricted uppercase alphabet that excludes visually ambiguous `0`, `O`, `1`, `I`, and `L` characters. IDs are intended to be randomly generated rather than sequential/enumerable. Generation and collision handling are deferred to SP-1; SP-0A defines validation and normalization only.

## Domain boundary

`SportStackerIdentity` represents the permanent person-level Sport Stacking identity.

`Stacker` remains the existing competition-scoped registration snapshot. It continues to own competition-specific data such as `StackerCode`, division, payment/check-in state, and the name/club/country values recorded for that competition.

`StackerIdentityLink` associates a competition `Stacker` row with one permanent `SportStackerIdentity` and records link provenance.

Future persistence constraints should enforce:

- unique `SportStackerIdentity.NadiTrackId`;
- at most one permanent identity per competition `Stacker` row;
- many competition `Stacker` rows may link to the same permanent identity;
- optional WSSA uniqueness must be introduced only after legacy-data audit confirms it is safe.

## Registration workflow target

When an organizer adds a stacker to a competition:

1. Search for an existing NADITrack identity first.
2. Exact NADITrack ID is authoritative for selecting the requested identity.
3. Other evidence can produce duplicate candidates or strong suggestions but must not silently merge people.
4. Resolve the match result explicitly: link an existing identity or deliberately continue as a new person.
5. If an existing identity is selected, create a new competition `Stacker` snapshot linked to that identity.
6. If the organizer chooses New Stacker, duplicate candidates require a separate override confirmation and audit reason.
7. If no existing person is selected, create one permanent identity, issue one NADITrack ID, create the competition `Stacker` snapshot, and link them atomically.

The target UX is:

`Add Stacker -> Existing NADITrack Stacker / New Stacker -> duplicate check -> explicit resolution -> competition entry`

SP-0B implements the pure matching/search decision boundary. SP-0C implements the pure resolution/approval boundary. SP-1 will implement issuance and persistence. SP-2 will address historical linking/backfill.

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

`StackerIdentityMatcher` is a pure in-memory policy component. Persistence adapters can later supply identity candidates without moving matching rules into controllers or SQL queries.

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
- no evidence means the New Stacker path may continue to identity creation in a later persistence phase.

Name comparison is case-insensitive and whitespace-normalized. Email comparison is case-insensitive. Phone matching ignores formatting characters and requires at least seven digits on both sides.

SP-0B does not write, merge, link, issue IDs, or mutate registrations. It only answers: exact existing identity, invalid/unknown explicit ID, candidate list requiring confirmation, or no candidate.

## SP-0C resolution boundary

`StackerIdentityResolutionPolicy` is the pure safety gate that consumes an SP-0B match result plus explicit operator intent. Only a decision with status `Approved` may be consumed by the future SP-1 persistence layer.

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

SP-0C does not itself create a `StackerIdentityLink`, update a profile, merge people, issue an identifier, or write to a database. It produces a decision only.

## Historical snapshot rule

Permanent-profile updates must not rewrite historical competition registration snapshots.

Example: if an athlete changes club in 2028, the permanent profile may show the current club while a 2026 competition continues to show the club registered in 2026.

Competition results continue to reference the competition-scoped participant identity exactly as they do today. Career aggregation will resolve those entries through the identity link in a later phase.

## Privacy

A public athlete profile is opt-in. The existence of `IsPublicProfile` does not make private attributes public.

Birth date, email, phone, parent/guardian information, home address, and other sensitive registration details must never become public merely because a career profile is enabled. Public/minor-profile policy is deferred to the dedicated profile/privacy phase.

## SP-0A/SP-0B/SP-0C persistence boundary

SP-0A intentionally does **not** register or persist permanent identities or links. SP-0B and SP-0C preserve that same persistence boundary while adding matching and resolution policy only.

These foundation phases intentionally do **not**:

- register `SportStackerIdentity` or `StackerIdentityLink` in `StackMeetDbContext`;
- add or modify EF Core migrations;
- modify `Stacker` or `StackerDtos`;
- modify `StackersController`;
- issue real NADITrack IDs;
- create or backfill identity rows;
- change public results;
- deploy anything to production.

This keeps the current Sport Stacking competition behavior unchanged while the permanent identity, matching, and resolution contracts are reviewed.

## Phase sequence

- SP-0A: permanent identity domain foundation and invariants.
- SP-0B: existing/new stacker search and duplicate-matching workflow.
- SP-0C: duplicate-resolution and identity-linking policy hardening.
- SP-1: persistent NADITrack ID issuance and storage.
- SP-2: link existing/historical competition stackers to permanent identities.
- SP-3: public career profile and personal bests.
- SP-4: tournament history, progress, and historical aggregation.
