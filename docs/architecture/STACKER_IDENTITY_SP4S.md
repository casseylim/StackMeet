# NADITrack Stacker Identity v1 — SP-4S Public Team Career Integration

Status: implementation branch

## Purpose

SP-4S integrates the privacy-safe SP-4R team-career publication into the existing permanent public Stacker profile.

It composes only reviewed layers:

`SP-4Q IdentityLinkedTeamCareerService`
→ `SP-4R PublicTeamCareerPublicationContract`
→ existing `/api/public/stackers/{nadiTrackId}`
→ existing `/Stackers/{nadiTrackId}` browser profile.

SP-4S adds no second parser, scoring path, placement logic, identity inference, or historical reconstruction.

## Public profile addition

`PublicSportStackerCareerProfile` gains one additive nullable member:

`TeamCareer : PublicTeamCareerPublication?`

The existing endpoint remains unchanged:

`GET /api/public/stackers/{nadiTrackId}`

The existing permanent browser route also remains unchanged:

`/Stackers/{nadiTrackId}`

## Published team facts

Each SP-4R point contains only:

- competition key/name/date;
- participant type: Doubles or Timed Relay;
- stage: Prelims or Finals;
- event;
- factual result status;
- official best time where valid;
- raw best time where valid;
- applied penalty.

The browser renders those facts with the publication policy:

> Verified team membership · teammate identities withheld

## Still withheld

SP-4S does not expose or render:

- team code;
- registered-member count;
- Child/Parent or external-partner flag;
- teammate/member IDs or names;
- external parent/partner name;
- membership SHA-256;
- state/results/result revisions;
- internal Stacker/competition/result IDs;
- birth date, email, phone, gender or WSSA ID;
- placement or rank;
- medal, award, podium or record claims.

## Fail-closed behavior

`PublicTeamCareerIntegrationService` returns public team history only by calling SP-4Q and then SP-4R.

If SP-4Q or SP-4R raises an `InvalidOperationException` for ambiguous or unsafe evidence, SP-4S returns an empty SP-4R publication for that otherwise valid public identity. The rest of the existing public profile remains available.

Private, unknown or malformed permanent identities retain the existing public not-found boundary because the base profile service is still the controller's first gate.

## Browser safety

The Team Career section uses DOM element creation and `textContent` only.

It does not read withheld properties such as team code, member count, external-partner state, member names or provenance hashes.

The public profile fetch remains:

- credential-free;
- non-cacheable.

## Validation

SP-4S adds:

- `StackerPublicTeamCareerIntegrationTests` LocalDB endpoint harness;
- `stacker-identity-v1-sp4s-public-team-career-integration.test.js` static guards;
- required CI execution after the SP-4R contract harness.

Coverage includes:

- normal Doubles publication;
- Child/Parent result publication without relationship/name leakage;
- Timed Relay publication;
- SP-4R publication version/policy;
- hidden-field serialization checks;
- current display-name mutation not rewriting team performance;
- ambiguous same-competition identity linkage failing team history closed without taking down the public profile;
- private and malformed identity not-found boundaries;
- DI ownership through the Sport Stacking registration seam;
- DOM/textContent-only browser rendering.

## Deliberately unchanged

SP-4S changes no:

- team result entry or validation rules;
- team membership mutation rules;
- SP-4Q historical interpretation;
- SP-4R privacy contract;
- Individual ranking/placement logic;
- database schema or EF migration;
- production release workflow;
- production database;
- `web.config`.

## Deployment safety

SP-4S performs no production deployment and no production database write.

The production `web.config` remains outside this phase and must not be overwritten by any later release.
