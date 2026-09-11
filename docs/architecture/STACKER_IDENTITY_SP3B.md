# NADITrack Stacker Identity v1 — SP-3B Public Profile Endpoint and Presentation

Status: first reviewed public presentation boundary; development only

## Purpose

SP-3B exposes the privacy-minimized SP-3A Sport Stacker career projection through one reviewed public API endpoint and one stable public profile URL.

The phase is intentionally presentation-only. It does not add new identity data, new scoring rules, profile editing, ownership claims, consent workflows, or production deployment.

## Permanent public routes

Public read API:

`GET /api/public/stackers/{NadiTrackId}`

Public profile page:

`/Stackers/{NadiTrackId}`

The page is a small isolated shell under `wwwroot/profile/` and reads only the public API contract. It does not depend on authenticated application state, competition sessions, maintenance API keys, or browser storage.

## Authentication boundary

The API remains under the existing `/api/public` exemption in `Program.RequiresApiAuth`. SP-3B does not weaken or add a new authentication bypass.

The browser request explicitly omits credentials. No bearer token, API key, account session, or maintenance credential is sent by the public profile page.

## Privacy and existence boundary

`SportStackerCareerProfileService.GetPublicAsync` remains the sole career projection authority.

A profile is returned only when the permanent `SportStackerIdentity` exists and `IsPublicProfile = true`.

Private, unknown, and malformed NADITrack IDs resolve through the same API not-found behavior. The endpoint therefore does not provide a separate private-profile existence signal.

The public contract remains limited to:

- permanent NADITrack ID;
- display name;
- country;
- optional club and region;
- finalized publicly-listed competition count and date range;
- finalized Individual personal bests for 3-3-3, 3-6-3, and Cycle, including competition/stage provenance.

Birth date, email, phone, gender, WSSA/external IDs, payment/check-in state, and other registration-only data are not consumed by the frontend and are not part of the SP-3A/SP-3B public contract.

## Finalized-result boundary

SP-3B does not recalculate career data in the controller or browser.

The page receives the already-reviewed SP-3A projection, where only publicly listed finalized competitions contribute. Active/provisional and non-public competitions therefore cannot appear in public career totals or personal bests.

## Presentation safety

The profile renderer uses DOM `textContent` and element creation rather than HTML injection.

The profile API and profile shell are non-cacheable for this phase.

The first public profile page also sends and declares `noindex, nofollow`. This deliberately allows a stable shareable URL while avoiding search-engine indexing until athlete ownership, minors/guardian consent, and final publication governance are separately reviewed.

## Deliberately excluded from SP-3B

SP-3B includes no:

- athlete self-service profile editing;
- account-to-athlete claim workflow;
- minors/guardian consent workflow;
- profile photo or media upload;
- search-engine indexing enablement;
- public athlete directory/search;
- tournament-history list beyond PB provenance;
- medals, awards, rankings, records, Doubles or Relay career aggregation;
- schema migration;
- historical Stacker/result rewrite;
- production database action;
- **no production deployment**.

Those items require separate review rather than being inferred from `IsPublicProfile`.

## Validation

SP-3B adds an isolated LocalDB integration harness that proves:

- an opted-in profile returns HTTP 200 with the reviewed public contract;
- the permanent ID is normalized;
- finalized public career count and PBs are returned through the endpoint;
- private, unknown, and malformed identifiers all return not found;
- the API route remains below `/api/public`;
- the response is marked non-cacheable;
- the permanent browser route is `/Stackers/{NadiTrackId}`.

Static guards also verify that the browser presentation omits credentials, avoids `innerHTML`, does not consume private contract fields, and remains `noindex` for this phase.
