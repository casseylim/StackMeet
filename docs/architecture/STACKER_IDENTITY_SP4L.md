# NADITrack Stacker Identity v1 — SP-4L Identity-Linked Finals Placement Career Read Model

Status: complete

## Purpose

SP-4L adds the first server-owned career read model that associates immutable SP-4K Individual Finals placement evidence with a permanent NADITrack identity.

It answers a deliberately bounded question:

> Given one permanent identity and one or more explicitly reviewed SP-4K placement scopes, which finalized/public historical placement rows belong to that identity?

SP-4L does **not** decide which category/gender placement scope should become the athlete's canonical public career claim. It therefore does not publish placement on the existing public profile.

## Authority chain

SP-4L preserves four separate authorities:

1. `SportStackerIdentity` is the permanent person identity.
2. `StackerIdentityLink` is the reviewed association to a competition-scoped registration entry.
3. the linked `Stacker.StackerCode` is used only as the participant-code bridge into that competition;
4. `FinalsHistoricalPlacementProjectionService` remains the sole historical placement calculator and immutable snapshot authority.

The service never matches a person by name and never calculates rank from current `CompetitionResult`, current `CompetitionState`, current Stacker demographic fields, or permanent-profile attributes.

## Explicit scope remains mandatory

SP-4K proved that a numeric placement is contextual. Category and gender filtering happen before ranking, and historical division must come from the competition snapshot.

SP-4L therefore accepts `IdentityLinkedFinalsPlacementSelection` inputs containing:

- internal competition id;
- one complete `FinalsHistoricalPlacementScope`.

The scope still carries:

- participant type;
- explicit competition-snapshot division;
- event;
- category;
- gender.

SP-4L does not invent a default scope and does not collapse different scopes into one bare placement claim. If the same immutable Finals result is requested under two valid scopes and receives two different placements, both facts retain their complete scope.

Exact duplicate competition/scope selections are de-duplicated in the read model.

## Public-eligibility boundary

Although SP-4L is not exposed publicly, it already preserves the established public-career eligibility rules:

- the permanent identity must have `IsPublicProfile = true`;
- malformed, unknown and private NADITrack IDs share the same null/not-found boundary;
- the competition must be `IsPubliclyListed = true`;
- the competition must be Closed, Archived, or have a non-null archive timestamp.

An active or non-public competition is skipped before SP-4K projection is attempted.

## Identity association

For each eligible requested competition, SP-4L reads the approved `StackerIdentityLink` and its linked competition-scoped Stacker entry.

Only these linked registration fields are consumed:

- competition id;
- participant/Stacker code.

Mutable registration name, gender, birth date, WSSA ID, custom division and other current profile fields are not historical ranking authority.

If one permanent identity has more than one reviewed Stacker link for the same eligible competition, SP-4L fails closed with:

- `identity-link-ambiguous`

It does not choose by name, row order, current demographics or fastest result.

## Immutable placement dependency

For every eligible linked selection, SP-4L calls the existing SP-4K `FinalsHistoricalPlacementProjectionService`.

SP-4K therefore remains responsible for:

- governed-v2 certification;
- source-v2 snapshot requirement;
- snapshot SHA-256 integrity;
- source revision provenance;
- operator-contract provenance;
- competition-time participant metadata;
- category/gender filtering;
- official-best/tie-key computation;
- Scratch/Missing/Invalid classification;
- competition rank assignment and shared-rank behavior.

SP-4L copies the matching linked participant row into the career read model. It does not recalculate placement.

If the explicit category/gender scope legitimately excludes the linked participant, no career fact is fabricated for that selection.

## Read-model contract

`IdentityLinkedFinalsPlacementCareerReadModel` contains:

- permanent `NadiTrackId`;
- chronological `History`.

Each `IdentityLinkedFinalsPlacementCareerPoint` contains:

- competition key;
- competition name;
- competition date;
- complete explicit SP-4K scope;
- SP-4K result status;
- official best time when valid;
- nullable placement;
- shared-placement fact;
- immutable projection evidence provenance.

The evidence record retains:

- projection version;
- ranking rule version;
- snapshot schema version;
- operator-contract version;
- snapshot SHA-256;
- source state revision;
- source results revision;
- snapshot capture time.

The output deliberately excludes:

- internal competition id;
- Stacker id;
- participant code;
- participant name;
- result public id;
- birth date;
- email;
- phone;
- WSSA ID.

Gender can still appear as part of the explicit placement **scope**, which is one reason SP-4L is not yet wired to the public profile.

## Non-valid Finals

SP-4L preserves SP-4K status semantics.

Scratch, Missing and Invalid rows may be retained as identity-linked Finals facts with:

- `Placement = null`;
- `OfficialBestTime = null` when SP-4K has no valid official time.

The read model never invents a placement for a non-valid Finals result.

## Compatibility and privacy gate

SP-4L intentionally adds:

- no controller route;
- no public-profile field;
- no browser rendering;
- no application-startup activation;
- no public placement publication.

Before placement is shown publicly, a later phase must review the publication policy for category/gender scope and how much of the explicit scope can be displayed without violating the established privacy contract.

The existing SP-4D public `FinalsCareer` remains unchanged and placement-free.

## Tests

`StackerFinalsPlacementCareerReadModelTests` uses an isolated generated SQL Server LocalDB database and verifies:

- permanent NADITrack identity linkage across multiple certified competitions;
- explicit scope is retained with every placement;
- the same immutable result can have different placement under different gender scopes;
- a different historical division can be represented in another competition;
- Scratch is retained without fabricated placement/time;
- active and non-public competitions cannot enter;
- a category scope that excludes the athlete cannot fabricate a placement;
- mutable current Stacker name/gender/division fields cannot rewrite historical placement;
- mutable current CompetitionResult data cannot rewrite historical placement after certification;
- private/malformed identities preserve the public not-found boundary;
- duplicate requested scopes are de-duplicated;
- ambiguous same-competition identity links fail closed;
- the read-model contract excludes participant/internal/sensitive identifiers;
- the existing public Finals career contract remains placement-free.

A JavaScript static guard additionally proves that SP-4L:

- delegates rank calculation to SP-4K;
- does not read current CompetitionResult or CompetitionState ranking sources;
- does not use mutable Stacker name/demographic fields for matching;
- is not referenced by the public profile controller or application startup.

## Deliberately unchanged

SP-4L changes no:

- Finals scoring or ranking rule;
- qualification logic;
- competition result row;
- competition state;
- identity resolution/persistence rule;
- public profile API contract;
- public profile UI;
- Prelims behavior;
- All-Around behavior;
- Doubles/Relay career ranking;
- medal, award, podium or record claim;
- Chess or other activity-module behavior;
- database schema.

## Next boundary

After SP-4L is proven and merged, a later separately reviewed phase may define a **public Finals placement publication contract**.

That phase must explicitly decide which category/gender scope is appropriate for public permanent career presentation and prove that the chosen presentation does not expose disallowed personal attributes or imply medal/award claims that have not been governed.

SP-4L itself does not make that decision.

## Deployment safety

SP-4L performs **no production deployment**, applies **no production database migration**, and writes **no production data** as part of this development phase.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved artifacts, and verify the production `web.config` hash remains unchanged afterward.
