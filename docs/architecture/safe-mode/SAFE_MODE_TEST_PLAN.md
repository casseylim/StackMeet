# Competition Safe Mode Test Plan

## Test levels

- Unit: versions, outbox/idempotency, retry, conflict classification, package validation.
- Provider contract: API/IndexedDB fakes and fault injection.
- Integration: browser + ASP.NET test API + SQL Server test database.
- End-to-end: multiple real browser profiles/devices, restart, outage proxy, competition-scale data.
- Recovery drills: encrypted backup verification/replay review.

Every scenario checks entity truth, outbox, acknowledgements, checkpoint, audit, UI status, restart behavior, and absence of silent loss/overwrite.

## Required scenarios

| Scenario | Procedure | Expected result |
|---|---|---|
| Internet loss during result entry | Drop network before save/response at multiple timing points | One durable local entity + outbox op; UI says saved locally; no duplicate after sync |
| Server outage while Wi-Fi connected | API returns timeout/503 while navigator reports online | Monitor classifies API unavailable, enters Safe Mode only with valid package; auth is not misclassified |
| Browser refresh offline | Queue operations, refresh/close tab offline | Package/projections/outbox/device/checkpoint restored; same OperationIds; work continues |
| Device restart with queued changes | Fully close/restart browser/device | Expired sync lease recovered, pending count intact, sync resumes idempotently |
| Duplicate submission retry | Lose acknowledgement after server commit, retry | Server returns original AlreadyApplied/ack; one database mutation/audit result |
| Partial sync failure | Mixed accepted/conflict/rejected/transient batch | Accepted remain acked; others visible; unrelated operations continue; dependencies block correctly |
| Conflicting result entry | Two devices edit same attempt/base version | No LWW; protected conflict, both values preserved, head-judge resolution audited |
| Deleted participant/team | Server tombstone while offline device edits/references entity | Conflict; record not resurrected; affected results retained/reviewed |
| Stale package | Exceed freshness/expiry and attempt prelim/final write | Warning/restriction per policy; finals require authorization; audit emergency decision |
| Multiple Safe Mode devices | Overlapping and designated assignments | Assigned writes sync; overlapping protected writes conflict; no cross-device loss |
| Internet restoration during finals | Restore mid-entry with pending finals attempts | Sync in background without blocking entry; protected conflicts surfaced; no premature Online/Synced |
| Emergency backup recovery | Export, corrupt one copy, restore valid copy to staging | Corrupt rejected; valid verified; operations replay idempotently through review |

## Additional critical cases

1. API health works but authentication expired: prompt re-auth, preserve outbox, do not claim internet loss.
2. Competition locked/closed while offline: server rejects protected new writes; review remains visible.
3. IndexedDB quota exceeded during local save: transaction aborts; UI must not say saved.
4. Crash between entity and outbox write: atomic transaction yields both or neither.
5. Crash after server commit before local ack: retry returns original acknowledgement.
6. Pull page applied then crash before final checkpoint: continuation resumes without skipped changes.
7. Package download interrupted/corrupt/wrong competition/signature: staging discarded, prior package remains active.
8. Schema/app/package incompatible: writes blocked; clear upgrade/refresh instruction.
9. Local clock incorrect/timezone changes: ordering/version unaffected; audit records client and server times.
10. Outbox dependency chain: parent accepted before child; rejected parent blocks child visibly.
11. Scratch vs numeric attempt, penalty conflict, and finals third-attempt tie: always protected review.
12. Check-in monotonic merge and reversal: forward merge allowed only by policy; reversal audited/approved.
13. Translation same-key conflict: low-risk review/approved merge; never impacts results.
14. Backup contains another competition/device: restore rejected or isolated, never merged automatically.
15. Two tabs on one device: one sync leader/lease; no duplicate uploads.

## Scale and endurance

- Package sizes: 100, 500, 2,000 stackers with realistic teams/results.
- 8-hour competition with intermittent 1–20 minute outages.
- 10,000 pending operations and mixed dependencies; verify memory, UI responsiveness, and resumable batching.
- API rate limiting and slow 3G/high latency.
- Storage near quota and compaction after acknowledgements/tombstone retention.

## Security tests

- Cross-competition ID injection, expired/offline role, revoked device, tampered package/backup, changed idempotency payload, XSS content in queued fields, unauthorized conflict resolution, sensitive field minimization, and post-event purge.

## Exit criteria for pilot

- Zero silent lost/duplicated official operations across required scenarios.
- All protected conflicts require authorized audited resolution.
- Recovery after restart/outage completes with matching server/client checkpoint.
- Package integrity, scope, expiry, and quota checks pass.
- Operational drill completed by tournament staff with documented rollback/support runbook.

