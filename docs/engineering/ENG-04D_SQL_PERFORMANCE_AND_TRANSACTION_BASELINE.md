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
| Competition row contention | Two SQL sessions, Serializable UPDLOCK/HOLDLOCK holder, UPDLOCK/ROWLOCK waiter; independent DMV observer | Observed LCK wait, holder elapsed and waiter elapsed, completion after rollback |
| Concurrent scoring | Existing two-client create/edit races, original one-success/one-conflict and no lost-update assertions | Pair elapsed ms |

One warm-up and five recorded samples per query and HTTP category; nearest-rank percentiles. STATISTICS IO messages are summed (including any separately reported LOB logical reads). Only physical operator names, counts and aggregate timings are logged; no query-plan XML, connection details, participant names or payloads. Output marker ENG04D_METRIC permits review from CI logs. The lock probe is a single deliberate contention observation per fixture, not a production latency distribution. SQL query timings include statistics/plan collection and reader drain overhead; HTTP timings include client and response processing where described. Representative SQL is not the exact EF-generated query plan. Measurements are diagnostic, no arbitrary speed thresholds. The test fails for missing plan/IO evidence, invalid response semantics, absent intended blocking or unexpected data count.

## Transaction assessment

- State Save obtains Competition then CompetitionState under Serializable. It validates membership chunks and team-result integrity before returning revision conflict. Moving stale checks earlier would change observable 400/409 precedence; do not do so. Current valid stale requests can perform expensive reads even though they cannot commit.
- Result batch locks Competition with UPDLOCK/ROWLOCK, checks lifecycle and dataset revision before team/member checks, reads candidates in chunks, commits once, and publishes after commit. Existing create/update/delete/revision race tests remain intact.
- Stacker changes share the Competition lock boundary. Do not shorten or reorder these locks without proving team/state/result integrity across concurrent operations.
- Identity creation uses Serializable and rechecks candidates using a full identity-table read before insert. Shared-read/range-lock conversion may contribute to contention; this is a source-level hypothesis, not a proven deadlock graph.
- Historical pinned-source offline CI attempt 1 of 45718592696 failed during CareerProfileReadinessTests; attempt 2 passed. Preserve the failure evidence and investigate separately; a successful retry does not eliminate concurrency risk. No changes to pinned source or retry policy. Existing identity readiness tests remain in the CI suites.

## Decision gates

This is a measurement-first increment, not a performance fix. Review CI metrics and repeated comparable runs before proposing a targeted optimization. Candidate changes include eliminating redundant team-reference work or reducing stale validation cost **only if observable error precedence and full regression coverage remain intact**. No index or lock-isolation changes yet. Actual EF plan capture, mixed team workloads, identity contention stress/deadlock graph capture, lock durations inside actual controller transactions, production cardinalities and coordinated recovery remain outstanding. The small samples and shared CI runner are not suitable for production SLA or throughput claims. If the measurements do not justify a change, retain the implementation and document the decision in ENG-04F; if necessary, use a separate ENG-04D follow-up before progressing.

Mandatory: Overall/All-Around = Finals only, certified snapshot bytes/digest immutable, no production IIS web.config overwrite (exclude upload/sync/rollback, secure backup and identical pre/post SHA-256). No production access, master merge or deployment. Hosted CI on this draft is pending.
