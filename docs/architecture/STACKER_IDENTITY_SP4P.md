# NADITrack Stacker Identity v1 — SP-4P Doubles / Relay Member → Career Profile Linking

Status: implementation candidate

## Purpose

SP-4P extends the SP-4O public Results → career-profile bridge to the individual members shown inside Doubles and Relay team standings.

The feature is presentation-only. It does not change team composition, result entry, timing, ranking, qualification, Finals governance, medals, or result publication.

## Identity boundary

SP-4P does not infer identity from a displayed name.

The browser uses only explicit competition participant IDs already present in the public Results contract:

- Doubles: `team.one` and `team.two`;
- Relay: `team.members`, or the existing explicit `team.one` through `team.six` slots for legacy team records.

Those competition participant IDs are then looked up in the existing SP-4O server-approved profile-link contract:

`GET /api/public/competitions/{competitionId}/profile-links`

Therefore a team member becomes clickable only when the existing server boundary has already confirmed a durable reviewed identity link, public-profile visibility, and a valid permanent NADITrack ID.

No matching is performed by name, WSSA ID, birth date, email, phone, gender, division, organization, or other registration attributes.

## Browser integration

The existing `wwwroot/results/results.js` renderer remains unchanged and authoritative.

`wwwroot/results/profile-links.js` additionally reads the public Results payload to obtain explicit Doubles/Relay membership IDs, then decorates only the already-rendered member cell.

Doubles member names use the existing ` & ` presentation separator. Relay member names use the existing `, ` separator.

The member profile URL is still accepted only when it matches the SP-4O permanent public profile path contract:

`/Stackers/NDT-XXXXXXX`

Rendering remains DOM/textContent based; no `innerHTML` is used.

## Fail-closed behavior

If the server-approved profile-link endpoint is unavailable, member names are rendered without profile links.

If public team-membership evidence is unavailable, previously decorated team member cells are flattened back to plain text rather than retaining stale identity links.

If an identity is made private, the next profile-link refresh removes the link while preserving the visible member name.

The Results page and its live-update behavior continue functioning independently of this optional profile-link layer.

## Scope safety

SP-4P introduces:

- no database migration;
- no SQL schema change;
- no result/ranking algorithm change;
- no team-composition write path;
- no production deployment;
- no `web.config` change.

The permanent identity/profile production activation remains a separately approved release activity.
