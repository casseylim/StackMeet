# Offline Competition Package

## Definition

A competition package is a signed, versioned, competition-scoped snapshot downloaded from the authoritative API into IndexedDB before Safe Mode use. It provides the minimum data needed to continue approved competition workflows.

## Included entities

- Competition identity, public code, dates, type, operational settings, enabled stages/events.
- Divisions and cutoff definitions.
- Stackers required for operations, including check-in/payment fields subject to access policy.
- Organizations.
- Doubles/Relay teams and memberships.
- Time sheets and participant/event/division assignments.
- Existing prelim/final results and attempts, scratch flags, penalties, official versions.
- Advancement configuration and currently generated finalist sheet references where authoritative.
- Award configuration needed for operational planning.
- Translation/configuration required by the installed client.
- Minimal authorized user/session/device claims usable offline, with expiry.
- Server checkpoint, per-entity versions, tombstones, package manifest, schema/app compatibility ranges, hashes, and signature.

## Excluded entities

- Password hashes, access codes, secrets, API tokens beyond short-lived protected offline claims.
- Other competitions.
- Unneeded user sessions, IP history, global audit history, and server logs.
- Full notification/history archives not needed for operation.
- Generated reports, screenshots, releases, backups, and temporary files.
- Sensitive registration fields not required by the device role.
- Server-only authorization policy and cryptographic private keys.

## Competition scoping

Every manifest, entity, outbox operation, checkpoint, conflict, backup, and IndexedDB key includes one immutable `CompetitionId`. A device may cache multiple packages only in isolated partitions; exactly one package is active in the competition UI. Cross-competition references are rejected.

## Preparation workflow

1. Authorized user registers/names the device and selects a competition/role.
2. API validates competition state, client version, authorization, and package policy.
3. Server creates a transactionally consistent checkpoint snapshot.
4. Client downloads chunks, validates manifest, hashes, counts, signature, versions, and references.
5. IndexedDB writes into a staging partition.
6. Client runs integrity/readiness checks and atomically activates the package.
7. User sees ready time, server checkpoint, package age, scope, device role, and storage estimate.
8. A readiness drill verifies offline open/read and a disposable write/rollback test.

## Version and compatibility

Manifest fields: `PackageFormatVersion`, `StateSchemaVersion`, `ApiContractVersion`, `MinimumClientVersion`, `MaximumClientVersion`, `CompetitionId`, `Checkpoint`, `GeneratedAt`, `ExpiresAt`, entity counts, chunk hashes, overall hash, signature/key ID, and required capabilities. Unsupported versions are never partially loaded.

## Freshness and refresh

- Readiness shows exact generated time and last successful incremental refresh.
- Recommended: full package before event day, refresh at opening, before prelim entry, before finals generation, and after major roster/team/settings changes.
- API policy defines maximum age by stage; finals packages require the strictest threshold.
- Refresh downloads incremental changes since checkpoint when compatible; otherwise stages and atomically replaces a full package.
- Refresh never removes unacknowledged outbox operations; it merges around them through sync/conflict policy.

## Integrity validation

Validate signature, TLS origin, competition ID, manifest/schema/client compatibility, chunk/overall hashes, entity counts, required stores, referential integrity, duplicate IDs, version presence, event/stage references, storage quota, outbox isolation, and an IndexedDB read-back sample. Failure leaves the prior active package intact.

## Expiry and archive

- `ExpiresAt` is policy, not silent deletion.
- Expired packages become blocked or restricted/read-only according to stage and authorized emergency policy.
- After competition closure and successful sync, archive locally for a short approved retention period, then encrypted-delete package/outbox after server reconciliation evidence.
- An unresolved outbox/conflict prevents automatic deletion.
- Emergency archives include manifest/checkpoint and audit evidence.

## Estimated sizes

Planning estimates, to validate with real exports:

| Competition | Entities | JSON payload | IndexedDB with indexes/outbox reserve |
|---|---:|---:|---:|
| Small (100 stackers) | 1k–3k | 1–3 MB | 5–15 MB |
| Medium (500 stackers) | 5k–15k | 5–15 MB | 20–60 MB |
| Large (2,000 stackers) | 20k–60k | 20–60 MB | 80–250 MB |

Images/attachments are excluded or separately cached. Readiness requires at least 3× estimated active package/outbox space and a quota check.

## Security

- TLS download plus signed manifest; reject tampering.
- IndexedDB is origin-isolated but not treated as encrypted secure storage by itself.
- Minimize fields by device role; avoid offline secrets.
- Device identity, authorized user, package expiry, and competition scope required.
- Offline authorization is time-limited and capability-scoped; high-risk actions may require a local authorized PIN/second person according to future policy.
- Emergency backups are encrypted, integrity-signed, access-controlled, and auditable.
- Shared devices require sign-out/lock and post-event data removal verification.

