# NADITrack Stacker Identity v1 — SP-4P Certified Finals Placement Presentation Governance

Status: implementation candidate

Base protected master: `104e1c35bb8c9743ab4005c3803cb2cf1936c7da`

## Purpose

SP-4P closes the presentation-governance boundary identified after SP-4N. It does not calculate, reconstruct or reinterpret Finals placement. It governs when the existing SP-4M/SP-4N permanent placement publication is safe for the public profile browser to present as a certified historical fact.

SP-4O remains a separate navigation feature: it links eligible names on the public Results portal to the permanent profile. SP-4P governs the placement claim after the visitor reaches that profile.

## Authority boundary

The server remains the only placement authority:

- SP-4J certifies immutable governed-v2 source evidence;
- SP-4K projects placement from that immutable evidence;
- SP-4L associates the historical participant with a reviewed permanent identity;
- SP-4M creates the privacy-minimized public publication contract;
- SP-4N selects eligible immutable scopes and adds that publication to the existing public profile.

SP-4P adds no ranking engine, no result query and no alternate placement calculation.

## Browser presentation gate

`wwwroot/profile/finals-placement-presentation.js` recognizes exactly one reviewed public contract:

- publication version: `sp4m-public-finals-placement-v1`;
- publication NADITrack ID must equal the already-validated public profile NADITrack ID;
- cohort policy must exactly equal `Competition-time division · mixed category · no additional gender filter`;
- event must be one of `3-3-3`, `3-6-3`, or `Cycle`;
- result status must be one of `Valid`, `Scratch`, `Missing`, or `Invalid`;
- a Valid result must have a positive official time and positive integer placement;
- Scratch/Missing/Invalid must have no official time, no placement, and no shared-placement claim.

If the root contract or any history point violates those reviewed semantics, the entire placement publication fails closed in the browser and the existing generic `No certified permanent Finals placement is available yet.` state is shown.

The browser does not repair case, guess missing fields, infer placement, or fall back to current results.

## Presentation wording

For a reviewed Valid fact the browser may show only:

- `Certified placement in competition-time division: #N`; or
- `Certified shared placement in competition-time division: #N`.

The numeric `#N` representation is deliberate. SP-4P does not convert placement to ordinal/podium wording and does not infer medal, award, podium classification, record-holder status, national ranking or global ranking.

Scratch, Missing and Invalid history may remain visible as certified result-status history, but never with a time or placement claim.

## Privacy boundary

SP-4P consumes only the existing SP-4M public fields. It does not expose or derive:

- raw division labels;
- gender or Special status;
- birth date, email or phone;
- WSSA ID;
- internal identity, Stacker, competition or result database IDs;
- snapshot hash/revisions;
- participant code or participant name from immutable evidence.

The exact historical division remains server-internal because historical labels may encode personal attributes.

## Browser safety

The public profile continues to use DOM creation and `textContent`; `innerHTML` is not introduced. The profile request remains `cache: 'no-store'` with `credentials: 'omit'`, and the page remains `noindex,nofollow` while ownership/minors-consent and public-directory governance are deferred.

The placement-policy script loads before `profile.js`. If that script is unavailable, the placement renderer fails closed rather than rendering ungoverned claims.

## Release-readiness boundary

SP-4P makes the permanent placement presentation code reviewable for a later separately approved production activation. It does **not** perform that activation.

Before a production release of the permanent identity/profile foundation, the deployment procedure must separately verify at minimum:

1. all approved identity and Finals-governance database migrations required by the merged master are present before the feature is relied upon;
2. the application payload includes the public profile assets, including `profile/index.html`, `profile/profile.css`, `profile/profile.js`, and `profile/finals-placement-presentation.js`;
3. the Results-to-profile asset `results/profile-links.js` is included when SP-4O is activated;
4. the existing production `web.config` is backed up and hashed, excluded from the deployment payload, and verified byte-for-byte/hash unchanged after deployment;
5. post-release smoke checks cover a known public profile, private/malformed not-found behavior, an eligible Results-to-profile link, a certified governed-v2 placement, and a legacy/non-certified competition that must not fabricate placement;
6. production rollback of application/static assets must not rewrite or delete immutable certified ranking snapshots.

## Tests

`tests/stacker-identity-v1-sp4p-finals-placement-presentation-governance.test.js` executes behavioral checks against the presentation-policy module and static integration guards. It verifies:

- accepted reviewed publication semantics;
- rejection of unknown versions, wrong identities, altered cohort wording and unsupported events/statuses;
- Valid placement/time requirements;
- non-Valid no-time/no-placement/no-shared-placement requirements;
- exact profile-identity binding;
- policy-script load ordering;
- DOM/textContent and noindex boundaries;
- absence of ranking-source dependencies and award/medal/podium/record/ranking inference.

The test is automatically included in the repository's complete JavaScript regression suite.

## Deliberately unchanged

SP-4P changes no:

- Finals timing, ranking, tie or qualification rules;
- immutable snapshot certification or persistence;
- SP-4K placement calculation;
- SP-4M public data shape;
- public profile API route;
- public Results ranking/render engine;
- Prelims or All-Around ranking behavior;
- Doubles or Relay permanent career aggregation;
- medal, award, podium, record or cross-competition ranking governance;
- production database or production files.

## Production safety

This phase performs no production deployment, no production database migration, no production data write and no `web.config` change.
