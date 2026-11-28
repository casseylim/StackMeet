# ENG-06C — Build isolation and negative controls candidate

Base: ENG-06B 6e3462bbb8f13fcb95912133ea348a27f18c8c2a, [CI 46115743170](https://github.com/casseylim/StackMeet/actions/runs/46115743170), all three jobs passed; regression job 140889100721 logged "ENG06B ledger validation passed: 150/150 records". New hosted CI pending.

## Changes

- The current-source regression-evidence job creates a fresh detached Git worktree beneath RUNNER_TEMP, verifies HEAD against GITHUB_SHA, then executes the existing collector in that workspace. The coverage and evidence output remains outside the source tree, while validation executes in the original checkout. Builds precede instrumented execution as before. This isolates generated files and binaries from the main checkout; it is not a sandbox or a process-level isolation claim. No changes to the pinned-source offline reproducibility job or existing build-and-test job.
- New reviewed suite tests/eng06c-negative-controls.test.js copies only required files into a disposable directory. For each case, run the original designated gate against clean files (must exit zero), deliberately mutate a single copied source (must exit 1 with the expected assertion), restore the copy (must exit zero), and verify original file hashes unchanged. Fixtures cleaned in finally.
- Finals-only policy: change the overall report's stage to Prelims in the disposable app.js; existing finals-only-overall.test.js must fail.
- Public privacy: insert a synthetic email field in the copied PublicResultsController response projection; existing public-results-portal.static.test.js must fail its email exposure assertion. This proves a static publication guard, not runtime SQL/API privacy against every possible leak.
- Stale-write protection: unconditionally capture a new ETag during an ordinary load in the copied ApiProvider; existing competition-state-concurrency.test.js must reject silent rebasing of the editor baseline. Synthetic fetch, not a production stale-write test.
- Actual independent validator CLI is invoked on a synthetic complete parent/child ledger and nonempty logs, then a Node suite exit is changed to 1; require nonzero exit, failed validation.json with matching reason, then restored pass. No real evidence is altered.
- Inventory updated to 100 top-level JS suites, 101 Node records including storage smoke, 151 total expected ledger records. No gate removed, no production credentials or endpoint invoked. Hosted validation pending.

## Outstanding

Confirm the isolated worktree and all four negative-control probes on hosted Windows CI, including exact SHA, coverage extraction and 151/151 ledger records. Full browser-based, network, accessibility, print and locale evidence remains ENG-06D. Complete operational evidence and thresholds remain ENG-06E. No claim that mutation probes exhaust rule/privacy/concurrency risks.

Production IIS web.config must never be overwritten, deleted or replaced; explicit exclusion, secure backup and identical pre/post SHA-256 required during deployment and rollback. Preserve Finals-only Overall/All-Around, separate preliminary reports, immutable certified historical snapshots. No application runtime, schema or production deployment workflow changes; no master merge or production deployment without explicit owner approval. Previous operational release gates carry forward.
