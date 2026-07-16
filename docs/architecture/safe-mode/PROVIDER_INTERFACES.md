# Safe Mode Provider Interfaces

## Common result/error model

Interfaces are design-only and asynchronous. Suggested return envelope: `Result<T> = { ok: true, value: T } | { ok: false, error: ServiceError }`. Errors include code, message key, retryable, correlation/operation ID, HTTP/status when relevant, and safe details. Expected codes: Offline, Timeout, Unauthorized, Forbidden, Validation, Conflict, NotFound, CompetitionClosed, StalePackage, IncompatibleVersion, Quota, Integrity, Storage, Cancelled, and Unknown.

## CompetitionRepository

**Responsibility:** application-facing state/query/command boundary; selects online/safe/sync behavior and guarantees durable local outbox writes.

**Methods:**

```text
initialize(context): Promise<Result<RepositoryStatus>>
getMode(): OperatingMode
getCompetition(id): Promise<Result<CompetitionView>>
query(entityType, query): Promise<Result<Page<Entity>>>
execute(command, options?): Promise<Result<CommandReceipt>>
resetLocalPackage(competitionId, authorization): Promise<Result<void>>
importXml(xml): Promise<Result<AppState>>
exportXml(state): Promise<Result<string>>
validate(state): Promise<Result<ValidationReport>>
```

**Dependencies:** OnlineApiProvider, IndexedDbProvider, SyncEngine, ConnectivityMonitor, domain validators/serializers. **Test doubles:** fake providers, fixed monitor, deterministic clock/ID generator.

## OnlineApiProvider

**Responsibility:** authenticated ASP.NET Core transport; no UI or conflict decisions.

```text
health(signal?): Promise<Result<ApiHealth>>
downloadPackage(request, signal?): Promise<Result<PackageStream>>
pullChanges(competitionId, checkpoint, pageToken?, signal?): Promise<Result<ChangePage>>
submitOperations(batch, signal?): Promise<Result<OperationBatchAck>>
getEntity(competitionId, type, id): Promise<Result<VersionedEntity>>
registerDevice(request): Promise<Result<DeviceRegistration>>
```

**Errors:** network/TLS/timeout/auth/scope/conflict/version/rate/5xx mapped without pretending auth failure is offline. **Dependencies:** HTTP client, auth token provider, serializer, telemetry. **Test doubles:** scripted API, latency/failure injector, idempotency simulator.

## IndexedDbProvider

**Responsibility:** transactionally store active/staging packages, projections, outbox, tombstones, checkpoints, conflicts, local audit, leases, and backups metadata.

```text
open(): Promise<Result<OfflineStoreInfo>>
stagePackage(manifest, chunks): Promise<Result<PackageValidation>>
activatePackage(packageId): Promise<Result<void>>
getEntity(scope, type, id): Promise<Result<Entity|null>>
query(scope, type, query): Promise<Result<Page<Entity>>>
commitLocalOperation(entityChange, operation, audit): Promise<Result<LocalReceipt>>
getPendingOperations(scope, limit): Promise<Result<OutboxOperation[]>>
recordAcknowledgements(acks): Promise<Result<void>>
applyChangePage(page): Promise<Result<void>>
saveConflict(conflict): Promise<Result<void>>
acquireSyncLease(deviceId, ttl): Promise<Result<Lease>>
releaseSyncLease(lease): Promise<Result<void>>
getReadiness(scope): Promise<Result<PackageReadiness>>
purgePackage(scope, authorization): Promise<Result<void>>
```

**Errors:** unavailable/quota/transaction/integrity/version/blocked upgrade. **Dependencies:** IndexedDB adapter, schema migrations, hash validator, clock. **Test doubles:** in-memory transactional store and fault-injecting store.

## SyncEngine

**Responsibility:** restartable pull/apply/push/ack/checkpoint state machine.

```text
start(scope, options?): Promise<Result<SyncSession>>
pause(reason): Promise<Result<void>>
resume(): Promise<Result<SyncSession>>
syncOnce(scope): Promise<Result<SyncSummary>>
retryOperation(operationId): Promise<Result<OperationOutcome>>
cancelRetry(operationId, authorization): Promise<Result<void>>
getProgress(scope): Promise<Result<SyncProgress>>
```

**Errors:** lease, connectivity, auth, conflict, dependency, version, integrity, partial batch. **Dependencies:** API/IndexedDB providers, ConflictResolver, monitor, backoff/clock, leader election. **Test doubles:** deterministic scheduler, scripted providers/resolver.

## ConnectivityMonitor

**Responsibility:** classify verified service availability and publish mode-relevant transitions.

```text
start(): void
stop(): void
getStatus(): ConnectivityStatus
checkNow(signal?): Promise<ConnectivityStatus>
subscribe(listener): Unsubscribe
```

Status distinguishes Online, Suspect, InternetUnavailable, ApiUnavailable, AuthRequired, Incompatible, and Unknown. **Dependencies:** browser network hints, API health endpoint, clock/backoff. **Test doubles:** manual status emitter and scripted health checker.

## ConflictResolver

**Responsibility:** apply approved entity policy, create review records, and produce explicit resolution commands.

```text
classify(localOperation, serverEntity, policy): Promise<Result<ConflictAssessment>>
autoResolve(assessment): Promise<Result<Resolution|null>>
listConflicts(scope, filter?): Promise<Result<ConflictRecord[]>>
resolve(conflictId, decision, authorizedActor): Promise<Result<ResolutionOperation>>
```

Protected conflicts return ReviewRequired, never a silent merged entity. **Dependencies:** conflict-policy registry, domain comparison, authorization, audit. **Test doubles:** fixed classifier and reviewer simulator.

## EmergencyBackupService

**Responsibility:** verified encrypted export/import of offline package, outbox, checkpoint, conflicts, and audit for recovery review.

```text
estimate(scope): Promise<Result<BackupEstimate>>
create(scope, credentials, destination): Promise<Result<BackupReceipt>>
verify(source, credentials): Promise<Result<BackupManifest>>
inspect(source, credentials): Promise<Result<RecoveryPreview>>
restoreToStaging(source, credentials): Promise<Result<RecoverySession>>
submitRecovery(session, authorization): Promise<Result<RecoverySubmission>>
```

Never writes directly to authoritative entities. **Errors:** crypto/integrity/wrong competition/version/quota/cancel/access. **Dependencies:** IndexedDB provider, crypto/key service, file adapter, audit. **Test doubles:** memory archive and corruption injector.

## Interface rules

- CompetitionId, DeviceId, UserId, OperationId, BaseVersion, cancellation, and correlation are explicit where applicable.
- Return values are immutable DTOs; no DOM/localStorage access.
- Test doubles must simulate restart, duplicates, partial failure, stale versions, quota, and conflict.
- Current `Repository.js` is not changed or connected in Sprint 3; these signatures guide later approved implementation.

