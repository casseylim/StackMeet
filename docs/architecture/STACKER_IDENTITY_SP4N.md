# NADITrack Stacker Identity v1 — SP-4N Public Finals Placement Integration

Status: complete

Base protected master: `d843cf043e06bc5bb8fb2506d813851ef6b52d35`

Proven implementation head: `d251343f5780d5963423f9273543c00e983afc81`

Implementation-head CI:

- push run `34963217249` — `Build and test` SUCCESS;
- PR run `34963223267` — `Build and test` SUCCESS.

Both runs passed SP-4N, CoreIntegrity, Phase 4C and the complete JavaScript regression suite before this documentation-only completion commit.

## Purpose

SP-4N activates the SP-4M privacy-safe permanent Finals placement contract on the existing public Sport Stacker profile without creating a second ranking authority.

The phase answers the missing selection question from SP-4M:

> Which immutable SP-4L scopes may be selected automatically for a permanent public profile without consulting current demographics or current result rows?

## Canonical immutable selection

`PublicFinalsPlacementCareerIntegrationService` is the SP-4N selection and integration boundary.

For each reviewed permanent identity link and finalized/public competition it:

1. uses the existing reviewed identity link only to obtain the competition-scoped historical participant code;
2. requires an immutable `governed-finals-v2` / `finals-ranking-source-v2` governance snapshot;
3. verifies the stored snapshot SHA-256 and persisted source revisions;
4. verifies the reviewed `sp4h-event-finals-v1` operator-contract provenance;
5. reads the participant's competition-time division from the immutable snapshot `competitionState`;
6. reads supported Finals events for that participant from immutable snapshot `finalsResults`;
7. builds only `Individual + explicit immutable division + event + mixed + all` SP-4L selections;
8. delegates placement calculation to SP-4L/SP-4K;
9. delegates privacy shaping to the SP-4M publication contract.

The selector does **not** use current Stacker gender, Special status, custom division, name, or current `CompetitionResult` rows to discover or calculate permanent placement.

## Module-owned composition boundary

During implementation, the Modular Platform Foundation Phase 2 guard correctly rejected registration of the SP-4N Finals integration service inside Shared Core `ActivityModuleRegistration`.

The corrected composition is:

- Shared Core `AddNadiTrackActivityModules()` remains free of Sport Stacking Finals-specific services;
- Sport Stacking owns `AddSportStackingIdentityProfileServices()` in its Identity namespace;
- application startup explicitly activates that module-owned seam from `Program.cs` after the shared activity-module registration.

The SP-4N static guard now enforces all three parts of that boundary. This preserves modular architecture while ensuring the live controller can resolve the SP-4N integration service.

## Static-guard handoff

SP-4N deliberately enriches an established public profile rather than replacing earlier contracts. Historical SP-3A, SP-3B and SP-4D guards were narrowed from obsolete implementation-shape assertions to their original invariants:

- the canonical public-profile lookup and indistinguishable private/not-found boundary happen before any placement enrichment;
- the existing public endpoint, no-store behavior and credential-free browser contract remain unchanged;
- the original SP-4D Finals Career records and renderer remain placement/rank/medal-free;
- certified placement is rendered only by SP-4N's separate governed placement surface;
- placement never implies a medal, award, podium classification or record.

No privacy or architectural guard was removed to make CI pass; ownership of the new governed placement behavior moved to the SP-4N tests.

## Fail-closed compatibility

A legacy, uncertified, malformed, ambiguous or unsupported historical competition contributes no permanent placement.

That does not make the existing public career profile unavailable. SP-4N placement is additive: the pre-existing public profile, personal bests, tournament history, career progression and Finals career remain available under their established contracts even when no certified permanent placement exists.

## SP-4L → SP-4M status compatibility correction

End-to-end analysis found that SP-4K/SP-4L emit result statuses in lowercase (`valid`, `scratch`, `missing`, `invalid`) while the isolated SP-4M fixture had used title-case values.

SP-4N corrects this seam at the SP-4M publication boundary by normalizing those canonical statuses and emitting stable public values:

- `Valid`
- `Scratch`
- `Missing`
- `Invalid`

The status normalization does not alter placement or ranking semantics.

## Existing public API integration

No new public endpoint is added.

The existing endpoint remains:

`GET /api/public/stackers/{nadiTrackId}`

`PublicSportStackerCareerProfile` receives one additive `FinalsPlacements` member containing the SP-4M `PublicFinalsPlacementCareerPublication`.

The existing route, no-store behavior, private/not-found boundary and credential-free browser fetch remain unchanged.

## Browser presentation

The existing `/Stackers/{NadiTrackId}` profile page adds a **Finals Placements** section.

It renders only the SP-4M privacy-minimized fields:

- competition key/name/date;
- event;
- canonical result status;
- certified official best time when valid;
- nullable placement;
- shared-placement fact;
- fixed cohort-policy wording.

The browser does not receive or render raw division, gender, Special status, participant code, internal IDs, snapshot hash/revisions, medal, award, podium or record claims.

The fixed public context remains:

> Competition-time division · mixed category · no additional gender filter

## Historical immutability

The SP-4N LocalDB integration harness certifies governed-v2 evidence and then mutates current registration demographics/division and current result attempts.

Permanent public placement must remain unchanged because canonical selection and SP-4K placement continue to use the immutable certified snapshot.

## Validation

`StackerPublicFinalsPlacementIntegrationTests` proves the complete path:

immutable certification → automatic SP-4N selection → SP-4L identity linkage → SP-4K placement → SP-4M publication → existing public-profile controller.

It covers:

- valid permanent placement;
- Scratch/unplaced history;
- actual lowercase SP-4L status compatibility;
- mixed/all ranking scope;
- gendered raw division privacy;
- legacy competition exclusion from permanent placement;
- current registration/result mutation immunity;
- private and malformed identity not-found boundaries.

The SP-4N JavaScript static guard additionally proves the immutable selector does not read current `CompetitionResult` rows or current demographic/division attributes, Shared Core remains free of Sport Stacking Finals registration, application startup activates the Sport Stacking-owned DI seam, and browser rendering remains `textContent`/DOM based, credential-free and no-store.

## Deliberately unchanged

SP-4N changes no:

- Finals scoring/ranking calculation;
- SP-4K immutable projection semantics;
- SP-4L identity association rules;
- ranking-governance persistence schema;
- certification workflow;
- Prelims or All-Around logic;
- Doubles or Relay permanent placement;
- medal, award, podium or record governance;
- Chess or another activity module;
- public profile route;
- authentication boundary.

## Deployment safety

SP-4N performs **no production deployment**, applies **no production database migration**, and writes **no production data**.

The production hard rule remains binding: the existing production `web.config` must never be overwritten, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved artifacts, and verify the production `web.config` hash remains unchanged afterward.

## Next boundary

After SP-4N is merged and post-merge master CI is green, the next phase should review **permanent placement presentation governance and release readiness** rather than adding medal/award claims implicitly.

Any medal, podium, award, record-holder or cross-competition ranking claim must remain a separately governed contract with its own immutable authority and tests.
