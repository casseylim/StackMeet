# StackMeet Technical Design Document

## Purpose

This is the index for StackMeet's living technical design. It records the current observable system before business-rule refactoring. A proposed rule change must update the relevant specification and characterization tests before implementation.

## Current architecture

StackMeet is a browser-based competition administration application. The current runtime is centered on `index.html` and `app.js`, with browser state persisted through the existing storage provider and XML used as the portable competition format. The hosted API and SQL model are supporting infrastructure; the browser application remains the current business-rule execution boundary for the competition workflows.

## Phase A baseline

Phase A is characterization-only. It freezes current behavior, including known defects, so that later modularization can distinguish a deliberate rule change from an accidental regression.

| Area | Current source of truth | Characterization reference |
|---|---|---|
| Division assignment | `app.js` compatibility functions | `docs/characterization/DIVISION_RULE_MATRIX.md` and `tests/characterization.test.js` |
| Competition state | normalized browser state | `docs/architecture/STATE_SCHEMA.md` |
| Results and finals | `app.js` plus results/finals engines | `tests/characterization.test.js`, `tests/prelim-save-pipeline.test.js` |
| Awards | current award planner | `BEHAVIOR_CATALOG.md` AWD-001 through AWD-003 |
| Reports | current report/filter functions | `tests/competition-report-engine.test.js` and characterization IDs |
| Public results | `wwwroot/results/results.js` and API payloads | `tests/public-results-portal*.test.js` |
| Import/export | XML state conversion in `app.js` | `tests/characterization.test.js` STO-002 through STO-004 |

## Business-rule boundaries

The current application does not yet have a single business layer. Division, team, result, award, report, and import/export rules are distributed across the browser shell and supporting modules. The dependency map records this current state; it is not a target architecture.

## Refactoring gates

1. The Phase A baseline must be reproducible.
2. New characterization tests must cover the affected observable workflow.
3. Existing failures must be classified as baseline defects or regressions.
4. A refactor must preserve the division invariants documented in the division matrix.
5. Production publication is not part of characterization work.

## Current baseline

As of 2026-08-03, syntax validation and the storage smoke test pass. The full characterization harness currently stops at `TEAM-003` because the test expects `relayTeamStatus()` to return `Locked` for a complete relay after `2099-01-01`, while the current implementation returns `Ready`. This is recorded as a pre-existing baseline discrepancy; Phase A does not change it.

The independent JavaScript checks currently have this baseline:

| Check | Result | Classification |
|---|---|---|
| `js/storage/storage-smoke.test.js` | Pass | Healthy baseline |
| `tests/competition-report-engine.test.js` | Pass | Healthy baseline |
| `tests/prelim-save-pipeline.test.js` | Pass | Healthy baseline |
| `tests/public-results-portal.sample-data.test.js` | Pass | Healthy baseline |
| `tests/characterization.test.js` | Stops at `TEAM-003` | Existing relay status discrepancy |
| `tests/competition-admin-phase1.static.test.js` | Fail | Root/hosted `admin.js` drift; unrelated to division behavior |
| `tests/public-results-portal.static.test.js` | Fail | Results script cache-buster mismatch; unrelated to division behavior |

These failures are tracked as baseline findings. They should not be silently converted into passing expectations during a division refactor.

## Remaining Phase A gap

The Node characterization harness now verifies XML export, but a true `xmlToState()` import round-trip still requires a browser DOM implementation. No XML DOM parser is currently installed for the Node harness. The import checkpoint must therefore be completed in a browser-runtime test or with an explicitly approved test dependency; a hand-written test-only parser would not faithfully characterize the production path.

## Future target

After Phase A, rules may be extracted behind stable APIs: DivisionEngine, ResultEngine, QualificationEngine, AwardEngine, TeamEngine, ReportEngine, and ImportExportEngine. Extraction is not approved merely because a unit test passes; the end-to-end workflow must remain equivalent first.

## Related documents

- `BEHAVIOR_CATALOG.md`
- `BUSINESS_RULES.md`
- `CHARACTERIZATION_TEST_PLAN.md`
- `docs/characterization/DIVISION_RULE_MATRIX.md`
- `docs/characterization/COMPETITION_FLOW_MATRIX.md`
- `docs/characterization/DIVISION_DEPENDENCY_MAP.md`
- `docs/architecture/COMPETITION_PACKAGE_SPEC.md`
- `docs/architecture/STATE_SCHEMA.md`
