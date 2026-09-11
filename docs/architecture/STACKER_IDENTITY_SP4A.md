# NADITrack Stacker Identity v1 — SP-4A Tournament History

Status: implementation candidate

## Purpose

SP-4A extends the reviewed public Sport Stacker career projection with a bounded tournament-history timeline. It does not introduce rankings, medals, profile editing, directory search, ownership, consent workflow, or any persistence behavior.

The history is intended to answer a simple public question: which finalized public competitions did this permanent NADITrack athlete appear in, and what was the athlete's best valid Individual performance for each supported event at each competition?

## Public history contract

Each finalized public appearance exposes only:

- competition key;
- competition name;
- competition date;
- zero or more Individual event performances.

Each event performance exposes only:

- normalized event code;
- official time;
- raw best valid attempt;
- applied penalty;
- normalized stage.

Birth date, email, phone, gender, WSSA ID, payment/check-in state, registration notes, internal database IDs, and other private registration attributes remain outside the public contract.

## Inclusion boundary

SP-4A reuses the SP-3A/SP-3B publication boundary exactly:

- the permanent identity must have `IsPublicProfile = true`;
- the competition must have `IsPubliclyListed = true`;
- the competition must be `Closed`, `Archived`, or have a non-null archive timestamp;
- only `Individual` CompetitionResult rows may contribute performances.

Active/provisional competitions and non-public competitions cannot enter tournament history.

A finalized public competition appearance remains in history even when the athlete has no valid Individual result for that competition. In that case its performance collection is empty.

## Timing semantics

SP-4A deliberately reuses the same candidate calculation as the existing career-PB projection rather than creating a second timing engine.

For each result row:

- event and stage must normalize through `CompetitionResultRules`;
- attempt values must be greater than zero and less than 999;
- malformed attempt JSON is ignored safely;
- the raw tournament candidate is the lowest valid attempt;
- a penalty contributes only when greater than zero and less than 999;
- official time is raw best time plus applicable penalty.

Within one competition and event, the lowest official Individual performance is selected. Ties remain deterministic through the existing result identifier ordering.

The tournament list is reverse chronological. Event ordering remains 3-3-3, 3-6-3, Cycle.

## Compatibility

SP-4A adds `TournamentHistory` to `PublicSportStackerCareerProfile`. Existing profile identity summary, competition count/date range, and personal-best semantics are unchanged.

The existing `/api/public/stackers/{NadiTrackId}` endpoint will serialize the additive history property through the already reviewed SP-3B public boundary. The existing profile page is not changed in SP-4A; presentation of the timeline can be reviewed separately.

## Safety boundary

SP-4A introduces:

- no database schema or migration;
- no write path;
- no historical Stacker/result rewrite;
- no production database action;
- no production deployment;
- no change to Sport Stacking scoring, ranking, division, Doubles, Relay, prelim/final qualification, reports, or certificates;
- no WSSA uniqueness rule;
- no profile ownership/editing workflow;
- no minors/guardian consent workflow;
- no athlete directory or search-engine indexing change;
- no Chess implementation change.

The service remains `AsNoTracking` and contains no EF persistence operation.

## Validation

`StackerTournamentHistoryTests` uses a uniquely named LocalDB database and proves:

- only finalized publicly listed appearances are returned;
- appearances with no valid results are retained;
- tournament history is reverse chronological;
- best valid Individual performance is selected per event per competition;
- penalty and stage provenance are retained;
- Doubles results are excluded;
- active and non-public competitions cannot leak into history;
- existing career PB semantics remain unchanged;
- public history contracts contain no sensitive identity fields.

A dedicated static guard protects the same boundaries, and required CI runs the LocalDB executable on every candidate branch/PR.
