# System Roadmap

Estimates are indicative for one small team and require refinement. No phase authorizes the next without its exit gate.

| Phase | Outcome | Complexity | Risk | Indicative effort |
|---:|---|---|---|---|
| 1. Current Prototype | Preserve current static app; fixtures and baselines | High existing complexity | High rule regression | Current + 1–2 sprints safety work |
| 2. Repository Migration | Repository parity; incremental direct-call cutover | High | Critical storage/XML drift | 3–6 sprints |
| 3. IndexedDB | Transactional package/entity/outbox stores and test doubles | Very High | Critical data loss/quota/upgrade | 4–7 sprints |
| 4. Competition Package | Signed scoped package, checkpoint/freshness/readiness | Very High | Critical stale/scope/security | 3–6 sprints |
| 5. Safe Mode | Controlled local writes, status, backup, designated ownership | Very High | Critical official continuity | 5–9 sprints |
| 6. ASP.NET Core API | Authenticated commands/queries/packages/sync endpoints | Very High | Critical auth/rule divergence | 6–12 sprints |
| 7. Microsoft SQL Server | Production schema, rowversion, audit/change feed, migrations/HA | Very High | Critical integrity/migration | 5–10 sprints |
| 8. Sync Engine | Pull/outbox/idempotency/conflicts/restart/reconciliation | Extreme | Critical duplicates/conflicts | 6–12 sprints |
| 9. Competition Platform | Multi-tournament production rollout/operations | Extreme | Critical operational/security | staged program |

## Phase gates

### Phase 1 — Current Prototype

Deliver characterization harness; JSON/XML fixtures; division/team/result/finals/award tests; route/print smoke baselines. **Exit:** current behavior reproducible and `app.js` unchanged except separately approved test seams.

### Phase 2 — Repository Migration

Implement Repository in tests with LocalStorageProvider; shadow legacy; migrate save, load, reset, startup import, XML export/import one at a time. **Exit:** no direct localStorage outside provider; exact formats and rollback proven.

### Phase 3 — IndexedDB

Approve sync identity/version model; implement schema migrations, staged packages, atomic entity+outbox+audit, leases, quota/failure tests. **Exit:** crash/restart/integrity/scale tests pass, no production Safe Mode yet.

### Phase 4 — Competition Package

Server/test package builder, signed manifest, scope, chunks, compatibility, freshness, staging/activation, readiness. **Exit:** corrupted/wrong/stale/incompatible package tests and offline read drill pass.

### Phase 5 — Safe Mode

Connectivity classification, authorized activation, locally pending UX, device ownership, emergency backup, low-risk pilot writes. **Exit:** staff drill, recovery, kill switch, no silent loss.

### Phase 6 — ASP.NET Core API

Layered solution, shared rule fixtures, competition-scoped auth, idempotent command API, health/capabilities, packages/changes/conflicts/audit. **Exit:** contract, auth, transaction, performance, security tests.

### Phase 7 — SQL Server

Production DDL/migrations, rowversion, stable IDs, tombstones, sync/audit/device/package tables, backup/restore/HA. **Exit:** migrated fixtures reconcile; rollback and RPO/RTO drill pass.

### Phase 8 — Synchronization Engine

Restartable pull/apply/push/ack, retries, partial batches, dependencies, tombstones, conflict review, telemetry. **Exit:** all `SAFE_MODE_TEST_PLAN.md` scenarios pass across devices.

### Phase 9 — Competition Platform

Internal -> shadow -> read-only package -> one designated device -> multiple designated devices -> broader rollout. Add monitoring, support, incident process, reconciliation, device inventory, retention, compliance, DR and SLAs.

## Cross-phase risks

Rule divergence between JS/server; unstable IDs; underestimated privacy/security; browser support/quota; network ambiguity; package staleness; multi-device conflicts; insufficient tournament drills; SQL operational readiness; architecture scope expanding faster than tests.

## Change control

Architecture changes after Sprint 4 are incremental: update the primary architecture entry, affected master/source document, stable decision, tests/gates, and changelog. Avoid full documentation rewrites unless explicitly commissioned.
