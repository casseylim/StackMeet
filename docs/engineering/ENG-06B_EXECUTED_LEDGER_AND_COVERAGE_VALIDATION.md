# ENG-06B — Executed-suite ledger and API coverage validation candidate

Base: ENG-06A 9a04e9ecb0251a7bd14e3a96de9b0ec69fc6eb02, draft PR #113, [CI 46114928355](https://github.com/casseylim/StackMeet/actions/runs/46114928355), all three jobs passed. Downloaded hosted regression artifact 15251426297. Its ledger identifies the exact base commit, SDK 11.0.100, Node v24.22.3 and Microsoft Windows 10.0.26100; 24 Build, 24 DotNet, 99 Node including storage smoke, one Coverage and one PowerShell entry (149 total).

## API coverage baseline

The exact base Cobertura package StackMeet.Api has 447 distinct class entries. Count only classes/class/lines/line entries, not duplicated method-level entries or whole-report totals. Branch covered/total counts are extracted from condition-coverage fractions on branch lines.

| Metric | Covered | Total | Rate |
|---|---:|---:|---:|
| API class-level lines | 10,969 | 14,166 | 77.431879% |
| API branches | 2,561 | 3,934 | 65.099136% |

Counts reproduce the package's reported rates. The class-level line counting unit is not deduplicated physical source lines. Whole report includes other packages; do not mix Node/browser evidence into API percentages. This is descriptive baseline evidence, not a target or coverage threshold.

## Implementation

- Add independent Node validator after the existing regression-evidence collector, before always-upload artifact step. Require the reviewed inventory's exact build, console, Node, storage, offline PowerShell and coverage entries; each must appear once, exit zero, have a nonnegative duration and the expected nonempty log under the evidence directory. Reject unknown infrastructure entries, missing logs, duplicates and path substitution.
- Require source SHA matches GITHUB_SHA, schema and nonempty SDK/Node/OS metadata. Independently validate covered child ledger against DotNet-only/no-build mode and compare its metadata and merged result fields against the parent. This does not cryptographically attest execution; CI checkout/runner and collector remain trusted.
- Extract API-only numerators/denominators into api-coverage.json from Cobertura via PowerShell XML parser; check count ranges, nonzero totals and agreement with reported rates in validator. Require source SHA match. Write validation.json with verdict, counts, errors and API summary; fail the CI step on invalid evidence. Existing collector and independent build/release gates remain; artifacts still upload after failure.
- New reviewed JS test covers missing, duplicate, unexpected, invalid exit, duration, source, metadata, path, missing/empty log, wrong package, invalid denominator/rate. Adds one JS suite to inventory (99 top-level, 100 Node including storage, 150 total ledger entries anticipated). Test does not yet exercise every filesystem/CLI failure or intentionally broken business rule fixture. Hosted CI pending.

## Remaining gates

ENG-06C: build isolation, evidence failure propagation and designated rule/privacy/stale-write negative controls. ENG-06D: isolated real browser workflows, locale, accessibility and print evidence. ENG-06E: completion review and cost/threshold decisions. The new validation relies on collector XML extraction and trusted CI; no independent XML parsing in the Node validator, no assertion-count guarantee, and no coverage threshold imposed. No production connection, deployment workflow, runtime, ranking or schema changes.

Production IIS web.config must NEVER be overwritten, deleted or replaced in deployment, sync or rollback. Explicit exclusion, secure backup, identical pre/post SHA-256 and stop on inability to verify remain mandatory. Overall/All-Around Finals only, preliminaries separate, certified historical bytes and digests immutable. All previous operational acceptance gates remain. No master merge or deployment without explicit owner approval.
