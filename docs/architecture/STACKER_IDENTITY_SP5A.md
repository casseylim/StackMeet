# NADITrack Stacker Identity v1 — SP-5A Personal Record Achievement Summary

Status: complete

## Purpose

SP-5A adds athlete-facing personal records alongside the separately reviewed Finals placement and Team Career features on current master.

The phase enriches the permanent public Stacker profile with a server-owned personal-record achievement summary for each supported Individual event:

- first recorded finalized public PB;
- current finalized public PB;
- total improvement from the first recorded PB to the current PB;
- number of strict PB milestones;
- number of finalized public tournament-best performances used by the progression;
- first-PB competition/date/stage provenance;
- current-PB competition/date/stage provenance.

The supported event order remains `3-3-3`, `3-6-3`, `Cycle`.

## Source of truth

SP-5A does not introduce a second timing or PB engine.

`SportStackerCareerProfileService` first reduces valid Individual results to the same one-best-performance-per-competition/event dataset already used by Tournament History and Career Progress. `BuildCareerProgression` remains the authoritative chronological PB progression. `BuildPersonalRecords` derives the achievement summary from that server-owned progression.

This means Personal Records and Career Progress cannot legitimately disagree about:

- which competitions qualify;
- tournament-best official time;
- whether a point is a new PB;
- the running PB;
- strict-improvement semantics.

## PB semantics

The existing reviewed semantics remain unchanged:

- only finalized, publicly listed competitions contribute;
- only `ParticipantType == "Individual"` contributes;
- supported event and stage normalization remains authoritative;
- valid attempts are greater than zero and less than 999;
- finite applicable penalty is greater than zero and less than 999;
- official time is raw best plus applicable finite penalty;
- the first valid finalized tournament performance establishes the first recorded PB;
- only a strictly lower official time creates a new PB milestone;
- a tie does not create another milestone;
- a slower performance remains part of Career Progress and the finalized-performance count without changing the PB;
- active/provisional and non-public competitions are excluded.

`TotalImprovement` is calculated on the server as:

`FirstRecordedPersonalBest - CurrentPersonalBest`

For an event with only one PB milestone, the value is `0`.

## Public contract

SP-5A adds the additive `SportStackerPersonalRecordAchievement` projection and exposes it through `PublicSportStackerCareerProfile.PersonalRecords`.

The existing `PersonalBests` property remains unchanged for compatibility. SP-5A is additive rather than a replacement contract.

`PersonalRecords` is an initialized additive property, preserving the existing public-profile constructor. The public controller's Finals placement and Team Career enrichment retains the personal-record summary.

The personal-record projection deliberately excludes private/internal fields such as:

- birth date;
- email;
- phone;
- gender;
- WSSA ID;
- competition Stacker ID;
- internal competition database ID.

## Presentation

The existing public profile Personal Bests area becomes the athlete-facing **Personal Records** area.

Each existing PB card continues to show the current official PB and timing provenance. When the additive `personalRecords` projection is available it also shows:

- first recorded PB;
- total improvement;
- PB milestone count;
- finalized-performance count;
- first-PB source;
- current-PB source.

The browser formats and presents server-projected values only. It must not compare official times, decide whether a performance is a PB, calculate total improvement, or reconstruct milestone counts.

The existing public-profile protections remain mandatory:

- `noindex,nofollow`;
- `cache: 'no-store'`;
- `credentials: 'omit'`;
- DOM element creation and `textContent`;
- no `innerHTML` injection.

## Deliberately excluded

SP-5A does **not**:

- generate certificates;
- change governed-v2 Finals snapshot certification;
- introduce placement, podium, medal or award publication;
- publish record-holder or official-record claims;
- change Prelims/Finals ranking semantics;
- change the separately reviewed Doubles or Relay career records;
- add a schema migration;
- mutate production data.

Certificate generation remains deferred. Existing governed Finals certification, placement publication and Team Career behavior remain intact.

## Tests

`StackerPersonalRecordAchievementTests` uses an isolated generated SQL Server LocalDB database and verifies:

- canonical event ordering;
- first/current PB values;
- exact total improvement;
- strict PB milestone count;
- ties do not create fake PB milestones;
- slower results do not create PB milestones;
- finalized-performance count remains complete;
- current PB provenance points to the strict-improvement milestone rather than a later tie;
- PB provenance may correctly come from Prelims;
- active/private competitions cannot contaminate public Personal Records;
- the old `PersonalBests` contract stays aligned;
- the public Personal Records contract excludes private/internal fields.

A static regression guard also requires the browser to render the server projection rather than recreating PB calculations.

## Deployment safety

SP-5A performs **no production deployment**, applies **no production database migration**, and writes **no production data**.

The production hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later separately approved production release must back up and hash the live `web.config`, exclude it from the deployment payload, deploy only approved binaries/static artifacts, and verify the production `web.config` hash remains unchanged afterward.
