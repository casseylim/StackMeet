# NADITrack Stacker Identity v1 — SP-4D Finals Career Aggregation

Status: implementation candidate

## Purpose

SP-4D extends the privacy-safe public Sport Stacker career projection with factual, finalized Finals-stage history and presents that projection on the permanent public profile.

The phase answers a bounded question: across finalized, publicly listed competitions, which supported Individual events reached the Finals stage, what result status was recorded, and what was the athlete's best valid Finals performance?

SP-4D deliberately does **not** publish placement, podium, medals, awards, records, rankings, All-Around standing, qualification claims, or division-scoped achievement claims. Those concepts require a separately reviewed compatibility contract with the existing Finals reporting rules before they can become permanent career data.

## Public contract

For each supported Individual event that has at least one finalized Finals result row, the public contract exposes one `SportStackerEventFinalsSummary` containing:

- event code;
- Finals appearance count;
- valid Finals result count;
- first Finals date;
- latest Finals date;
- best valid Finals official time, when available;
- competition provenance for that best Finals result;
- chronological Finals history.

Each `SportStackerFinalsHistoryPoint` contains only:

- competition key;
- competition name;
- competition date;
- normalized result status;
- official time when valid;
- raw best time when valid;
- applicable penalty when valid.

No internal database identifier is published.

## Finals status semantics

SP-4D classifies Finals rows without fabricating a performance:

- `Valid` — at least one attempt is greater than zero and less than 999;
- `Missing` — the stored attempt array is empty;
- `Scratch` — there is no valid attempt and all stored attempts are 999, or the stored penalty is 999 or greater;
- `Invalid` — the row has no valid attempt but is neither Missing nor Scratch, or its attempt JSON is malformed.

For a Valid row:

- raw best time is the lowest valid attempt;
- an applicable penalty is greater than zero and less than 999;
- official time is raw best time plus the applicable penalty.

For Scratch, Missing and Invalid rows, official time and raw best time remain null.

## Source boundary

SP-4D reuses the established Stacker Identity public boundary:

- the permanent identity must have `IsPublicProfile = true`;
- the competition must have `IsPubliclyListed = true`;
- the competition must be `Closed`, `Archived`, or have a non-null archive timestamp;
- only `Individual` result rows contribute;
- only normalized `Finals` rows enter Finals Career;
- only supported Sport Stacking events enter the projection.

Active/provisional competitions, non-public competitions, Doubles, Timed Relay and Prelims-only rows cannot enter Finals Career.

## Duplicate legacy rows

A normal competition should have one Finals row per participant/event. If legacy data contains duplicates, SP-4D publishes one deterministic history point per competition/event, preferring:

1. Valid;
2. Scratch;
3. Invalid;
4. Missing;

Within the same status it prefers the lowest official time when present, then the lowest internal result identifier for deterministic selection. Internal identifiers are never published.

## Relationship to existing career views

SP-4D does not change SP-3A Personal Bests, SP-4A Tournament History or SP-4C Career Progression.

A career Personal Best may come from Prelims. A best Finals performance is therefore a distinct factual statistic and must not overwrite or redefine the athlete's existing career PB.

## Why placement and medals are deferred

The current Finals reporting implementation contains established ranking and tie-break behavior, while penalty application is represented separately by the shared best-result helper. SP-4D does not create a new historical placement engine that might disagree with the live Finals report.

Before permanent placement, medal or podium history is published, the ranking contract must be characterized and reviewed explicitly, including:

- division reconstruction for historical competitions;
- penalty treatment in rank ordering;
- equal-rank tie behavior;
- second- and third-attempt tie-break behavior;
- award-place configuration;
- Special/combined/gender division behavior;
- legacy competition compatibility.

## Presentation

The existing `/Stackers/{NadiTrackId}` page adds a `Finals Career` section between Career Progress and Tournament History.

For each event it displays:

- valid Finals count versus Finals appearances;
- first and latest Finals dates;
- best valid Finals time and source competition when available;
- chronological Finals history;
- explicit Valid, Scratch, Missing or Invalid status.

The browser renders the server projection only. It does not calculate Finals status, official time, best Finals, rank, placement, medals or awards.

Dynamic content continues to use DOM element creation and `textContent`; `innerHTML` remains forbidden.

## Privacy and compatibility

SP-4D does not expose birth date, email, phone, gender, WSSA ID, payment/check-in state, internal IDs or registration notes.

It does not change:

- the existing public profile API route;
- identity matching/resolution/persistence;
- CompetitionResult storage;
- scoring or qualification rules;
- Finals ranking/report behavior;
- division behavior;
- Doubles or Relay behavior;
- certificates or awards planning;
- Chess or other activity modules.

The profile remains `noindex,nofollow`, requests remain `cache: 'no-store'` and `credentials: 'omit'`, and the private/unknown/malformed profile boundary remains unchanged.

## Deployment safety

SP-4D performs no production deployment and introduces no schema migration or production data write.

The production deployment hard rule remains binding: the existing production `web.config` must never be overwritten, replaced, regenerated or published over. Any later production release must back up and hash the current production `web.config`, exclude it from the deployment payload, and verify that its post-release hash is unchanged.

SP-4D does not modify `web.config`, `appsettings*.json`, IIS configuration, publish profiles, production deployment workflows or production data.

## Validation

`StackerFinalsCareerTests` uses a uniquely named LocalDB database and verifies:

- canonical event order;
- Finals appearances and valid-result counts;
- chronological first/latest Finals dates;
- best valid Finals official performance with penalty provenance;
- Valid, Scratch, Missing and Invalid classification;
- malformed attempt JSON remains non-fabricated Invalid history;
- Prelims-only rows cannot enter Finals Career;
- Doubles cannot enter Individual Finals Career;
- active and non-public competitions cannot leak into the public projection;
- SP-3A Personal Best and SP-4A Tournament History behavior remains intact;
- public Finals contracts exclude sensitive/internal identifiers and rank/medal/award claims.

A dedicated JavaScript static guard protects the public contract, browser rendering, privacy, read-only and production-deployment boundaries. Required `Build and test` CI runs both the LocalDB integration harness and the complete JavaScript regression suite.
