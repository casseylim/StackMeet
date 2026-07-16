# Synchronization Protocol

## Principles

- Server SQL rowversion/checkpoint is authoritative.
- Every client mutation is an idempotent operation, not an unversioned whole-state upload.
- Pull before push after reconnect, except a server may accept independent commands safely.
- Protected result conflicts pause only affected operations, never disappear.
- Sync state survives browser/device restart.

## Download and checkpoint

1. Client sends CompetitionId, PackageVersion, client/API versions, DeviceId, last server checkpoint, and entity capabilities.
2. Server returns ordered changes/tombstones after the checkpoint, paged/chunked with a response checkpoint.
3. Client validates scope, versions, hashes, references, and ordering.
4. One IndexedDB transaction applies a page and records its continuation token; final checkpoint advances only after all pages commit.
5. If checkpoint history is unavailable/incompatible, server requires a full package refresh. Pending outbox is preserved and rebased/reviewed.

A checkpoint is opaque and monotonically ordered by the server; client clocks never determine change order.

## Local write

One IndexedDB transaction must:

1. Validate active competition package and authorization capability.
2. Read entity and BaseVersion.
3. Apply local projection for immediate operation.
4. Append immutable outbox operation.
5. Append local audit entry.
6. Commit all or none.

Only after commit may UI show “saved locally.”

## Outbox operation

```json
{
  "operationId": "UUID/ULID",
  "competitionId": 1,
  "deviceId": "device-uuid",
  "userId": 42,
  "entityType": "resultAttempt",
  "entityId": "stable-id",
  "action": "create|update|delete",
  "baseVersion": "rowversion/base64-or-null",
  "payload": {},
  "dependencies": ["operation-id"],
  "createdAtClient": "ISO-8601",
  "sequence": 123,
  "status": "pending",
  "attemptCount": 0,
  "lastSyncAttempt": null,
  "lastError": null
}
```

Payload is a command/patch with required fields, not an arbitrary whole database snapshot. Sensitive fields follow role policy.

## IDs and idempotency

- OperationId is globally unique and generated once before local commit.
- Server stores an idempotency record scoped by CompetitionId + OperationId, request hash, outcome, entity/version, and receipt time.
- Retrying identical operation returns the original acknowledgement.
- Reuse with different payload is rejected as integrity error.
- Client-generated entity IDs are stable UUIDs or server-reserved IDs; current public codes remain display identifiers.

## Upload ordering

Order by local sequence and dependencies: parent entity before membership/result; result before attempts; update before delete. Independent entities may batch. Protected result operations should preserve data-entry sequence. A blocked dependency blocks dependants, not unrelated operations.

## Server acknowledgement

Each operation returns one of:

- `Accepted`: OperationId, entity ID, new version, server time, audit ID, checkpoint/change token.
- `AlreadyApplied`: same original outcome.
- `Conflict`: current server entity/version, conflict code, protected fields, review requirement.
- `Rejected`: validation/auth/competition-state reason; no mutation.
- `Deferred`: retryable dependency/temporary condition.

Client records acknowledgement before removing/archiving the outbox item. Keep acknowledged metadata for audit/idempotency retention.

## Retry rules

- Retry network timeout, 408, 429, and transient 5xx with exponential backoff + jitter: e.g. 2s, 5s, 15s, 30s, 60s, then max 5 minutes.
- Honor `Retry-After`.
- Do not auto-retry 400 validation, 401/403 auth, 404 scope/entity, 409 conflict, incompatible version, or closed competition until state/user action changes.
- Limit concurrent requests and prevent two Sync Engines for the same device/package via IndexedDB lease/leader election.
- Manual retry respects the same idempotency key.

## Incremental synchronization

Loop: acquire lease -> health/auth -> pull changes -> apply/conflict classify -> upload eligible outbox batches -> record acks -> pull acknowledgement-generated changes -> repeat. Use bounded pages and progress counters. Checkpoint advances only through fully committed server pages.

## Tombstones

Server emits tombstones with EntityType, Id, CompetitionId, DeletedAt/By, Version, reason, and retention expiry. IndexedDB keeps tombstones long enough to prevent deleted records reappearing from stale devices. A local update against a tombstone becomes a conflict; protected historical results are never cascade-erased silently.

## Version handling

- SQL `rowversion` is the optimistic concurrency token, returned opaque/base64.
- BaseVersion is required for updates/deletes and null for creates.
- Server compares BaseVersion inside the same SQL transaction as mutation/audit/idempotency record.
- Package/schema/API incompatibility blocks writes and requires upgrade/refresh.
- Client timestamps are audit context only, never conflict authority.

## Partial failures

Batch response has per-operation outcome. Accepted operations remain accepted; rejected/conflicted ones remain visible. Transaction groups that must be atomic (team + members, result + attempts where commanded together) are one server command or all rejected. Do not roll back unrelated successful operations locally.

## Restart/resume

IndexedDB stores package, entity projections, outbox, acks, sync lease expiry, page continuation, checkpoint, conflict records, and audit. On startup, recover expired lease, verify last transaction/checkpoint, resume pending/deferred operations, and never regenerate OperationIds.

## Completion criteria

Sync is complete only when:

- all server changes through final checkpoint are committed locally;
- outbox has no pending/uploading/deferred eligible operations;
- all acknowledgements are durable;
- no unresolved blocking conflicts exist (nonblocking review items are explicitly reported);
- local checkpoint equals server-reported competition checkpoint;
- integrity/reference checks pass; and
- completion audit is recorded with counts and time.

