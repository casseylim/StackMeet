# ENG-04D — SQL performance and transaction baseline candidate (draft)

Base: ENG-04C 6ebc73d3ae6c6dca9190ca2332d5bb05dd9b8021; [CI 45718592696](https://github.com/casseylim/StackMeet/actions/runs/45718592696), attempt 2 passed all jobs. This package instruments the disposable test harness only. No measured results are claimed until the new CI execution is reviewed.

## Measurements

The existing CoreIntegrity harness starts a loopback API on a generated LocalDB database and drops the database after completion. Eng04DPerformanceAssertions independently verifies the generated name, local server, integrated authentication and absence of AttachDBFilename. Two matched fixtures (32 and 512 SQL stackers and Finals individual results, one result per participant, no scored teams) are seeded. No production connection, schema, migration or release changes.

| Measurement | Protocol | Evidence |
|---|---|---|
| Roster by competition | Parameterized representative SQL | Actual STATISTICS XML physical operators, STATISTICS IO logical reads and elapsed time |
| Results by competition | Same fixture sizes and projected columns | Same |
| Team-result reference search | Competition and team type predicates; no team rows in these fixtures | Same |
| Member lookup | Competition and one participant code | Same |
| Public results HTTP GET | Actual loopback API, assert SQL roster/result counts | p50/p95 elapsed ms |
| Valid stale state POST | All fixture codes referenced; 512 exercises 500-code chunk boundary | p50/p95 and 409/current ETag |
| Invalid stale state POST | Missing participant; malformed JSON | 400 precedence and unchanged saved bytes/revision |
| Competition row contention | Two SQL sessions, Serializable UPDLOCK/HOLDLOCK holder, UPDLOCK/ROWLOCK waiter; bounded SQL lock timeout probe followed by a second waiter | SQL error 1222, holder/timeout/waiter elapsed, successful completion after rollback |
| Concurrent scoring | Existing two-client create/edit races, original one-success/one-conflict and no lost-update assertions | Pair elapsed ms |

One warm-up and five recorded samples per query and HTTP category; nearest-rank percentiles. STATISTICS IO messages are summed (including any separately reported LOB logical reads). Only physical operator names, counts and aggregate timings are logged; no query-plan XML, connection details, participant names or payloads. Output marker ENG04D_METRIC permits review from CI logs. The lock probe is a single deliberate contention observation per fixture, not a production latency distribution. SQL query timings include statistics/plan collection and reader drain overhead; HTTP timings include client and response processing where described. Representative SQL is not the exact EF-generated query plan. Measurements are diagnostic, no arbitrary speed thresholds. The test fails for missing plan/IO evidence, invalid response semantics, missing SQL lock-timeout or release evidence or unexpected data count.

## Transaction assessment

- State Save obtains Competition then CompetitionState under Serializable. It validates membership chunks and team-result integrity before returning revision conflict. Moving stale checks earlier would change observable 400/409 precedence; do not do so. Current valid stale requests can perform expensive reads even though they cannot commit.
- Result batch locks Competition with UPDLOCK/ROWLOCK, checks lifecycle and dataset revision before team/member checks, reads candidates in chunks, commits once, and publishes after commit. Existing create/update/delete/revision race tests remain intact.
- Stacker changes share the Competition lock boundary. Do not shorten or reorder these locks without proving team/state/result integrity across concurrent operations.
- Identity creation uses Serializable and rechecks candidates using a full identity-table read before insert. Shared-read/range-lock conversion may contribute to contention; this is a source-level hypothesis, not a proven deadlock graph.
- Historical pinned-source offline CI attempt 1 of 45718592696 failed during CareerProfileReadinessTests; attempt 2 passed. Preserve the failure evidence and investigate separately; a successful retry does not eliminate concurrency risk. No changes to pinned source or retry policy. Existing identity readiness tests remain in the CI suites.

## Decision gates

This is a measurement-first increment, not a performance fix. Review CI metrics and repeated comparable runs before proposing a targeted optimization. Candidate changes include eliminating redundant team-reference work or reducing stale validation cost **only if observable error precedence and full regression coverage remain intact**. No index or lock-isolation changes yet. Actual EF plan capture, mixed team workloads, identity contention stress/deadlock graph capture, lock durations inside actual controller transactions, production cardinalities and coordinated recovery remain outstanding. The small samples and shared CI runner are not suitable for production SLA or throughput claims. If the measurements do not justify a change, retain the implementation and document the decision in ENG-04F; if necessary, use a separate ENG-04D follow-up before progressing.

Mandatory: Overall/All-Around = Finals only, certified snapshot bytes/digest immutable, no production IIS web.config overwrite (exclude upload/sync/rollback, secure backup and identical pre/post SHA-256). No production access, master merge or deployment. Hosted CI on this draft is pending.

## First CI result and correction

Run [45730912779](https://github.com/casseylim/StackMeet/actions/runs/45730912779) passed offline reproducibility but failed Build and test and Regression evidence/API coverage. The build log shows all four representative queries for the 32-participant fixture used Index Seek and two logical reads each. Roster p50/p95 1.235/2.462 ms; results 1.025/1.125 ms; team references 1.240/1.363 ms; member lookup 0.922/0.990 ms. Public HTTP p50/p95 7.560/8.064 ms; stale-state HTTP 6.718/7.094 ms. Scoring create pair 319.099 ms, edit pair 29.915 ms. These are partial diagnostic data, not an accepted benchmark. The test stopped at the first controlled contention assertion; no 512-fixture measurements were collected in that log.

The original compound assertion failed to establish both the DMV-observed blocking relationship and successful waiter completion. Exact DMV visibility/sampling cause was not established. Replace the sampled DMV gate with engine-level error 1222 from a test-only 750 ms LOCK_TIMEOUT while the holder owns the update lock; require a second waiter to remain pending while the holder is active and complete successfully after rollback. Record timeout-probe, holder and waiter durations separately. This retains a positive blocking proof and a release proof, without changing production isolation, indexes, command timeouts, or CI requirements. Corrected hosted validation pending.
