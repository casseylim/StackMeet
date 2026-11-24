# Synthetic performance and concurrency baseline

Windows, SQL Server LocalDB, Release net8.0, SDK 10.0.400, local Kestrel HTTP, one host for server and clients. API and test projects came from baseline a3042ef. Each size had its own active competition and minimal saved JSON, N SQL participants and N individual Finals Cycle results with three attempts each. Three simultaneous read waves per size followed one warmup. Each client consumed the complete HTTP response and checked competition key and both collection sizes. Percentiles use nearest rank across requests. Other audit work shared the host. These are short controlled scenarios without a production SLO.

| Participants / concurrent clients | GETs | Unexpected errors or count mismatches | GET p50 / p95 ms | HTTP body bytes | API working set MiB after waves | Single browser navigation ms |
|---|---:|---:|---:|---:|---:|---:|
| 100 / 3 | 9 | 0 | 36.6 / 145.5 | 28,325 | 127 | 4,001 |
| 300 / 5 | 15 | 0 | 126.5 / 172.0 | 84,925 | 150 | 397 |
| 500 / 10 | 30 | 0 | 128.7 / 257.4 | 141,523 | 163 | 341 |
| 1,000 / 20 | 60 | 0 | 390.7 / 601.6 | 283,028 | 291 | 332 |
| 3,000 / 50 | 150 | 0 | 1,255.3 / 7,423.3 | 855,028 | 330 | 500 |
| 5,000 / 100 | 300 | 0 | 2,529.7 / 4,379.7 | 1,427,028 | 722 | 1,064 |

All 564 measured reads returned the expected counts. An independent serial trace against the restored fixture logged exactly five EF commands per GET at each size. Controllers/PublicResultsController.cs:19, :34, :45, :57 and :89 load competition metadata, assets, saved state, stackers and results respectively. The count is flat while transferred rows and bytes grow. The 3,000-row tail exceeded the 5,000-row tail in this run. These samples do not establish a monotonic latency curve. Working set includes retained heap and runtime pages across successive sizes.

The browser column measures a separate desktop Edge navigation through the real portal to the synthetic competition heading and the next animation-frame callback. One observation per size, smallest first with cold startup. It includes network and application work. It measures neither paint in isolation nor a browser percentile. On the 100-participant page, closing the live WebSocket led to another public-results GET and a live connection indicator. Source at wwwroot/results/results.js:117 and :1722 implements polling and rejoin. Multi-minute outages, token expiry and fleet-wide reconnect bursts remain unmeasured.

## Contested edits

Each burst used a freshly restored synthetic database. All clients fetched or shared the same initial revision, submitted one state replacement each, and identified their submission by a unique marker. A final GET checked the winning marker and expected participant count. The maintenance path isolated the persistence operation from login. This measures contention on a single aggregate and deliberately maximizes stale submissions. Division and team graph validation costs will vary with the workload.

| Participants / writers | POST bytes for first writer | Committed / expected HTTP 409 | First p50 / p95 ms | Repeat p50 / p95 ms |
|---|---:|---:|---:|---:|
| 100 / 3 | 5,407 | 1 / 2 | 1,171 / 1,183 | 347 / 350 |
| 300 / 5 | 16,607 | 1 / 4 | 153 / 179 | 35 / 44 |
| 500 / 10 | 27,807 | 1 / 9 | 154 / 223 | 49 / 75 |
| 1,000 / 20 | 55,807 | 1 / 19 | 562 / 823 | 192 / 330 |
| 3,000 / 50 | 171,807 | 1 / 49 | 6,358 / 9,933 | 1,994 / 3,548 |
| 5,000 / 100 | 287,807 | 1 / 99 | 11,371 / 24,646 | 6,887 / 14,020 |

Across both runs, all 376 requests returned the expected terminal status and every stored winner matched. On the repeat, database-scoped sys.dm_db_index_operational_stats grew from zero to 181 row-lock waits totaling 740,884 cumulative waiter-ms. Page-lock waits stayed at zero. Cumulative time sums across waiters. These counters cannot identify individual blocking statements. Controllers/CompetitionStateController.cs:92 holds the competition and state locks across validation. Inspect per-statement wait times and the cost of validating an already stale request before changing ordering.

**Acceptance for engineering changes.** Repeat all six shapes on the same runtime, fixture, seed and host class. Keep response identity, every winning marker and exactly one commit per contested revision. Report p50/p95, unexpected HTTP statuses, bytes, commands, waits and working set separately. Obtain owner targets before assessing pass/fail on latency. Add a sustained mixed workload with independent result edits, identity reads and reconnects before making production capacity commitments.

The [measurement ledger](evidence/runtime-measurements.json) retains the per-shape results and browser observations.
