# Safe Mode Roadmap Update

## Phase 0 — Current static prototype (current)

- Browser SPA, direct global state and localStorage JSON.
- XML import/export and current competition workflows.
- Sprint 1 architecture/state/service documentation.
- Sprint 2 disconnected Repository interface and LocalStorageProvider smoke tests.

**Gate:** Preserve current behavior and formats while adding characterization fixtures.

## Phase 1 — Repository migration

1. Automated fixtures for load/save/reset/startup import/XML and critical competition rules.
2. Implement Repository in tests with LocalStorageProvider; prove parity.
3. Shadow comparison; migrate one production storage call per release.
4. Remove direct storage only after full parity.

**Gate:** No direct localStorage outside provider; no format drift; rollback proven.

## Phase 2 — Sync foundations and IndexedDB

1. Approve CTO Safe Mode decisions/conflict policy.
2. Stable internal IDs, versions, DeviceId/OperationId, package/checkpoint formats.
3. IndexedDB provider with schema upgrades, staging/activation, atomic entity+outbox+audit transactions, leases, and quota handling.
4. In-memory/fault test doubles and restart tests.

**Gate:** Offline provider passes integrity, crash, restart, and scale tests without UI cutover.

## Phase 3 — PWA preparation

- HTTPS, app manifest, controlled service worker/app-shell caching, update/version policy, offline startup, storage persistence request, install/device readiness.
- Never cache authenticated API responses blindly; package data stays IndexedDB.
- Force compatible client/package before protected writes.

**Gate:** App-shell offline load and safe update/rollback proven on supported browsers/devices.

## Phase 4 — ASP.NET Core API

- Competition-scoped endpoints, authenticated device registration, package download, incremental changes, idempotent operation batch, conflict review, audit, and health/capability/version endpoints.
- Domain validation and result rules authoritative server-side with shared fixtures.

**Gate:** API contract/version, idempotency, authorization, transactions, and load tests pass.

## Phase 5 — Microsoft SQL Server authority

- Port current proposed schema to SQL Server types/constraints.
- Add rowversion, tombstones/change feed, devices, sync operations, packages, conflicts, and audit tables through approved migrations.
- XML/state migration, backups, monitoring, HA/restore drills.

**Gate:** Reconciliation and rollback prove no cross-competition leakage or result loss.

## Phase 6 — Authentication and authorization

- ASP.NET Core Identity/external identity decision; competition roles/capabilities; device assignment/revocation; short-lived tokens and offline authorization envelope.
- Head-judge/admin protected conflict resolution and two-person finals policy decision.

**Gate:** Threat model, penetration tests, role matrix, revocation/offline-expiry tests approved.

## Phase 7 — Outbox synchronization

- Online Provider + Sync Engine; pull/checkpoint, outbox, idempotency, retry, partial response, tombstones, restart resume, leader lease, telemetry.
- Start read-only package pilot, then low-risk writes, then designated result stations.

**Gate:** All Sprint 3 sync test scenarios pass; kill switch and support runbook ready.

## Phase 8 — Conflict resolution

- Policy registry and review UI; protected results never silent.
- Audit and authorized resolution operations; data ownership assignments.
- Operational training for TD/head judge.

**Gate:** Multi-device drills with deliberate result/team/settings conflicts and signed acceptance.

## Phase 9 — Production rollout

1. Internal test competitions.
2. Shadow online-only pilot.
3. Package/read-only Safe Mode pilot.
4. Single designated offline entry device.
5. Multiple designated devices by station/data ownership.
6. Broader rollout only after metrics and incident review.

Required operations: dashboards/alerts, support escalation, backups/restore, device inventory, compatibility matrix, incident and post-competition reconciliation reports, kill switch, and rollback.

## Explicit non-goals before approval

- No multi-master silent merge.
- No offline package containing all competitions or password hashes.
- No whole-state last-write-wins upload.
- No official-result auto-resolution.
- No Safe Mode production use before package, sync, conflict, security, and recovery drills pass.

