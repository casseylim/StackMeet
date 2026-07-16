# Synchronization Data Schema

## Status

Design proposal only. Current JavaScript state, localStorage JSON/XML, and `database/schema.sql` are unchanged in Sprint 3.

## Common envelope fields

| Field | Purpose | IndexedDB/outbox | SQL/API direction |
|---|---|---|---|
| `Id` | Stable internal entity ID; UUID/client-reserved for offline creates | Entity primary key | SQL PK or alternate sync UUID; current public codes remain separate |
| `CompetitionId` | Immutable tenant/scope | Partition/key on every record | FK to `competitions.id`, required on competition data |
| `CreatedAt` / `CreatedBy` | Original creation audit | Stored from server/local operation | UTC datetime/user FK |
| `UpdatedAt` / `UpdatedBy` | Last accepted update audit | Server value plus local projection metadata | UTC datetime/user FK |
| `Version` | Optimistic concurrency token | Opaque last server version | SQL `rowversion` exposed base64/ETag |
| `IsDeleted` / `DeletedAt` | Tombstone lifecycle | Retained tombstone | Soft-delete/tombstone fields or separate change log |
| `DeletedBy` | Deletion actor | Tombstone audit | User FK |
| `DeviceId` | Originating device | Package/outbox/audit | Registered device/audit FK |
| `OperationId` | Idempotent command identity | Outbox primary key | Idempotency/audit unique key with CompetitionId |
| `BaseVersion` | Version edited by client | Outbox operation | Compared in transaction; not entity current field |
| `SyncStatus` | pending/uploading/ack/conflict/failed/deferred | Local metadata | Optional operation status, not business entity truth |
| `LastSyncAttempt` | Retry time | Outbox metadata | Optional telemetry |
| `SyncError` | Structured last error | Outbox/conflict metadata | Optional operation failure record |

Recommended additional fields: `EntityType`, `EntityId`, `Action`, `Payload`, `LocalSequence`, `Dependencies`, `AttemptCount`, `ServerReceivedAt`, `AcknowledgedAt`, `Checkpoint`, `PackageId`, and `SchemaVersion`.

## Existing JavaScript mapping

| Current state | Current IDs/fields | Proposed sync mapping | Future SQL |
|---|---|---|---|
| `settings` | no entity ID; competition singleton | CompetitionId, Version, update operation; split stages/events/config | `competitions`, `competition_stages`, `competition_events` |
| `divisionSettings` / `divisions` | names/cutoff arrays | stable cutoff/division IDs + versions/tombstones | `division_cutoffs`, `divisions` |
| `stackers[]` | public `id` (`1.x`) | add internal sync Id; keep bib code; version/audit/delete fields | `stackers`, `organizations` |
| `doubles[]` | public `id` (`2.x`), member codes | stable team/member IDs, separate versioned membership ops | `teams`, `team_members` |
| `relays[]` | public `id` (`3.x`), member codes | stable team/member IDs, ordered membership ops | `teams`, `team_members` |
| `results[]` | result `id`, participant public code, attempts array | stable result/attempt IDs; separate attempt versions; protected ops | `results`, `result_attempts`, `time_sheets` |
| `awards` | singleton config | versioned award plan/config aggregate | new award plan tables required |
| `translations` | language/key dictionary | per competition/language/key versioned entities | new translation table/config required |
| `leaderboard` | singleton config | competition singleton with Version | `leaderboard_settings` |
| `notifications` | `id`, read | versioned server notification and user/device read op | `notifications` (read model may need normalization) |
| `users` | local display data | authenticated user/session/device claims; never trust local record as auth | `users`, `user_sessions`, new devices table |
| `importBatch` | batch string | migration/import audit only, not authoritative sync version | import/audit table needed |

Current calculated fields such as stacker `age`/`division`, official time, finalists, and placements require a declared authority. Prefer storing authoritative inputs and server-calculated outcomes/version while allowing client preview; never sync derived values independently without calculation version and source inputs.

## Proposed new server support tables

- `devices`: DeviceId, competition assignment, name, role/capabilities, active/last seen.
- `sync_operations`: CompetitionId + OperationId unique, request hash, outcome, entity/version, timestamps.
- `change_feed`: ordered competition checkpoint/change sequence including tombstones.
- `audit_events`: append-only actor/device/operation/before-after/resolution.
- `conflicts`: durable review lifecycle and decisions.
- `competition_packages`: package ID/version/checkpoint/manifest/hash/signature/expiry.

These are future proposals; schema change requires separate approval/migration.

## Type and security rules

- UTC ISO-8601 over API; SQL datetimeoffset/datetime2 UTC.
- Rowversion is opaque and never ordered/interpreted by JavaScript.
- Device/user IDs come from authenticated registration, not arbitrary UI input.
- Payloads minimize personal data and are encrypted in transit; sensitive offline retention follows package policy.
- Sync metadata must not enter current XML until explicitly approved.

