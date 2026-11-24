# ENG-06A regression evidence

Baseline de24245383a0940d4da1c369bdcd251b49868692. This draft is stacked on ENG-01 so the reporting PR contains only CI, the runner and evidence. The coverage workload includes ENG-01's SQL-backed HTTP and browser cache tests.

| File | Change |
|---|---|
| tests/Invoke-RegressionEvidence.ps1 | Discover and build all console test projects. Execute the compiled suites serially. Record suite type, exit status, duration, output path and source/runtime metadata. Run top-level JavaScript tests, storage smoke and simulated release safety. Optionally collect Cobertura from the .NET child processes and include their individual exits in the parent ledger. |
| .github/workflows/ci.yml | Run the evidence job with a pinned coverage collector and isolated LocalDB. Upload logs, JSON and Cobertura even after failure. Retain the existing build, regression and reviewed-source reproducibility gates during rollout. |
| docs/engineering/ENG-06A_COMPLETION_REPORT.md | Record results, comparison and acceptance. |

ENG-00 executed the console suites through a local audit runner. The existing CI job listed projects and console invocations individually, and its final JavaScript loop stopped after the first failure. The additional evidence job discovers projects, runs independent suites after individual test failures, and writes the outcomes before returning failure. Suite artifact logs are scoped to synthetic runners. The separately parameterized PowerShell smoke scripts keep their existing fixture-specific entry points. The runner does not invoke scripts with production defaults.

## Evidence

Windows, SDK 10.0.400, Node 24.18.0, dotnet-coverage 18.12.0. Tested code and runner at 054325cf47803042ab3ebb24069e7555725eb513. All 24 .NET builds and all 24 instrumented console suites exited zero. All 85 top-level JavaScript suites, storage smoke and 68 offline release-safety cases passed. The ENG-01 scenarios ran inside CoreIntegrityIntegrationTests. A separate missing-collector probe exited 1 and wrote an Infrastructure failure to the JSON ledger.

| Measurement | ENG-00 | ENG-06A with ENG-01 |
|---|---:|---:|
| API lines | 71.39% | 72.88% (+1.49 pp) |
| API branches | 53.13% | 56.92% (+3.79 pp) |
| CompetitionResultsController hit / mapped lines | 0 / 100 | 100 / 117 |
| CompetitionStateController hit / mapped lines | 98 / 132 | 113 / 139 |
| StackersController hit / mapped lines | 0 / 75 | 32 / 95 |

The [machine-readable summary](evidence/eng-06a-regression-summary.json) includes the per-suite exit ledger and controller coverage. ENG-00 references use its [validation report](ENG-00_VALIDATION.md) and [controller coverage ledger](evidence/coverage-net.md).

The API package rate uses the Cobertura package named StackMeet.Api. Controller hit/mapped figures deduplicate line numbers by source file. API-wide mapped lines can change with compilation and test execution. Compare the rates with the same collector and source selection. The evidence job does not aggregate Node and browser coverage into the API metric.

## Acceptance and rollback

Accept once the draft's evidence job publishes a parseable ledger and coverage report, discovers every baseline .NET and JavaScript suite, and fails with a reported nonzero status when any existing suite fails. Review the synthetic SQL results as part of ENG-01. Preserve the offline release guard and reviewed-source reproducibility jobs. Review added CI time and put a minimum coverage gate in a separate change after measurements stabilize.

The coverage job runs alongside the existing CI path to retain its independent gates during review. This adds runner time. The local execution proves suite discovery and collection in the test workstation environment. Hosted runner evidence is available when the draft job completes. Roll back by reverting this workflow and runner change. The previous CI test and release procedures can continue against their existing entry points.
