# ENG-00 engineering baseline audit

**Assessment date:** 24 November 2026. **Source:** casseylim/StackMeet at a3042efca304feab8e77f87ff20fc24675461e9c. **Disposition:** all twelve assessment areas have evidence or implementation plans. Release remains blocked. Owner acceptance of the baseline, rule interpretation and residual limitations is pending.

The working branch started at a34e75b37a9e26ed69b3ef8f7c74a0dcba79f612, one documentation-only change beyond baseline. Measurements used a separate checkout, generated local databases and loopback or intercepted browser fixtures. This report supplements the [initial audit](ENG-00_INITIAL_AUDIT.md). Later evidence supersedes its preliminary statements about unavailable tests and preflight evidence.

## Decisions for the owner

1. Keep release run 37912223752 ineligible. Its baseline listing did not contain the required admin.html. The governed checks worked. The infrastructure reason remains unresolved.
2. Resolve the public/operator scoring discrepancy for zero-valued attempts and scratch penalties before consolidating calculation code. Preserve all historical certification and projection versions.
3. Prioritize result write intent and lifecycle integrity. Full-state ETags held under contention at all six sizes. Per-result compatibility and lifecycle races remain separate risks.
4. Triage the three high-severity transitive advisory matches and agree on publication boundaries. A package match alone does not establish exploitable deployment exposure.
5. Set service and recovery targets before committing to supported event size or recovery time. At 5,000 participants / 100 readers the full public response was 1,427,028 bytes and GET p95 was 4.38 s. Contested state-save p95 ranged from 14.0 to 24.6 s across two bursts.

## Assessment ledger

| Area | Completed assessment | Evidence |
|---|---|---|
| 1. Source | Inventory of 511 tracked files, architecture and authority review, 71 HTTP controller actions in 20 controllers, plus hub and route review | [Architecture and risk register](ENG-00_ARCHITECTURE_AND_RISKS.md) · [Inventory](evidence/inventory.md) · [Actions](evidence/endpoints.md) |
| 2. Complexity, duplication, dependencies | 1,948 browser functions and 400 C# methods parsed. 218 overlapping ten-line duplicate windows. Resolved package and internal dependency graphs. | [JS complexity](evidence/complexity.md) · [C# metrics](evidence/csharp-complexity.json) · [Duplication](evidence/duplication.md) · [Type/script dependencies](evidence/dependencies.md) · [Packages](evidence/packages.md) |
| 3. Security and privacy | Authentication, role and publication boundaries, asset handling, session revocation, private-key signature history check and NuGet scan. Seven package/advisory matches across five transitive packages. | [Security assessment](ENG-00_ARCHITECTURE_AND_RISKS.md) · [Advisories](evidence/nuget.md) |
| 4. Data integrity and concurrency | Reviewed unique keys, lock boundaries and result revisions. Actual LocalDB reference, certification and identity suites passed. All 12 contested state-save bursts produced one correct winner. | [Risk register](ENG-00_ARCHITECTURE_AND_RISKS.md) · [Concurrent edits](ENG-00_PERFORMANCE.md) |
| 5. Regression | 24 .NET suites, 85 JavaScript regression scripts, storage smoke and 68 offline release-safety cases passed. Supplemental PowerShell runtime checks are recorded separately. | [Validation](ENG-00_VALIDATION.md) · [Suite ledger](evidence/suites.md) |
| 6. Coverage | Release API 71.39% line / 53.13% branch. Separate canonical Node and live browser function-entry measurements with denominator limitations. | [Backend coverage](evidence/coverage-net.md) · [Browser coverage](evidence/coverage-browser.md) · [Node coverage](evidence/coverage-node.md) |
| 7. Performance | All six participant/operator shapes, 564 validated full reads, 376 contested writes, size/latency/memory/SQL waits, browser navigation and reconnect. | [Performance report](ENG-00_PERFORMANCE.md) |
| 8. Competition rules | Characterization, SQL integrity, immutable Finals, individual/doubles/relay rules, award planning and cross-view scoring vectors. Two differences verified. | [Rule matrix](ENG-00_VALIDATION.md) |
| 9. UI, accessibility, languages | Real Edge render, keyboard entry, print execution, responsive widths and three locales. axe identified contrast and accessible-name findings. | [UI and language evidence](ENG-00_VALIDATION.md) |
| 10. Deployment and recovery | Synthetic file transaction/rollback, checksum SQL backup, independent database restore and DBCC CHECKDB, XML and portable package checks. | [Recovery report](ENG-00_RELEASE_AND_RECOVERY.md) |
| 11. Failed release | Workflow and payload SHAs distinguished, stop condition located at snapshot check, preflight failure and skipped deploy verified against GitHub. | [Incident report](ENG-00_RELEASE_AND_RECOVERY.md) · [Actions run](https://github.com/casseylim/StackMeet/actions/runs/37912223752) |
| 12. Implementation planning | ENG-01 through ENG-09 each have dependencies, bounded changes, verification and rollback criteria. | [Implementation plans](ENG-01_TO_ENG-09_IMPLEMENTATION_PLANS.md) |

## Acceptance still required

* **Competition owner** — sign off scoring vectors, qualification edge cases and legacy-policy interpretation. Existing fixtures demonstrate tested behavior. New interpretations require separate governance.
* **Security and privacy owners** — confirm listing/link access, metadata policy, credential lifecycle, advisory exposure and complete cross-role coverage. The history scan covered signatures in reachable history. Hosting configuration, rotating secrets and deleted remote history are outside this assessment.
* **Operations** — reconcile the deployment root under separate authorization. Approve RPO/RTO and rehearse coordinated database, uploaded asset, key and configuration recovery. LocalDB and simulated file transport do not certify a production site restore.
* **QA and accessibility** — validate assistive technology and operator keyboard workflows, native-language quality and printed awards on target devices. Measure sustained mixed traffic and more than one reconnecting browser before establishing capacity. The benchmark did not exercise WAN, IIS, production hardware or a failover cluster.
* **Engineering owner** — accept or assign the above limitations, plus the remaining concurrency interleavings. Line coverage leaves gaps, including the batch result controller in the .NET coverage run. Standalone benchmarks and supplemental HTTP scripts ran separately from that collector.

Recommended sequence is ENG-01 first, with ENG-06 evidence and ENG-08 advisory triage in parallel. Follow with ENG-02, ENG-03, ENG-04, ENG-05 and ENG-07. Complete ENG-09 after rule-owner approval. Every implementation, migration proposal and release remains a separate governed decision.
