# Finals Report Engine

## Current operator pipeline

Pipeline: competition state → Finals stage → participant/category/gender/filter selection → shared classification/ranking → DTO rows → screen, print, and CSV.

`js/reports/FinalsReportEngine.js` remains the current operator authority for result classification, tie keys/ranks, final placement rows, All-Around rows, category/gender filters, and organization credits. Renderers do not reimplement ranking.

SP-4E names this frozen current behavior `legacy-finals-v1` for compatibility purposes. SP-4F does not modify or replace the current engine.

Qualification snapshots are created only by the explicit **Generate Draft Qualification Snapshots** action. Reports never regenerate them. Approved snapshots are immutable; a confirmed regeneration preserves them and marks them Superseded before creating new Draft records.

## Versioned governance boundary

`js/reports/FinalsRankingPolicy.js` defines the ranking-governance boundary introduced by SP-4F:

- `legacy-finals-v1` reproduces the existing operator placement semantics;
- `governed-finals-v2` defines corrected future semantics but is not activated by the current application.

The policy module is intentionally not referenced by `app.js` or `FinalsReportEngine.js` in SP-4F. This prevents a documentation/governance phase from silently changing current competition results.

Unversioned competitions resolve to `legacy-finals-v1`. Unknown non-blank versions fail closed. `governed-finals-v2` requires an explicit future persistence/activation step; there is no date-based or software-version-based automatic upgrade.

## Governed-v2 ranking key

For a governed-v2 Valid Finals result, the comparison key is:

1. official best time = lowest valid attempt + finite result-level penalty;
2. second-best raw valid attempt;
3. third-best raw valid attempt.

A result-level penalty of `999` or greater is Scratch and ineligible for governed-v2 ranking. Equal complete keys retain competition ranking (`1, 1, 3`).

## Permanent placement scope

A permanent historical placement cannot be represented by a rank number alone. Future server-side placement snapshots must carry at least:

- ranking rule version;
- participant type;
- one explicit competition-snapshot division;
- event;
- category scope;
- gender scope;
- enough finalized ranking input/provenance to reproduce the result.

For legacy historical competitions, the operator-era competition-state participant division snapshot is authoritative when available. If that historical division snapshot is unavailable or ambiguous, permanent placement should remain unpublished rather than be reconstructed from current permanent-identity data.
