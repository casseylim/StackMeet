# NADITrack Stacker Identity v1 — SP-4C Career Progression

Status: implementation candidate

## Purpose

SP-4C extends the privacy-safe public Sport Stacker career projection with chronological event progression and presents that reviewed projection on the permanent public profile.

The phase answers a bounded question: across finalized, publicly listed competitions, how did the athlete's best Individual performance at each tournament change over time, and when did that performance establish a new career personal best?

SP-4C does not introduce rankings, medals, awards, records governance, profile editing, athlete search, ownership, consent workflow, or production deployment.

## Progression contract

For each supported Individual event, the public contract exposes a chronological sequence of finalized tournament performance points.

Each point contains only:

- competition key;
- competition name;
- competition date;
- official tournament-best time for the event;
- normalized stage provenance;
- whether the point established a new career PB;
- the running career PB after that point;
- improvement from the previous PB when a strict improvement occurred.

The first valid finalized performance establishes the baseline PB. Its improvement value is null because no previous career PB exists.

A later performance establishes a new PB only when its official time is strictly lower than the prior PB. Equal times do not create duplicate PB milestones. Slower times remain visible as performance points while the running PB remains unchanged.

## Source and reduction semantics

SP-4C reuses the same public/finalized and timing boundaries as SP-3A and SP-4A:

- permanent identity must have `IsPublicProfile = true`;
- competition must have `IsPubliclyListed = true`;
- competition must be `Closed`, `Archived`, or have a non-null archive timestamp;
- only `Individual` result rows contribute;
- event and stage normalize through `CompetitionResultRules`;
- attempts must be greater than zero and less than 999;
- malformed attempt JSON is ignored;
- an applicable penalty is greater than zero and less than 999;
- official time is lowest valid attempt plus applicable penalty.

Multiple Prelims/Finals rows in one competition/event are first reduced to one tournament-best official performance. The same reduced candidate list feeds both SP-4A Tournament History and SP-4C Career Progression, preventing those public views from disagreeing about an athlete's tournament time.

## Ordering

Events retain canonical order:

1. 3-3-3;
2. 3-6-3;
3. Cycle.

Within each event, progression points are chronological by competition date and then deterministic competition/result identifiers. Internal identifiers are used only for stable ordering and are never published.

## Presentation

The existing `/Stackers/{NadiTrackId}` profile adds a Career Progress section between Personal Bests and Tournament History.

The browser renders the server-projected values only. JavaScript does not calculate PB status, improvement amount, tournament-best selection, penalties, or official times.

Each point shows:

- competition date/name/key;
- official time;
- stage;
- running PB after the competition;
- exact server-projected improvement when present;
- `New PB` or `PB held` status from the server projection.

Dynamic content continues to use DOM element creation and `textContent`; `innerHTML` remains forbidden.

## Privacy and compatibility

SP-4C does not expose birth date, email, phone, gender, WSSA ID, payment/check-in state, internal database IDs, registration notes, or other private registration attributes.

It does not change:

- the existing public API route;
- identity matching/resolution/persistence;
- historical Stacker snapshots;
- CompetitionResult storage;
- Sport Stacking scoring/ranking/division logic;
- prelim/final qualification logic;
- Doubles or Relay behavior;
- reports or certificates;
- activity-module or Chess behavior.

The profile remains `noindex,nofollow`, browser requests remain `cache: 'no-store'` and `credentials: 'omit'`, and private/unknown/malformed identities remain indistinguishable at the SP-3B not-found boundary.

## Deployment safety

SP-4C performs no production deployment and introduces no schema migration.

The production deployment hard rule established in SP-4B remains binding: the existing production `web.config` must not be overwritten, replaced, regenerated, or published over. Any later release must back up and hash the current production file, exclude it from the payload, and verify its hash remains unchanged after deployment.

SP-4C does not modify `web.config`, `appsettings*.json`, IIS configuration, publish profiles, deployment scripts, or production data.

## Validation

`StackerCareerProgressTests` uses a uniquely named LocalDB database and proves:

- only finalized publicly listed Individual results enter progression;
- progression points are chronological;
- the first valid result establishes the baseline PB;
- slower results remain visible without changing the PB;
- a strict improvement records the exact improvement amount;
- a tie does not create a duplicate PB milestone;
- tournament-best stage provenance is retained;
- Doubles, active competitions, and non-public competitions are excluded;
- SP-3A PB and SP-4A Tournament History remain aligned;
- progression contracts expose no sensitive/internal identifiers.

A dedicated JavaScript static guard protects the presentation/security/deployment boundaries, and required `Build and test` CI runs both the LocalDB progression test and the repository-wide regression suite.
