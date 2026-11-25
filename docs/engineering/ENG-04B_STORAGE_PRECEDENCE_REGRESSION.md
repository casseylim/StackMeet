# ENG-04B — Storage precedence and regression characterization (draft)

Base: ENG-04A 7c555e764147c26fcbf6f789d175849e81a314e9, CI run [45623464636](https://github.com/casseylim/StackMeet/actions/runs/45623464636), all three jobs passed. This package adds tests only to the existing CoreIntegrity disposable LocalDB HTTP harness. Hosted CI for this branch is pending; no claim that new assertions have executed yet.

| Fixture | Expected existing contract |
|---|---|
| Missing saved state | Public results 404 even when SQL competition exists. |
| Legacy-only roster and results | Public JSON participant fallback is available; legacy results do not populate public results; private contact fields omitted. |
| Equivalent legacy and SQL roster | Eight public participant fields match for the explicit custom-division fixture. This is not universal parity across every age/locale/legacy alias. |
| Mixed SQL/JSON | Nonempty SQL roster replaces the entire JSON roster; no union; SQL attempts override stale JSON result copies. SQL competition metadata and JSON display settings remain distinct. |
| SQL-only modern state | Empty JSON roster/result arrays do not hide SQL roster and results. |
| Qualification | Public read leaves saved JSON and ETag unchanged, including the synthetic approved qualification record; raw qualification payload is not returned by the public API. Not an exhaustive qualification lifecycle test. |
| Historical evidence | Legacy-v1 source capture in a closed synthetic competition: FinalsResults contains SQL Finals only, exact UTF-8 SHA-256 matches, public GET does not change snapshot bytes/hash, recapture rejected. Legacy copies in the embedded competition-state evidence remain untouched. |
| Last SQL participant removed | Deliberately seeded legacy-only fallback becomes visible again when SQL roster becomes empty; legacy result copies still do not return. Both arrays empty yields empty roster. This is a known-risk characterization, not endorsement or remediation. |

Existing CoreIntegrity assertions remain in the same CI job: missing/cross-competition participant checks, legacy team aliases, parent/partner exceptions, malformed state/deletion protection, duplicate/incomplete teams, team membership immutability when referenced by results, stale revisions and rejected state writes. ENG-03G remains responsible for operator Finals-only overall ranking composition, read-only reporting and separate Prelims reporting. Existing governance and qualification regression suites remain unchanged. The new historical source assertion does not calculate Overall standings or recertify historical production results.

## Reconciliation implications for ENG-04C

The empty SQL roster is not sufficient evidence of a never-migrated competition. A previous SQL roster may have been intentionally emptied while stale JSON remains. Public and operator reads can diverge in that case. Flag AUTH-01 (legacy-only or stale-copy ambiguity) for reviewed provenance; never automatically merge, restore or remove data. AUTH-02/03 mixed copies must be reported using logical identity and safe aggregate discrepancy counts. SQL-only results are normal. A missing saved state is a separate condition from an empty roster. A synthetic known-risk assertion must be explicitly reconsidered if an owner-approved future correction changes the contract.

The fixture seeds intentionally inconsistent rows directly into its generated test database to represent preexisting storage, not as an example of an authorized application write. The existing harness guards the LocalDB server and generated database name, launches a loopback API and drops the isolated database after tests. No production access, migration, application behavior changes, historical data edits, release workflow changes or web.config edits. No new CI job: existing CoreIntegrity entry point invokes the new assertions. No master merge or deployment. Full browser acceptance, actual production reconciliation, measured query/lock performance and coordinated recovery remain open.

Mandatory: Overall/All-Around = Finals only; preserve original historical snapshot bytes and SHA-256; exclude production IIS web.config from upload/sync/rollback, securely back up and verify identical pre/post SHA-256.
