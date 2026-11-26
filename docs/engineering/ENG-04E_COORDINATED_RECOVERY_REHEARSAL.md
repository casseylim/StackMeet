# ENG-04E — Coordinated recovery rehearsal candidate

Base: ENG-04D 3331140af769d9422f81345ed94169d2f75a8d23. CI [45731926128](https://github.com/casseylim/StackMeet/actions/runs/45731926128) passed all three jobs. ENG-04E hosted execution pending.

## ENG-04D evidence and decision

Build job 139615821741 captured five recorded samples per category, after warm-up.

| Metric | 32 fixture | 512 fixture |
|---|---:|---:|
| Public results HTTP median / P95 (ms) | 6.502 / 7.053 | 27.591 / 41.363 |
| Stale state HTTP median / P95 (ms) | 5.918 / 6.878 | 13.872 / 14.466 |
| Roster logical reads | 2 | 5 |
| Results logical reads | 2 | 9 |
| Team reference logical reads | 2 | 9 |
| Individual lookup logical reads | 2 | 2 |
| Controlled lock timeout probe (ms) | 752.852 | 752.963 |
| Waiter completed after release (ms, includes deliberate hold) | 152.974 | 151.524 |

All four representative query plans used Index Seek. Both contention cases produced SQL 1222 and a pending waiter completed after holder rollback. Concurrent scoring create/edit pairs took 341.409/28.832 ms. These are limited LocalDB measurements, not production SLAs. No immediate index, validation or isolation change justified. Actual EF plans, representative mixed workloads and identity concurrency/deadlock investigation remain open. Prior failed pinned-source CI is retained as risk evidence.

## Rehearsal protocol

The existing CoreIntegrity harness shuts down its loopback API before ENG-04E. The new test seeds one synthetic recovery competition in the existing generated LocalDB database. No real data, credentials, IIS configuration or network endpoints are used.

1. Verify exact LocalDB server, generated source naming, integrated authentication and distinct non-existing generated restore target. Create a unique temporary root.
2. Seed roster, separate Finals/Prelims rows, approved qualifier JSON, revisions, identity/link and privacy flag, asset metadata, authenticated encrypted setting and immutable Finals evidence. Generate disposable encryption/signing keys.
3. With fixture writers quiescent, copy selected competition asset, synthetic key/config files and a checked-out app.js to the temporary backup set. Capture SHA-256 manifest. Backup whole generated SQL database with COPY_ONLY and CHECKSUM; RESTORE VERIFYONLY with CHECKSUM; hash backup bytes.
4. Restore under the new database name with explicit MOVE for data/log files, CHECKSUM and RECOVERY. No REPLACE. CHECKDB must not report errors. Restore selected files to a separate directory and validate their hashes.
5. Verify whole-database aggregate counts and selected competition state/revisions, SQL attempts, identity link and privacy, asset bytes vs SQL metadata, exact snapshot record/UTF-8 digest and Finals-only capture. Recover current-format protected ciphertext using restored synthetic encryption key; validate a synthetic session using the signing key. Verification must not rewrite ciphertext.
6. Simulate an allowlisted frontend copy where a conflicting web.config exists in the payload. Copy app.js only; assert protected live fixture config SHA-256 unchanged.
7. Drop only the distinct generated restore target and remove temporary rehearsal files. The parent harness owns cleanup of the original generated source.

Timing markers ENG04E_METRIC report backup/verification and restore/verification durations, backup byte size and aggregate counts; no key, credential, private data or snapshot payload output. Actual success and timings await CI logs.

## Boundaries and outstanding gates

- This is a quiescent synthetic recovery drill, not a production backup or a certified operational RTO/RPO. No full/log chain, point-in-time recovery, retention, offsite restore, production key escrow or restore authorization has been exercised.
- The whole generated database is backed up; asset/file consistency checks cover the selected recovery competition, not all earlier intentionally inconsistent fixtures. Only one release frontend file is copied. No actual IIS/FTP operation, full release inventory, real configuration, rollback script integration, or restored API host smoke test is performed. Existing release harness and XML/package exchange tests remain separate evidence, not a substitute for a full-stack restore.
- Synthetic keys are held only in disposable local fixture files; this is not a production secret backup design. Production configuration and keys need approved encrypted storage/access controls.
- The ENG-00 release preflight root/admin.html issue remains unresolved. Operational recovery point, RPO/RTO, retention, authority, production asset manifest and full restore acceptance remain owner gates.

Mandatory: Overall/All-Around = Finals only; preserve certified snapshot bytes and digest; production IIS web.config explicitly excluded from upload/sync/rollback, securely backed up and verified identical before/after. No application behavior, migrations, deployment workflow, production data, master merge or deployment changes.
