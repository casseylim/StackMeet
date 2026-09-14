# NADITrack Stacker Identity v1 — SP-4H Operator Finals Ranking Version Activation

Status: implementation candidate

## Purpose

SP-4H makes the existing operator **event-level Finals** ranking path explicitly consume the persisted SP-4G ranking rule version without changing historical defaults, publishing permanent placement, or enabling operator rule selection.

The activation boundary is deliberately narrow:

- a persisted `legacy-finals-v1` selection keeps the frozen existing operator behavior;
- a persisted `governed-finals-v2` selection makes event-level Finals classification/tie ordering use the reviewed SP-4F v2 contract;
- an unversioned competition still resolves to `legacy-finals-v1`;
- an unknown version fails closed;
- a selected SQL competition whose rule registry is unavailable fails closed rather than silently downgrading to v1.

## Read-only persisted rule projection

`FinalsRankingRulesController` exposes an authenticated, read-only `GET /api/competitions/finals-ranking-rules` projection for Sport Stacking competitions the current session may access.

The response contains only:

- competition ID;
- effective rule version;
- whether the rule was explicitly persisted.

The endpoint is `no-store`. It does not expose snapshot JSON, source revisions, participant data, results, selection actor data, or ranking evidence.

SP-4H adds **no rule-selection endpoint** and does not call `SelectRuleVersionAsync` from the controller. It also does not call `CaptureFinalizedSnapshotAsync`.

## Operator activation

During normal SQL-native competition initialization, `StackerApi` loads both the accessible competition list and the effective Finals ranking-rule registry. It also loads the reviewed `FinalsRankingPolicy.js` module before SQL initialization completes.

`FinalsReportEngine` resolves the selected SQL competition from the existing session key and then requires a matching rule in that registry.

Compatibility behavior remains explicit:

- no SQL-selected competition (local/offline compatibility context) resolves to `legacy-finals-v1`;
- a SQL-selected competition without a loaded rule fails closed;
- blank/unversioned server governance resolves to `legacy-finals-v1` at the server boundary;
- explicit unknown versions fail closed in the shared policy contract.

No version is inferred from dates, software builds, result content, or current permanent identity data.

## Event-level Finals behavior

For event-level Finals rows, placement and organization-credit inputs:

- `legacy-finals-v1` keeps the current valid-result classification and raw best/second/third attempt tie key;
- `governed-finals-v2` treats a result-level `999` penalty as Scratch even when otherwise-valid attempts exist;
- `governed-finals-v2` orders by official best time (`raw best + finite penalty`) followed by second-best and third-best raw valid attempts;
- true equal complete keys retain competition ranking gaps (`1, 1, 3`);
- display-name ordering remains a stable presentation tiebreak only and never becomes rank evidence.

The browser does not invent a third ranking contract. It delegates version-specific classification and tie-key semantics to `FinalsRankingPolicy.js`.

## Deliberately unchanged in SP-4H

### Prelims

Prelims remain on the frozen legacy result/tie semantics. SP-4H does not allow a Finals governance choice to change qualification/preliminary reporting.

### All-Around

All-Around is outside the SP-4H activation scope and remains explicitly on the existing legacy-compatible calculation path. Its governance must be reviewed separately before any rule change.

### Qualification snapshots

The existing `final-qualification-v1` qualification snapshot contract is unchanged.

### Historical placement

SP-4H still does not publish historical placement, podium, medal, award, record-holder or ranking claims on permanent athlete profiles.

### Governed-v2 historical certification

SP-4G's `governed-finals-v2` snapshot-capture block remains in force during SP-4H. This phase proves the operator engine can consume v2, but it does not yet change the server certification rule. A later reviewed phase may decide when sufficient durable evidence exists to unblock v2 finalized snapshot certification.

## Failure behavior

The activation path is fail-closed by design:

- unsupported persisted rule version → error;
- SQL-selected competition with no loaded rule registry entry → error;
- explicit governed rule with the reviewed policy module unavailable → error.

This is safer than silently reverting to legacy rules after an operator competition has explicitly selected another version.

## Tests

`tests/stacker-identity-v1-sp4h-finals-ranking-activation.test.js` characterizes:

- unselected/local compatibility fallback to legacy v1;
- missing selected-competition rule fails closed;
- unknown rule fails closed;
- legacy finite-penalty ordering remains unchanged;
- governed-v2 finite penalty changes event-level Finals ordering as reviewed;
- governed-v2 `999` penalty removes a result from Finals ranking;
- Prelims remain legacy while Finals v2 is selected;
- All-Around remains outside SP-4H and legacy-compatible;
- the server projection is read-only and `no-store`;
- the client loads the reviewed policy module and rule registry.

Existing SP-4E/SP-4F/SP-4G regression tests remain authoritative for the compatibility findings, rule definitions and immutable persistence boundary.

## Next boundary

After SP-4H is proven and merged, the next safe phase should review **governed-v2 certification readiness**. That phase can decide whether and how to unblock `CaptureFinalizedSnapshotAsync` for a v2 competition while proving that the finalized source evidence corresponds to the operator rule actually used.

Permanent historical placement projection should remain later than that certification step.

## Deployment safety

SP-4H performs **no production deployment** and applies no migration to production. SP-4G's migration remains a separately controlled production-release prerequisite for any future activation release.

The production deployment hard rule remains binding: the existing production `web.config` **must never be overwritten**, replaced, regenerated or published over. Any later release must back up and hash the production `web.config`, exclude it from the payload, deploy only separately approved artifacts, and verify the production `web.config` hash remains unchanged afterward.
