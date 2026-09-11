# NADITrack Stacker Identity v1 — SP-4B Tournament History Presentation

Status: implementation candidate

## Purpose

SP-4B presents the already-reviewed SP-4A tournament-history projection on the permanent public Sport Stacker profile route.

The phase is presentation-only. It consumes the existing `TournamentHistory` property returned by `GET /api/public/stackers/{NadiTrackId}` and renders it on `/Stackers/{NadiTrackId}`. It does not introduce a second API, a new persistence path, a database migration, ranking logic, medal logic, or any change to competition-result authority.

## Presentation contract

The public page adds a Tournament History section below Personal Bests.

For each finalized public appearance it renders only:

- competition name;
- competition date;
- public competition key when present;
- zero or more Individual event performances.

For each performance it renders only:

- event code;
- official time;
- raw best valid time;
- applicable penalty when present;
- normalized stage provenance.

An appearance with no valid Individual result remains visible with a neutral appearance-recorded message.

The presentation preserves the SP-4A server ordering: newest finalized public competition first, with event ordering provided by the reviewed read model.

## Privacy and security

SP-4B continues the SP-3B browser boundary unchanged:

- profile route remains `noindex, nofollow`;
- public fetch uses `credentials: 'omit'`;
- public fetch uses `cache: 'no-store'`;
- no bearer token, API key, session credential, or authorization header is sent;
- all dynamic values are rendered with DOM element creation and `textContent`;
- `innerHTML` is forbidden;
- birth date, email, phone, gender, WSSA ID and other registration-only data are not consumed by the renderer.

Private, unknown and malformed NADITrack identities continue to use the existing indistinguishable unavailable boundary provided by SP-3B.

## Compatibility

SP-4B does not change:

- the public API route;
- the public career DTO;
- SP-3A personal-best calculation;
- SP-4A tournament-history calculation;
- competition-result storage;
- scoring/ranking/division logic;
- prelim/final qualification;
- Doubles or Relay behavior;
- reports or certificates;
- activity-module behavior;
- Chess behavior.

The existing profile identity summary, career statistics and Personal Bests remain intact.

## Deployment safety boundary

SP-4B contains no production deployment action.

When this work is eventually deployed, the existing production `web.config` is environment-specific and must **not** be overwritten by the application deployment payload. Production deployment must preserve the current `web.config`, verify it before/after release, and deploy application binaries/static assets without replacing that file.

This requirement is a hard production-release constraint, not an optional cleanup step.

SP-4B itself changes only profile presentation assets, architecture documentation and regression guards. It does not modify `web.config`, `appsettings*.json`, deployment scripts, IIS configuration, publishing profiles, or production data.

## Validation

A dedicated SP-4B static regression guard verifies:

- the Tournament History presentation container exists;
- the renderer consumes `profile.tournamentHistory`;
- appearances with no performances remain renderable;
- official/raw/penalty/stage presentation is present;
- browser fetch remains unauthenticated and non-cacheable;
- rendering continues to use `textContent`/DOM creation and never `innerHTML`;
- private registration fields remain absent from the renderer;
- `noindex,nofollow` remains in the profile document;
- the production `web.config` preservation requirement is documented.

The repository-wide JavaScript regression suite automatically executes the new guard in required `Build and test` CI.
