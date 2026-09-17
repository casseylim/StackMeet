# NADITrack Stacker Identity v1 — SP-4O Public Results → Career Profile Linking

Status: complete

## Implementation record

- Base protected master: `8a3fc0a1f5b77f70a625fa3e6ce366e0775e5909`
- Proven implementation head: `83621a0ba3025cbd626b8c9a9aef352be743d2ac`
- Push CI run: `35167275721` — `Build and test` SUCCESS
- Pull-request CI run: `35167652716` — `Build and test` SUCCESS
- Both exact-head runs completed the Release build, identity/profile integration gates, SP-4N regression coverage, CoreIntegrity checks, and complete JavaScript regression suite successfully.

## Purpose

SP-4O connects individual names shown on the permanent public competition Results portal to the corresponding permanent public NADITrack Sport Stacker career profile.

The feature is intentionally additive. It does not change timing, ranking, qualification, Finals governance, or result publication logic.

## Identity and privacy boundary

The browser must never infer a permanent athlete identity from a competition result.

A career-profile link is published only when all of the following are true:

1. the competition-scoped `Stacker` has a durable reviewed `StackerIdentityLink`;
2. the linked `SportStackerIdentity` has `IsPublicProfile = true`;
3. the permanent NADITrack ID passes `NadiTrackIdRules` validation;
4. the Stacker belongs to the requested competition.

No fallback matching is allowed by name, WSSA ID, birth date, email, phone, gender, club, division, or other registration data.

Private, unlinked, malformed, legacy-only, or otherwise ineligible entries remain plain text on the Results portal.

## Public link contract

Endpoint:

`GET /api/public/competitions/{competitionId}/profile-links`

Public payload contains only:

- competition participant code;
- server-owned relative profile URL in the form `/Stackers/{NadiTrackId}`.

No private identity attributes or internal database IDs are exposed.

## Results browser integration

`wwwroot/results/profile-links.js` is loaded after the existing `results.js` renderer.

It decorates the already-rendered `.stacker-cell` name only when the server has published an approved profile URL. It validates the URL again in the browser, uses DOM/textContent APIs only, and fails closed to the existing plain-text name if the optional link service is unavailable.

The existing Results engine remains authoritative for all result display and live-update behavior.

Initial SP-4O presentation scope:

- Individual Preliminary standings;
- Individual Finals standings;
- All-Around standings, including division groups that use the existing `.stacker-cell` presentation.

Doubles and Relay team/member linking is deliberately deferred because those sections have a different team identity presentation and should not be conflated with the individual profile-link contract.

## Production note

SP-4O does not change the previously identified production-activation requirements for the permanent identity/profile foundation. Production still requires the approved database migration procedure and an application deployment manifest that includes the relevant static web assets. A merge to `master` is not a production deployment.
