# ENG-04F — Storage authority and recovery completion candidate

Disposition: engineering candidate, not production acceptance. Stacked draft; no master merge or deployment. ENG-04F CI pending.

## Exact evidence ledger

| Package | Validated head | CI run | Scope |
|---|---|---|---|
| ENG-04A | 7c555e764147c26fcbf6f789d175849e81a314e9 | [45623464636](https://github.com/casseylim/StackMeet/actions/runs/45623464636) | Source authority/recovery baseline |
| ENG-04B | 76b04756fab89f5ca3fcf3dc170b03cad1fac6dc | [45625411565](https://github.com/casseylim/StackMeet/actions/runs/45625411565) | SQL/JSON precedence and historical evidence |
| ENG-04C | 6ebc73d3ae6c6dca9190ca2332d5bb05dd9b8021 | [45718592696](https://github.com/casseylim/StackMeet/actions/runs/45718592696), attempt 2 | Read-only reconciliation; guarded LocalDB only |
| ENG-04D | 3331140af769d9422f81345ed94169d2f75a8d23 | [45731926128](https://github.com/casseylim/StackMeet/actions/runs/45731926128) | Representative plans, IO, API timings, lock and stale semantics |
| ENG-04E | 991bf694a4c5ffc51cbdeb0092ddf382b7b152b0 | [45760271425](https://github.com/casseylim/StackMeet/actions/runs/45760271425) | Coordinated synthetic backup/restore |
| ENG-04F | Current draft | Pending | Cross-package recovery/reconciliation validation |

Each listed A–E workflow completed success, including Build and test, Regression evidence and API coverage, and Offline DLL reproducibility analysis. The latter checks a separately pinned historical source, not a release approval for the ENG-04 head. Earlier failures remain recorded in the package reports; corrected tests preserve immutability, lock proof and 64-bit identity IDs.

## Reviewed ENG-04E execution

Build job [139708400492](https://github.com/casseylim/StackMeet/actions/runs/45760271425/job/139708400492) emitted PASS ENG04E coordinated synthetic recovery and CoreIntegrity LocalDB integration harness passed, followed by generated source cleanup. Backup and verification 1023.594 ms; restore and verification 1169.082 ms; backup 7,065,600 bytes; 11 competitions, 560 stackers, 549 results. Whole generated SQL database restored; selected competition file/key/release checks only. No operational RTO/RPO claim.

Repeated ENG-04D evidence in that same run (32/512 participants): roster reads 2/5, results 2/9, team references 2/9, individual lookup 2/2; all four representative query plans Index Seek. Public API median/P95 5.099/5.673 ms and 14.829/25.932 ms. Stale state median/P95 4.640/5.040 and 7.136/7.477 ms. Both SQL timeout/release probes passed. Prior run values differ because of runner/workload variability; do not infer a speed improvement. Retain existing transaction order and indexes; no performance change justified by this limited evidence.

## Integration validation

ENG-04F links the existing ENG-04C inspector to the ENG-04E source and restored database, with separate temporary asset copies using its existing fixture restrictions. Require Pass/no findings and identical state revision 4/result revision 2 in the selected clean recovery fixture. Negative control modifies only the disposable restored asset copy: require Review with AUTH-07-ASSET-MISMATCH and unchanged damaged copy (no automatic repair); recheck clean source and original recovered file. No database, backup, application, production path or inspector authorization changes. Recovery timing excludes this added integration step. New assertions pending hosted execution.

## Exit assessment against original ENG-04 plan

| Original exit / safeguard | Evidence | Disposition |
|---|---|---|
| Legacy and SQL fixtures project identically where parity required | ENG-04B comparable public fields; intentional SQL authority and separate preliminary results | Partial: bounded fixtures, no exhaustive legacy schema coverage |
| No conflicting authority mutation path | Existing SQL writes, JSON team validation, state/result lock boundary characterized | Partial: empty SQL roster legacy fallback ambiguity and arbitrary legacy JSON writers remain; no automatic migration |
| Read-only reconciliation before migration | ENG-04C aggregate AUTH-01–09 and negative controls; ENG-04F integration | Candidate; production adapter, permissions and scope not approved |
| Plans/locks and safe stale rejection | ENG-04D representative queries and controlled SQL lock proof | Partial: exact EF plans, full team workloads, real controller lock duration and identity contention investigation outstanding |
| SQL, XML, assets, protected settings, snapshot digests restored from single recovery point | ENG-04E quiescent SQL/selected assets/key/settings/snapshot recovery; existing XML/package exchange tests separate | Partial: combined XML exchange and full release/runtime recovery not demonstrated |
| Preserve historical certified evidence and Finals-only overall | Existing ranking regression, exact snapshot/hash preservation in B/C/E; original DB immutability trigger intact | Synthetic verified; historical browser/report acceptance still open |
| Rollback keeps legacy readers and versioned evidence | No removal, migration or application change in ENG-04 | Retain readers until reconciliation and migration approval |

Do not label the original ENG-04 exit fully satisfied merely because package CI passes. No automatic repair, schema migration, legacy reader removal, production access, deployment or master merge. A separate migration proposal must define provenance for genuinely empty versus legacy-only roster, reversible compatibility, dry-run evidence, conflict handling, owner decision and rollback.

## Residual risks and recommended ownership (roles proposed, not assigned)

- Storage/data owner: empty SQL legacy fallback, incomplete legacy aliases/qualification/identity/publication coverage, read-only operational reconciliation, exact data parity. Block legacy reader removal and migration.
- Engineering: prior identity transaction deadlock/timeout in pinned release CI; retry passed, root cause not resolved. Capture a deadlock graph and reproduce before changing isolation or locking.
- Infrastructure/DB owner: approve coordinated operational backup point, full/log chain as appropriate, encryption/access/retention/offsite policy, actual RPO/RTO and restore authority; run full-stack restore on isolated infrastructure with all assets, matching release, keys and API/browser smoke. Block production recovery certification.
- Release owner: unresolved ENG-00 FTP root/admin.html preflight, exact release artifacts and rollback set, protected IIS config and secure backup. Never overwrite production web.config in upload, synchronization or rollback; require identical pre/post SHA-256, stop on mismatch or inability to verify.
- Product/rules owner: ENG-03 browser, keyboard, accessibility, EN/MS/zh-Hans and print/Excel acceptance; historical Finals-only display review. Preserve preliminary reporting separately; Overall/All-Around from Finals only.

## Next work

ENG-05 may be prepared on the validated stacked candidate after explicit owner authorization and ENG-04F CI review; outstanding operational gates carry forward. Initial focus: provider queue and public reconnect characterization, pending edits and baseline revisions, duplicate/delayed/missing events, dropped response after commit, reconnect after deletion, failed refresh and close during edit. Preserve full GET fallback and operator drafts. No master merge or production release implied by progression.
