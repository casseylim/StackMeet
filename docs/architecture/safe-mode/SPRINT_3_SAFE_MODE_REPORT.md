# Sprint 3 Competition Safe Mode Report

## Status

**Complete — architecture and documentation only.** No production code, runtime reference, business rule, XML format, localStorage format, SQL schema, UI, or competition workflow was changed.

## Documents created

Under `docs/architecture/safe-mode/`:

1. `SAFE_MODE_ARCHITECTURE.md` — operating modes, components, diagrams, audit, backup, and failure containment.
2. `COMPETITION_PACKAGE.md` — package scope, preparation, version/freshness, integrity, size, expiry, and security.
3. `SYNC_PROTOCOL.md` — checkpoint/pull, atomic local writes, outbox, idempotency, retries, acknowledgements, tombstones, versions, partial failure, restart, and completion.
4. `CONFLICT_POLICY.md` — entity-specific merge/LWW/approval/audit rules with official-result protection.
5. `SYNC_DATA_SCHEMA.md` — proposed sync metadata mapped to current JavaScript state and future SQL tables without schema changes.
6. `PROVIDER_INTERFACES.md` — design contracts for Repository, API, IndexedDB, Sync Engine, connectivity, conflicts, and emergency backup.
7. `SAFE_MODE_UX.md` — status, banners, pending/failed/conflict/package/device/backup/manual controls without implementation.
8. `SAFE_MODE_TEST_PLAN.md` — required outage, restart, duplicate, partial failure, conflict, stale package, multi-device, finals, and recovery tests.
9. `ROADMAP_UPDATE.md` — progression from static prototype through repository, IndexedDB/PWA, ASP.NET Core/SQL Server, auth, sync, conflict resolution, and rollout.
10. `SPRINT_3_SAFE_MODE_REPORT.md` — this delivery summary.

Updated:

- `docs/architecture/CTO_DECISIONS.md` — appended `CTO-001` through `CTO-007`, all marked **Proposed** dated 2026-07-11.

The requested review reference `docs/ARCHITECTURE_REVIEW.md` is currently located at repository root as `ARCHITECTURE_REVIEW.md`; that existing file was reviewed and not moved.

## Recommended target architecture

```text
StackMeet UI
    -> CompetitionRepository
        -> OnlineApiProvider -> ASP.NET Core API -> Microsoft SQL Server (authority)
        -> IndexedDbProvider -> competition package + projections + outbox + audit
        -> SyncEngine -> pull/checkpoint/push/ack/resume
        -> ConflictResolver -> authorized review for protected conflicts
        -> EmergencyBackupService -> verified encrypted recovery staging
```

StackMeet remains online-first. A verified outage or authorized manual decision activates Competition Safe Mode using one valid competition-scoped package. Each local mutation atomically writes its local projection, immutable outbox operation, and audit record. Reconnection enters Syncing Mode; operation IDs, base versions, SQL rowversion, acknowledgements, tombstones, and server checkpoints prevent loss and duplication. SQL Server remains authoritative.

## Major risks

| Risk | Level | Required control |
|---|---|---|
| Silent overwrite of official results | Critical | Never LWW; preserve both values; head-judge/results-admin audited resolution. |
| Duplicate/lost offline writes | Critical | Atomic IndexedDB transaction, durable outbox, unique OperationId, server idempotency record. |
| Startup/package scope mixes competitions | Critical | Immutable CompetitionId on every package/entity/operation/checkpoint and strict partition validation. |
| Stale data during finals | Critical | Strict freshness/expiry gates, designated ownership, authorized emergency audit. |
| Multiple offline device conflicts | High | Initial station/data ownership, rowversion/BaseVersion, conflict review. |
| Partial sync/restart | High | Per-operation acknowledgement, durable continuation/checkpoint, leases, resumable state machine. |
| Deleted participant/team resurrection | High | Server tombstones and conflict on stale references. |
| Offline data exposure | High | Minimized role-scoped package, no password hashes, protected backup, expiry/purge, device control. |
| IndexedDB quota/corruption | High | Readiness/quota checks, atomic staging, integrity hashes, emergency backups and recovery drills. |
| API/auth outage misclassification | Medium–High | Verified API health with distinct network, server, auth, version, and competition-state statuses. |

## Decisions requiring Product Owner approval

1. `CTO-001`: online-first with Competition Safe Mode fallback.
2. `CTO-002`: Microsoft SQL Server as authoritative central database.
3. `CTO-003`: IndexedDB as intended browser offline store.
4. `CTO-004`: official result conflicts never silently resolved.
5. `CTO-005`: durable outbox with unique operation IDs.
6. `CTO-006`: one competition-scoped downloaded package.
7. `CTO-007`: designated device/data ownership for initial offline operation.

Additional policies to approve later: package freshness/expiry by stage, offline authorization lifetime, device assignment, finals second-person conflict approval, backup encryption/retention, tombstone/idempotency retention, and Safe Mode pilot limits.

## Impact on the previous migration plan

`MIGRATION_PLAN.md` remains valid for first isolating current localStorage/XML behind Repository with exact parity. Sprint 3 extends the future direction after that boundary exists:

1. Finish characterization and Repository parity first.
2. Migrate current calls incrementally without changing formats.
3. Add stable internal IDs/version/audit design through separate approved migrations.
4. Add IndexedDbProvider and competition package in tests/shadow mode.
5. Build ASP.NET Core/SQL Server authority and idempotent sync endpoints.
6. Pilot Safe Mode progressively; do not replace current localStorage directly with untested synchronization.

The Sprint 2 `Repository.js` and `LocalStorageProvider.js` remain unchanged and disconnected. Safe Mode interfaces are design documents only.

## Validation evidence

- `node --check app.js`: **PASS**, exit code 0.
- Existing storage smoke tests: **PASS**, exit code 0.
- Baseline hashes unchanged for `app.js`, `index.html`, `styles.css`, bundled data, `Repository.js`, and `LocalStorageProvider.js`.
- `index.html` references no Safe Mode file/component.
- Production code modified: **none**.
- Existing storage calls redirected: **none**.
- XML/localStorage/SQL schema modified: **none**.

## Sprint 4 readiness

**Ready for Sprint 4 planning after proposed CTO decisions are reviewed.** Recommended Sprint 4 scope is executable characterization/test design and small proof-of-concept test doubles for package/outbox/idempotency behavior outside production runtime. Do not connect Safe Mode to `app.js` or alter production persistence until Repository parity and decision gates are complete.
