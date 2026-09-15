# NADITrack Stacker Identity v1 — SP-4M Public Finals Placement Publication Contract

Status: implementation candidate

## Purpose

SP-4M defines the first privacy-safe **publication contract** for permanent Individual Finals placement facts.

It consumes only the server-owned SP-4L identity-linked placement read model and answers one bounded question:

> Which immutable SP-4L placement facts have a scope and provenance that are safe to shape for a later public permanent profile?

SP-4M deliberately does **not** activate placement on the public API or browser profile. Controller/startup/UI wiring remains a separate later phase.

## Canonical publication scope

SP-4K proved that placement is contextual because category and gender filters are applied before ranking. SP-4L therefore retains the complete immutable scope.

SP-4M permits only this canonical publication policy:

- `participantType = Individual`;
- an explicit competition-time division other than blank/`all`;
- event = `3-3-3`, `3-6-3`, or `Cycle`;
- `category = mixed`;
- `gender = all`.

The public wording for this policy is:

> Competition-time division · mixed category · no additional gender filter

`gender = all` is described as **no additional gender filter**, not as a claim that the underlying division is gender-neutral. A historical division may itself be gender-specific.

## Why mixed + no additional gender filter

A permanent public profile already excludes gender and Special status.

Publishing a `normal` or `special` placement would allow Special-status inference. Publishing an `M` or `F` placement would expose the athlete's gender directly.

SP-4M therefore fails closed for those scopes rather than selectively hiding the scope label while publishing its rank.

The fixed mixed/no-additional-gender-filter policy prevents category/gender selection from becoming a permanent personal attribute in the public-profile contract.

## Raw historical division remains internal

SP-4E established that a bare numeric rank is insufficient: division is part of the immutable ranking scope.

However, some competition division labels themselves contain gendered wording such as `Male` or `Female`. Copying those labels into the permanent profile could indirectly reintroduce an attribute that the established public profile deliberately omits.

SP-4M therefore keeps the exact historical division in the internal SP-4L authority chain but does **not** copy the raw division label to `PublicFinalsPlacementPoint`.

The permanent public fact remains contextualized as placement within the athlete's **competition-time division**, and the competition key/name/date remain present so the fact is not detached from its tournament provenance.

This is privacy minimization, not a change to ranking authority. SP-4K/SP-4L still retain the exact division used to calculate the placement.

## Publication contract

`PublicFinalsPlacementCareerPublication` contains:

- publication contract version;
- permanent public NADITrack ID;
- fixed cohort-policy description;
- chronological placement history.

Each `PublicFinalsPlacementPoint` contains only:

- competition key;
- competition name;
- competition date;
- event;
- Finals result status;
- immutable official best time when valid;
- nullable placement;
- factual shared-placement membership.

The public placement point intentionally excludes:

- raw division label;
- category field;
- gender field;
- complete SP-4K/SP-4L scope object;
- snapshot hash/revision/capture internals;
- participant code/name from the competition snapshot;
- Stacker/internal competition/result IDs;
- birth date;
- email;
- phone;
- WSSA ID;
- medal, award, podium or record claims.

## Immutable provenance gate

SP-4M accepts only SP-4L points carrying complete immutable governed-v2 provenance:

- SP-4K projection version;
- `governed-finals-v2` ranking rule;
- `finals-ranking-source-v2` snapshot schema;
- non-empty operator-contract version;
- non-empty immutable snapshot SHA-256;
- source state/results revisions;
- snapshot capture time.

The provenance is validated but is **not copied** into the public payload.

SP-4M has no database dependency and does not read current `CompetitionResult`, `CompetitionState`, `Stacker`, or permanent-profile ranking attributes.

## Result semantics

SP-4M preserves SP-4K/SP-4L Finals status semantics and fails closed if an injected source object is inconsistent.

For `Valid`:

- official best time must be positive;
- placement must be a positive integer;
- shared placement may be true or false.

For `Scratch`, `Missing`, or `Invalid`:

- official best time must be null;
- placement must be null;
- shared placement must be false.

No non-valid result can be transformed into a fabricated rank.

## Fail-closed blockers

SP-4M uses stable blocker prefixes:

- `public-placement-scope-not-safe` — scope is not the reviewed publication policy;
- `public-placement-evidence-not-safe` — immutable provenance or status/placement semantics are inconsistent.

## Tests

`StackerPublicFinalsPlacementContractTests` verifies:

- mixed/no-additional-gender-filter placement transforms successfully;
- placement, event and shared-rank fact survive transformation;
- a raw gendered historical division label does not appear in serialized public output;
- raw category, gender, snapshot provenance, participant and internal identifiers are absent;
- male/female-specific scope fails closed;
- normal/special-specific scope fails closed;
- `division=all` fails closed;
- wrong/non-SP-4K immutable provenance fails closed;
- Scratch cannot carry fabricated placement;
- Valid evidence cannot omit placement;
- non-valid Finals may remain factual, unplaced history.

A JavaScript static guard additionally proves that:

- the contract is fixed to mixed category + no additional gender filter;
- the public point contains no raw division/category/gender/sensitive/award fields;
- the contract has no mutable SQL ranking-source read path;
- the public profile controller, application startup, browser profile and existing public profile DTO do not activate SP-4M yet.

## Deliberately unchanged

SP-4M changes no:

- Finals scoring/ranking calculation;
- SP-4K immutable projection logic;
- SP-4L identity association logic;
- competition results;
- competition state;
- public profile API response;
- public profile browser UI;
- application startup wiring;
- database schema;
- Prelims/All-Around/Doubles/Relay behavior;
- medal, award, podium or record governance;
- Chess or another activity module.

## Next boundary

After SP-4M is proven and merged, a separately reviewed phase may implement **canonical publication selection and public-profile integration**.

That later phase must derive the eligible mixed/no-additional-gender-filter SP-4L selections from immutable certified historical evidence without falling back to current demographics/results, then integrate only the SP-4M privacy-safe contract into the public profile.

SP-4M itself does not discover scopes and does not expose placement over HTTP.

## Deployment safety

SP-4M performs **no production deployment**, applies **no production database migration**, and writes **no production data**.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved artifacts, and verify the production `web.config` hash remains unchanged afterward.
