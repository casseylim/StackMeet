# ENG-04C — Read-only storage reconciliation candidate (draft)

Base: ENG-04B 76b04756fab89f5ca3fcf3dc170b03cad1fac6dc, [CI run 45625411565](https://github.com/casseylim/StackMeet/actions/runs/45625411565), all three jobs succeeded. New hosted checks pending.

## Implementation

`tools/StorageReconciliation/StorageReconciliation.cs` is a reusable development candidate linked into CoreIntegrityIntegrationTests, not the deployed API. The entry point InspectAsync accepts a generated LocalDB connection, one competition ID, optional isolated asset fixture root and cancellation token. It refuses non-LocalDB, non-integrated security, attached files and non-CoreIntegrity database names before connecting. A fresh EF context, no-tracking reads and a serializable transaction with rollback give stable SQL evidence in the disposable fixture. There is no mutation, repair mode, API route, scheduled job, production connection or production CLI. Asset reads are restricted to a named temporary fixture root, one competition directory and non-reparse file paths; no asset writes or deletes in the inspector. SQL and file evidence is not a coordinated production recovery point.

The report contains UTC capture timestamp, observed state/result revisions, aggregate outcome (Pass, Review, Inconclusive), and sorted reason codes, severity and counts. No participant names/codes, contacts, raw state/results, snapshot content, asset paths, competition identifier, SQL connection strings or exceptions are serialized. Pass means only that no findings were detected by the implemented checks; not a complete integrity certification. Counts are per reason (and may overlap), not counts of unique affected records.

| Family | Candidate checks |
|---|---|
| AUTH-01 | Empty SQL roster with nonempty legacy roster; ambiguous migration provenance. |
| AUTH-02 | Duplicate/missing participant codes, legacy-only and SQL-only codes when both copies coexist, directly comparable public-field disagreements. |
| AUTH-03 | Logical result keys, duplicate/invalid rows, conflicting attempts/penalty, legacy-only or matching legacy copies, SQL row revision bounds. SQL-only modern rows are normal. |
| AUTH-04 | Missing individual result participants, invalid team graph using existing readiness parser, string team references and scored team membership against SQL. Parent display names are not participant identities. |
| AUTH-05 | Qualification metadata and selected participant/team references, without recalculation. |
| AUTH-06 | Existing certified snapshot hash over exact UTF-8 bytes, supported schema/rule, competition identity and capture revision metadata, malformed or partial evidence. No snapshot absent-by-design finding. Older valid capture revisions are not rejected merely because live revisions increased. |
| AUTH-07 | Referenced files missing, size/hash mismatch and orphan direct files; unavailable or unsafe fixture path is inconclusive. |
| AUTH-08 | Competition name/date disagreement with JSON settings, not automatic overwrite. |
| AUTH-09 | Missing/ambiguous state, unknown schema, malformed JSON or invalid collection shapes are inconclusive. |

## Synthetic acceptance

Existing CoreIntegrity HTTP fixture is extended to inspect mixed SQL/JSON, clean empty state, legacy-only ambiguity, missing team and qualification references, malformed/unknown state, assets missing/matching/corrupt/orphan, and valid historical digest and rejected SQL mutation in a separate disposable fixture, plus deliberately corrupted in-memory evidence passed through the same digest evaluator. Assert aggregate-only output, production connection refusal, and exact unchanged original state/revision/snapshot/hash. Asset test setup intentionally writes synthetic bytes and cleans its own temporary directory; inspector only reads. The attempted SQL digest update is expected to fail with trigger error 51041; the stored evidence must remain unchanged. The actual corrupt digest is injected only into an in-memory copy. Existing ENG-04B and ENG-03G regression suites remain intact.

## Limitations and next steps

No real production discrepancies have been measured. There is no operational report runner or deployment approval. A production adapter would require a separately approved read-only principal, coordinated backup/restore evidence, bounded query and file I/O, redacted error handling and independent review. Serializable fixture reads can hold locks while inspecting assets; do not use this approach against live competition traffic. No performance or recovery claim. Numeric pending-team aliases, full legacy/schema variants, derived division parity, detailed qualification source-result/rule verification, identity/publication reconciliation, duplicate JSON property handling and full historical placement semantics remain follow-up scope. Asset orphan detection requires supplied fixture root; absent root with no metadata cannot prove absence of orphan bytes. Reconciliation neither repairs legacy fallback nor approves removal. Full ENG-03 browser/runtime acceptance and ENG-04D/E measurement and recovery gates remain open.

Mandatory: Overall/All-Around = Finals only, immutable historical snapshot bytes, production web.config excluded from upload/sync/rollback with secure backup and identical pre/post SHA-256. No application behavior changes, schema migration, production database/file changes, master merge or deployment.

## CI correction

Initial run [45711070164](https://github.com/casseylim/StackMeet/actions/runs/45711070164) failed after 12 new reconciliation assertions passed: the test attempted to update an already captured synthetic snapshot, and SQL trigger TR_FinalsRankingGovernance_ImmutableSnapshot correctly rejected it (51041). Regression evidence coverage also exited nonzero. Offline reproducibility passed. Correction: assert the existing SQL rejection and unchanged persisted evidence, and use a pure shared digest evaluator to test corrupted in-memory evidence. No trigger, migration, production data or CI gate modified. Corrected hosted CI is pending.
