# NADITrack Stacker Identity v1 — SP-4E Finals Ranking Compatibility Contract

Status: characterization candidate

## Purpose

SP-4E characterizes the existing Sport Stacking Finals placement rules before NADITrack publishes permanent historical placement, podium, medal or award claims on a stacker's career profile.

This phase is deliberately **read-only and behavior-preserving**. It does not add placement to the public profile and it does not rewrite `FinalsReportEngine.js`. Instead, it freezes the observable behavior of the current report engine in an executable compatibility test and records the decisions that must be resolved before a later historical-placement projection can be considered authoritative.

## Existing authority surfaces

The current operator-side report behavior is split across two established browser engines:

- `wwwroot/js/results/BestResultEngine.js` owns shared result classification, finite-penalty application and `rankingTime()`;
- `wwwroot/js/reports/FinalsReportEngine.js` owns Finals tie keys, rank assignment, stage-aware placement rows, All-Around rows, report filters and organization credits.

Existing report documentation remains relevant:

- `docs/reports/FINALS_BUSINESS_RULES.md`;
- `docs/reports/FINALS_REPORT_ENGINE_SPEC.md`.

SP-4E does not declare a new ranking engine. It records how those existing surfaces behave today.

## Characterized Finals placement invariants

The dedicated SP-4E JavaScript characterization locks the following current behavior:

1. **Finals stage boundary** — `placementRows()` uses Finals rows only. A faster Preliminary result cannot displace a Finals result.
2. **Ranking scope** — placement groups are keyed by participant type, competition-snapshot division and normalized event.
3. **Valid-only placement** — only rows classified `valid` receive a numeric rank. Scratch, Missing and Invalid rows remain reportable but unplaced.
4. **Tie key** — valid attempts are sorted ascending and compared lexicographically as best, second-best, then third-best.
5. **Equal rank** — equal complete tie keys receive equal competition rank. The next distinct row uses competition ranking (`1, 1, 3`), not dense ranking.
6. **Display stability only** — participant/name ordering stabilizes display when tie keys are equal and does not break a true performance tie.
7. **Category/gender filtering** — Normal/Special/Mixed and Male/Female filtering occurs before placement grouping. The selected filter therefore forms part of the meaning of a published placement.
8. **Participant-type isolation** — Individual, Doubles and Timed Relay placement groups are separate even when division and event labels match.
9. **Division provenance** — the report engine ranks against the division present in that competition's participant metadata, not permanent-profile attributes.

All-Around, qualification snapshots and organization-credit ranking remain separate report concepts and are not converted into permanent individual career placement by SP-4E.

## Compatibility finding 1 — finite penalty ordering diverges

The current codebase has two observable ordering semantics for a normal finite penalty:

- `BestResultEngine.rankingTime(result)` returns the best valid attempt plus an applicable penalty greater than zero and less than 999;
- `FinalsReportEngine.finalTieKey(result)` compares the raw valid attempts only and does not include that finite penalty.

Therefore a result with raw attempts `[5.000, 5.200, 5.300]` and a `0.500` penalty has shared `rankingTime = 5.500`, but its Finals tie key remains `[5.000, 5.200, 5.300]`. It can rank ahead of an unpenalized `[5.100, 5.200, 5.300]` result in the current Finals report even though the shared penalty-adjusted time is slower.

SP-4D intentionally publishes factual official time as raw best plus applicable finite penalty. SP-4E therefore **must not derive historical placement by simply sorting SP-4D official times**, because doing so can disagree with the existing Finals report.

SP-4E records this divergence without changing either engine.

## Compatibility finding 2 — scratch-penalty ordering is ambiguous

`BestResultEngine.calculateBestResult()` currently checks for a valid attempt before checking whether `penalty >= 999`.

Consequently, a row with a valid attempt and `penalty = 999` is currently classified `valid`, remains ranking-eligible, and its `rankingTime()` ignores the 999 penalty because only penalties below 999 are applied.

That implementation behavior is not aligned cleanly with the broad wording in the existing Finals business-rules document that a scratch penalty should be Scratch. SP-4E locks the observed behavior in characterization so any correction must be explicit, reviewed and versioned rather than silently changing historical placement.

## Compatibility finding 3 — placement scope is contextual

A numeric rank alone is insufficient permanent career data.

The same athlete/result can have a different rank depending on the report scope chosen before grouping, including:

- Normal vs Special vs Mixed;
- Male vs Female vs Combined/no gender filter;
- division selection;
- participant type;
- event.

A future historical-placement record therefore needs an explicit, immutable scope contract. NADITrack must not publish a bare `Rank = 1` without saying what field the athlete was ranked within.

## Compatibility finding 4 — historical division reconstruction is not yet a single immutable field

The operator Finals engine consumes competition participant metadata that already contains a `division` value.

The SQL `Stacker` record stores birth date, gender, Special status and optional custom division, while standard division can be reconstructed from competition date/settings. Legacy competition state may also contain a historical division snapshot.

Before permanent historical placement is published, the system must define which historical source is authoritative when those representations differ. SP-4E does not rewrite old registration snapshots or guess a division from permanent identity information.

## Publication gate

SP-4E establishes the following hard gate:

**Permanent historical placement, podium, medal or award claims remain unpublished until the penalty semantics and placement-scope/division provenance decisions are explicitly resolved.**

A later phase may proceed only after choosing and documenting one reviewed approach, for example:

- preserve the legacy Finals report semantics exactly for historical competitions; or
- introduce a versioned corrected ranking contract that can distinguish legacy results from newly governed results.

The choice must not be made implicitly by the career-profile code.

## What SP-4E does not change

SP-4E does not change:

- `FinalsReportEngine.js`;
- `BestResultEngine.js`;
- competition scoring or qualification;
- saved Finals results;
- rank, tie, division, Special or gender behavior;
- All-Around calculations;
- awards planning or medal tables;
- SP-3A Personal Bests;
- SP-4A Tournament History;
- SP-4C Career Progression;
- SP-4D Finals Career factual history;
- public profile API models or UI;
- Doubles/Relay permanent career statistics;
- Chess or other activity modules.

No schema migration, production database write or production deployment is part of this phase.

## Validation

`tests/stacker-identity-v1-sp4e-finals-ranking-compat.test.js` executes the production `BestResultEngine.js` and `FinalsReportEngine.js` unchanged and verifies:

- best/second/third attempt tie ordering;
- equal-rank and rank-gap behavior;
- Finals-only placement;
- participant-type/division/event isolation;
- non-valid rows remain unplaced;
- Normal/Special/Mixed and gender filter scope;
- finite-penalty divergence between shared `rankingTime()` and Finals `finalTieKey()`;
- current valid-attempt + 999-penalty classification behavior.

Because CI already executes every top-level `tests/*.test.js`, the compatibility characterization joins the complete JavaScript regression suite without modifying CI configuration.

## Deployment safety

SP-4E performs no production deployment.

The production deployment hard rule remains binding: production `web.config` must never be overwritten, replaced or regenerated. Any later release must back up and hash the existing production `web.config`, exclude it from the deployment payload, deploy only the intended application/static artifacts, and verify the production `web.config` hash remains unchanged afterward.
