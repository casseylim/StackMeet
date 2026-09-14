# NADITrack Stacker Identity v1 — SP-4F Versioned Finals Ranking Governance

Status: complete

## Purpose

SP-4F resolves the governance questions identified by SP-4E without changing the existing operator Finals report or publishing permanent historical placement yet.

The phase defines two explicit ranking contracts:

- `legacy-finals-v1` — the frozen behavior already used by the current operator Finals report;
- `governed-finals-v2` — a corrected, versioned contract for future explicit activation.

The central safety rule is that old competitions must never be silently re-ranked under a new rule. An unversioned competition is interpreted as `legacy-finals-v1`. `governed-finals-v2` is **defined but not activated** by SP-4F and can only be selected by a later explicit activation/persistence phase.

Unknown non-blank versions fail closed. They never fall back silently to either v1 or v2.

## Why versioning is required

SP-4E proved that existing ranking behavior and the shared official-time helper are not identical:

- shared `BestResultEngine.rankingTime()` applies a finite result-level penalty to the best valid attempt;
- current `FinalsReportEngine.finalTieKey()` orders raw valid attempts and ignores that finite penalty;
- current shared classification accepts a valid attempt before considering a `999` penalty;
- the broad Finals business-rule wording treats a scratch penalty as Scratch.

Changing the existing Finals engine in place would therefore risk rewriting past winners. SP-4F instead freezes v1 and defines v2 separately.

## Rule version resolution

`FinalsRankingPolicy.js` establishes the version boundary:

1. blank / null / missing version → `legacy-finals-v1`;
2. explicit `legacy-finals-v1` → legacy v1;
3. explicit `governed-finals-v2` → governed v2;
4. any other non-blank value → unsupported / fail closed.

There is no date-based auto-upgrade and no inference from application version, competition status, athlete identity, or current time.

A later activation phase must explicitly persist the chosen rule version before v2 can become operational.

## `legacy-finals-v1` — frozen compatibility contract

Legacy v1 is not redesigned by SP-4F. It remains the behavior characterized in SP-4E:

- Finals-stage placement only;
- rank scope is participant type + competition-snapshot division + event after report filters;
- only results classified `valid` receive numeric rank;
- tie key is raw valid attempts sorted ascending: best, second-best, third-best;
- equal complete keys receive equal competition rank (`1, 1, 3`);
- name/participant only stabilizes display inside a true tie;
- finite penalty is not included in the Finals tie key;
- a valid attempt currently wins classification before a `999` penalty is considered.

SP-4F does not endorse those last two details as future policy. It preserves them so historical behavior is not silently rewritten.

## `governed-finals-v2` — corrected contract

Governed v2 is defined as follows.

### Result status

- `Scratch` — result-level penalty is `999` or greater, regardless of otherwise-valid attempts;
- `Valid` — no scratch penalty exists and at least one finite attempt is greater than zero and less than `999`;
- `Missing` — no numeric attempts exist and there is no scratch penalty;
- `Scratch` — no valid attempt exists and every numeric attempt is `999`;
- `Invalid` — no valid attempt exists and the row is neither Missing nor Scratch.

A `999` attempt is never a time. A mixture such as `[999, 5.250, 999]` remains a Valid result when there is no result-level scratch penalty because the valid attempt is still usable.

### Finite penalty

A finite result-level penalty is greater than zero and less than `999`.

Governed v2 uses the established shared official-time meaning for the primary comparison:

`official best = lowest valid attempt + finite result-level penalty`

The penalty is applied to the primary best-time comparator only because the existing data model stores one result-level penalty and the existing shared `rankingTime()` defines exactly that official best-time value. SP-4F does not invent per-attempt penalties that are not stored by the system.

### Tie key

Governed v2 compares:

1. official best time (`raw best + finite penalty`);
2. second-best raw valid attempt;
3. third-best raw valid attempt.

Missing second/third attempts compare as infinity. Equal complete governed-v2 keys retain equal competition ranking (`1, 1, 3`). Name/participant remains display-only and never breaks a true performance tie.

## Explicit placement publication scope

A permanent placement claim must carry its complete scope. `Rank = 1` by itself is forbidden.

SP-4F defines the minimum publication scope as:

- rule version;
- participant type (`Individual`, `Doubles`, or `Timed Relay`);
- one explicit competition-snapshot division;
- event (`3-3-3`, `3-6-3`, or `Cycle`);
- category (`normal`, `special`, or `mixed`);
- gender scope (`all`, `M`, or `F`).

`division = all`, omitted category, or omitted gender is not a valid permanent-placement publication scope.

The policy helper produces a deterministic scope key from these fields so a future server projection can persist or verify exactly what field a rank belongs to.

## Historical division authority

SP-4F resolves the historical provenance rule conservatively:

**For legacy historical placement, the authoritative division is the competition-state participant division snapshot that the operator Finals engine ranked at that competition.**

Permanent identity fields must never be used to rewrite that historical division.

If a trustworthy competition-state participant division snapshot is unavailable or ambiguous, NADITrack must not fabricate a historical placement by recalculating a standard division from today's identity or today's rules. That competition remains in factual Finals history without a permanent placement claim.

For future governed-v2 competitions, a later activation phase must persist an immutable ranking snapshot at the appropriate competition finalization boundary. That snapshot must include the chosen rule version and the exact publication scope/division provenance required to reproduce the placement later.

SP-4F introduces no schema change and does not create that persisted snapshot yet.

## Activation boundary

`governed-finals-v2` is a reusable policy definition only. It is deliberately not referenced by `app.js` or `FinalsReportEngine.js` in SP-4F.

Therefore SP-4F does **not**:

- switch any current or future competition to v2;
- modify existing Finals screen/print/CSV ordering;
- recalculate any winner;
- rewrite stored results;
- add rank/placement to the public Stacker profile;
- add a rule-version database column;
- infer v2 from competition date or software version.

A later phase should introduce the explicit persistence/activation boundary and only then consider server-side permanent placement projection.

## Public career boundary

SP-3A Personal Bests, SP-4A Tournament History, SP-4C Career Progression and SP-4D Finals Career remain factual timing projections and are unchanged.

SP-4F still publishes no placement, podium, medal or award history. In particular, the factual SP-4D official time must not be sorted ad hoc in the browser to derive placement.

Permanent placement remains blocked until the future activation/snapshot phase can prove:

- explicit rule version;
- immutable competition scope;
- authoritative historical division provenance;
- reproducible ranking inputs;
- finalized/public competition eligibility.

## Implementation surface

SP-4F adds `wwwroot/js/reports/FinalsRankingPolicy.js` as an isolated, reusable policy module.

It exposes:

- canonical v1/v2 version constants and descriptors;
- safe version resolution;
- legacy and governed classification;
- legacy and governed tie keys;
- versioned eligible-row ranking;
- explicit publication-scope validation and deterministic scope keys.

The module is not loaded by the current operator application. Existing `BestResultEngine.js` and `FinalsReportEngine.js` remain unchanged and authoritative for current live competition behavior.

## Validation

`tests/stacker-identity-v1-sp4f-finals-ranking-governance.test.js` verifies:

- missing version resolves to legacy v1;
- unknown non-blank versions fail closed;
- legacy v1 classification/tie key remains compatible with the existing Finals engine;
- finite penalty changes governed-v2 primary ordering;
- `999` result-level penalty overrides otherwise-valid attempts in governed v2;
- valid attempts can coexist with scratch attempts when there is no scratch penalty;
- governed-v2 second/third attempts break equal official-best ties;
- equal complete governed-v2 keys keep competition-ranking gaps;
- permanent placement scope must explicitly include participant type, division, event, category and gender;
- the new policy is not wired into current `app.js` or `FinalsReportEngine.js`.

Required `Build and test` passed on the SP-4F pull-request head, including all existing Stacker Identity integration suites and the complete JavaScript regression suite containing this governance test.

## Deployment safety

SP-4F performs **no production deployment**, schema migration or production data write.

The production deployment hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later release must back up and hash the existing production `web.config`, exclude it from the deployment payload, deploy only intended application/static artifacts, and verify that the post-release production `web.config` hash remains unchanged.
