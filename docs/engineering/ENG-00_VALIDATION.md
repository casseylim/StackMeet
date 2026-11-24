# Regression, coverage, rules and user experience

Fresh execution used a clean clone of engineering/eng-00-baseline. Its starting head a34e75b differed from a3042ef only by ENG-00_INITIAL_AUDIT.md. Windows, Node 24.18.0, .NET SDK 10.0.400, .NET 8.0.28, SQL LocalDB and Edge 158.0.4370.33. The baseline CI workflow at .github/workflows/ci.yml:66 onward restores console harnesses separately from the solution. Running dotnet test on the solution alone would miss these suites.

| Assessment | Execution and evidence |
|---|---|
| Build | dotnet restore StackMeet.sln --configfile NuGet.Config, then dotnet build StackMeet.sln -c Release --no-restore. Zero errors, three nullable warnings in historical placement projection. |
| Backend | Restored each tests/*/*.csproj against NuGet.Config and ran dotnet run --project PROJECT -c Release --no-restore. All 24 projects passed. StackerCareerProgressTests initially encountered MSB3027/MSB3021 because the benchmark held the output DLL. Its isolated retry passed and the independent covered run also passed. |
| JavaScript | Ran each tests/*.test.js and wwwroot/js/storage/storage-smoke.test.js with Node and NODE_V8_COVERAGE. All 85 top-level suites and storage smoke passed. Some suites inspect source text. They cannot establish browser or SQL behavior. |
| Release rollback | pwsh -NoProfile -File tests/naditrack-application-release.tests.ps1. All 68 synthetic offline cases passed. |
| Backend coverage | dotnet-coverage 18.12.0 collect, Cobertura format, over a child PowerShell runner executing all 24 Release console assemblies. All returned zero under collection. StackMeet.Api alone had **71.39% line and 53.13% branch coverage**. File-level mapped lines are available in the [coverage ledger](evidence/coverage-net.md). |
| Frontend coverage | V8 union of observed function-entry ranges across Node suites gave **81/99** entries for canonical first-party production script URLs. A separate real-browser, empty-dashboard matrix across six viewport/language cases gave **71/187** entries. VM-loaded code under synthetic filenames and unvisited pages are outside those denominators. Combining these values into a repository-wide percentage would be misleading. [Node](evidence/coverage-node.md) · [Browser](evidence/coverage-browser.md) |

The [suite ledger](evidence/suites.md) lists individual outcomes. The initial audit's successful historical CI remains separate from these runs.

## Competition invariants

| Contract | Evidence on synthetic data | Residual question |
|---|---|---|
| Entry and qualification | tests/characterization.test.js:71 onward covers independent ID prefixes, DOB normalization, age on event date, divisions, doubles eligibility, relay readiness and qualifier snapshot export. | Golden cut-line decisions for every sport/division require competition-owner sign-off. |
| Scoring and awards | Characterization includes 27 scenarios. Valid times, scratch-only results, second/third tie-breaks, equal ranks, team organization credit, gender/category filters, all-around and award planning passed. CoreIntegrityIntegrationTests/Program.cs:31 exercises backend normalization, precision and invalid relay events. | Cross-view helper comparison below found two exceptions. |
| Persistence and boundaries | CoreIntegrityIntegrationTests/Program.cs:189 onward ran actual loopback HTTP with SQL. Wrong-competition references, stale ETags, malformed state and removal of a team with existing results were rejected. The six-scale parallel saves preserved one winning revision. | Close/save, participant removal/save and row-level result retries still need a complete interleaving matrix. |
| Certified Finals | StackerFinalsRankingGovernancePersistenceTests, certification readiness/activation and historical placement projection suites passed. Fixtures cover legacy snapshot shape, guarded activation and immutable projection. | No real athlete snapshot was consulted. Preserve existing digest and legacy version contracts in subsequent work. |
| Identities and career | Matching, resolution, backfill, career progression, personal records, public profile and team/finals placement suites passed. Public publication and historical facts have separate contracts. | Legal consent and current publication policy require owner review. |
| Activity and translations | Phase4C and Phase5A suites passed for assignment and registered modules. Multilanguage suites and language fallback tests passed. | A new activity requires independent rules fixtures and native-language review. |
| Backup exchange | Edge invoked baseline stateToXml and xmlToState with synthetic state, escaped characters and a Malay dictionary entry. The 13,136-byte v2 XML round trip matched normalized input and preserved the translation. | This exercised browser functions with startup suppressed. Physical printer paths and full legacy XML corpus parity remain open. |

**Verified rules discrepancy, high priority.** Ran the exact public bestTime helper from wwwroot/results/results.js:1698 and the exported operator rankingTime from wwwroot/js/results/BestResultEngine.js:41. The public dashboard uses bestTime in eligibility and display at results.js:357 and :484. CompetitionResultRules.cs:26 permits zero-valued attempts and a penalty of 999 within its input bounds.

| Attempts | Penalty | Operator ranking time | Public time |
|---|---:|---:|---:|
| 0, 5 | 0 | 5 | 0 |
| 5, 7 | 999 | 5 | unranked (NaN) |
| 999 | 0 | unranked (Infinity) | unranked (NaN) |
| 5, 7 | 0 | 5 | 5 |

The public filter accepts zero and applies the scratch penalty before selecting a result. The operator helper excludes zero and classifies a valid attempt before testing the scratch penalty. The observed discrepancy concerns provisional/display computations. The audit has no evidence of a corrupted certified result. The competition owner should sign off the intended interpretation for historical and newly entered records separately.

## Portal, accessibility and languages

On the real SQL-backed English dashboard, axe-core 4.14.1 ran WCAG 2 A/AA and 2.1 A/AA rules against 100 synthetic results. It reported 24 rules passed, one incomplete and two violation types. Color contrast affected 18 nodes including the Preliminary link. Visible-label/accessibility-name mismatch affected seven nodes including the brand link at wwwroot/results/index.html:17. Automated severity was serious for both. Match visible words in the accessible name and raise foreground/background contrast for every state. Repeat the same audit and verify the incomplete item manually.

A separate intercepted empty-competition fixture exercised actual portal assets in Edge at 390 and 1,280 CSS pixels in en, ms and zh-Hans. All six presented the expected document language and localized title, displayed the synthetic competition heading, focused an anchor on the first Tab and generated nonempty print PDFs. No uncaught page errors occurred. At 390px English and Malay reached 412px document width. A repeat saw the English connection indicator extend to 421px during a different connection state. Chinese and all desktop cases fitted their viewport. wwwroot/results/results.css:29 lays out the brand, localized selector and connection indicator in one row. Test connecting, live and offline labels after reflow.

Screen-reader comprehension, full operator keyboard workflows, native-speaker fidelity, low-vision zoom and visual pagination of populated award sheets require further acceptance. Browser PDF generation alone establishes print execution, not acceptable paper output.


## Supplemental runtime scripts

All four additional tracked PowerShell scripts ran on explicitly configured loopback URLs. security-api.integration.ps1 passed its credential, malformed-save, unchanged-state, lifecycle and throttle checks. phase-1a-api.ps1 passed its synthetic competition/stacker CRUD and integrity checks. production-readonly-smoke.ps1 ran the basic health/version path on the test host. competition-admin-runtime-smoke.ps1 initially inherited the throttle test's active limiter and returned 429. On a fresh test host it returned 200 for health/version/admin list/own state, 403 for cross-competition state and 401 for administrative access with the legacy competition session. The remaining role and recovery boundaries are documented in the risk register.
