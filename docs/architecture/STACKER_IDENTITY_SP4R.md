# NADITrack Stacker Identity v1 — SP-4R Public Team Career Publication Contract

Status: implementation branch

## Purpose

SP-4R defines the privacy-safe publication contract for permanent Doubles and Timed Relay career history.

It consumes only the internal SP-4Q `IdentityLinkedTeamCareerReadModel` and answers one narrow question:

> Which SP-4Q team-career facts are safe to shape for a later public permanent profile?

SP-4R does **not** activate team history on the public API or browser profile. Controller/startup/UI integration remains a separate later phase.

## Public shape

`PublicTeamCareerPublication` contains:

- publication version;
- permanent public NADITrack ID;
- reviewed privacy-policy wording;
- chronological public team history.

Each `PublicTeamCareerPoint` contains only:

- competition key;
- competition name;
- competition date;
- participant type: Doubles or Timed Relay;
- stage: Prelims or Finals;
- supported event;
- factual result status;
- official best time when valid;
- raw best time when valid;
- applied penalty.

The fixed public privacy wording is:

> Verified team membership · teammate identities withheld

## Information deliberately withheld

The public team point does not expose:

- team code;
- registered-member count;
- Child/Parent or external-partner flag;
- teammate/member participant codes;
- teammate/member names;
- external parent/partner name;
- membership SHA-256;
- state/results/result revisions;
- Stacker/internal competition/result IDs;
- DOB;
- email;
- phone;
- WSSA ID;
- gender;
- placement or rank;
- medal, award, podium or record claims.

The source SP-4Q model may use protected membership details internally to establish that the permanent athlete really belonged to the team. SP-4R validates that evidence and then removes it from the public shape.

## Supported team contexts

SP-4R accepts only:

- `Doubles` or `Timed Relay`;
- `Prelims` or `Finals`;
- events `3-3-3`, `3-6-3`, or `Cycle`;
- Timed Relay only for `3-6-3`.

Competition key/name/date and the internal team reference must be present in the source.

The internal team reference is validated but not published.

## Membership-shape gate

SP-4R does not re-parse competition state. It validates the already-derived SP-4Q membership shape:

- Doubles may have one registered member only when SP-4Q has identified an external partner;
- Doubles with two registered members must not also claim an external partner;
- Timed Relay requires at least four registered members;
- Timed Relay cannot carry an external-partner shape.

These checks prevent an injected or inconsistent SP-4Q object from becoming public.

## Provenance gate

Every public team-history fact must carry SP-4Q evidence with:

- positive competition-state revision;
- positive competition-results revision;
- positive result revision;
- a registered-membership SHA-256 of exactly 64 hexadecimal characters.

The provenance is validated but not copied into the public payload.

SP-4R itself has no database dependency and does not query `CompetitionState`, `CompetitionResult`, `Stacker`, or identity-link tables.

## Result semantics

For `Valid`:

- raw best must be positive;
- applied penalty must be >= 0 and < 999;
- official best must be positive;
- official best must equal raw best + applied penalty.

For `Scratch`, `Missing`, or `Invalid`:

- official best must be null;
- raw best must be null;
- applied penalty must be zero.

Result status is normalized to canonical public casing.

## Ambiguous public context

SP-4Q retains team code internally. SP-4R intentionally removes it.

If more than one internal team entry for the same permanent athlete would collapse into the same public:

- competition;
- team type;
- stage;
- event

context, SP-4R fails closed instead of publishing two indistinguishable permanent facts.

Stable blocker:

- `public-team-career-context-ambiguous`.

A later phase may introduce an explicitly reviewed public multi-entry representation if the product requires it.

## Stable blockers

SP-4R uses:

- `public-team-career-source-not-safe` — invalid NADITrack ID, unsupported team context, or inconsistent membership shape;
- `public-team-career-evidence-not-safe` — missing/invalid provenance or result semantics;
- `public-team-career-context-ambiguous` — more than one internal team entry maps to the same minimized public context.

## Tests

`StackerPublicTeamCareerContractTests` covers:

- normal Doubles publication;
- Child/Parent publication with family linkage stripped;
- Timed Relay 3-6-3 publication;
- Scratch as factual no-time history;
- public JSON privacy stripping;
- malformed permanent ID fail-closed behavior;
- unsupported participant type/stage/event;
- relay external-partner rejection;
- invalid Doubles membership shape;
- malformed membership hash;
- non-positive result revision;
- inconsistent raw/penalty/official time;
- Scratch with fabricated time;
- ambiguous privacy-minimized public context.

The JavaScript static guard additionally proves:

- the contract version/policy are fixed;
- provenance checks remain present;
- sensitive/internal/award fields are absent from the public point;
- the contract has no persistence read path;
- controller, public profile DTO and startup do not activate SP-4R yet.

## Deliberately unchanged

SP-4R changes no:

- team membership parsing;
- team result entry;
- competition state;
- existing public profile response;
- browser profile UI;
- startup/DI wiring;
- database schema;
- production deployment;
- production database data;
- `web.config`;
- placement/rank/medal/award governance.

## Next boundary

After SP-4R is proven and merged, a separately reviewed phase may integrate the SP-4R publication into the permanent public profile.

That integration phase must preserve the fail-closed behavior and must not expose SP-4Q membership internals.

## Deployment safety

SP-4R performs **no production deployment**, applies **no production database migration**, and writes **no production data**.

The production `web.config` remains untouched. The production-release workflow remains separately invoked and manually gated.
