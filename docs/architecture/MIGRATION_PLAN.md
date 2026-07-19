# Storage Repository Migration Plan

## Purpose

Introduce a storage boundary incrementally while preserving current behavior, localStorage key/JSON, XML, startup import, reset behavior, and all competition rules.

## Current flow

```text
+---------------------+
| UI and app.js       |
| rendering + rules   |
+----------+----------+
           |
           | direct JSON.parse / JSON.stringify
           v
+---------------------+
| browser localStorage|
| stackmeet-...-v1    |
+---------------------+
```

Current direct operations are inside `loadState()`, `applyStartupImport()`, and `saveState()`. Reset replaces global state with normalized demo data; the following render saves it. XML import/export is implemented directly in `app.js` and must remain byte/field compatible.

## Future flow

```text
+---------------------+
| UI / route modules  |
+----------+----------+
           |
           v
+---------------------+
| Repository          |
| state contract      |
+----------+----------+
           |
           v
+---------------------+
| Provider / adapters |
+----------+----------+
           |
           v
+---------------------+
| Storage             |
| localStorage / API  |
+---------------------+
```

The UI requests application state operations. Repository coordinates validation, normalization, defaults, startup import, XML serialization, and reset. Provider owns browser storage calls only. A future API provider can replace localStorage without placing transport logic in UI/business services.

## Sprint 2 baseline

- `Repository.js` defines the public interface only.
- `LocalStorageProvider.js` encapsulates current-key JSON load/save/clear operations.
- Neither file is loaded by `index.html` or called by `app.js`.
- Provider smoke tests use an isolated in-memory storage implementation.
- Existing application behavior is therefore unchanged.

## Migration stages

### Stage 0 — Characterize current behavior

- Freeze fixtures for missing, valid, corrupt, demo, imported, and legacy localStorage data.
- Freeze XML import/export round-trip fixtures.
- Characterize startup import and reset/save timing.
- Record current error/fallback behavior before redirecting any call.

**Exit:** Tests reproduce existing behavior without the repository.

### Stage 1 — Provider parity

- Compare provider `load()` with current `JSON.parse(localStorage.getItem(storageKey))`.
- Compare provider `save()` byte-for-byte with `JSON.stringify(state)` under the existing key.
- Treat provider `reset()` as storage clearing only; Repository later coordinates creation/saving of normalized defaults so visible reset behavior remains unchanged.
- Test quota, unavailable storage, invalid JSON, and missing keys without changing current production handling.

**Exit:** Provider behavior is proven compatible but remains disconnected.

### Stage 2 — Repository implementation beside legacy flow

- Implement Repository with injected provider, defaults, normalizer, and XML adapter.
- Keep current `app.js` calls active.
- Run repository calls against fixtures in tests only.
- Require deep equality for loaded/reset state and exact XML compatibility.

**Exit:** Repository is behaviorally equivalent in tests and still has no runtime caller.

### Stage 3 — Shadow comparison

- In a controlled test harness, run legacy and repository load/save/reset/import/export paths over identical inputs.
- Compare state, serialized JSON, XML, exceptions/fallbacks, and side effects.
- Do not enable browser production routing yet.

**Exit:** No unexplained difference across approved fixtures.

### Stage 4 — Migrate one boundary at a time

Recommended order:

1. `saveState()` delegates to Repository save.
2. Startup load delegates to Repository load.
3. Reset delegates to Repository reset.
4. Startup bundled import moves behind Repository coordination.
5. XML export delegates to Repository export.
6. XML import delegates to Repository import.
7. Validation becomes an explicit Repository result only after current validation behavior is fully characterized.

Each step is a separate change with rollback and regression evidence. Do not combine load, save, reset, and XML migration in one commit.

### Stage 5 — Remove legacy direct access

- Confirm no `localStorage` references outside Provider.
- Confirm no XML parsing/serialization outside Repository serializer boundary.
- Remove legacy code only after every route and persistence fixture passes.
- Update architecture documents and decision log.

**Exit:** UI depends on Repository, Repository depends on Provider, and persisted formats remain unchanged.

### Stage 6 — Future API provider

- Define competition-scoped API contracts and revision/concurrency rules.
- Implement a provider matching the repository contract.
- Migrate data through tested XML/SQL mapping.
- Keep browser localStorage as an approved fallback only if explicitly decided.

## Compatibility invariants

- Key remains `stackmeet-stacktrack-style-v1`.
- JSON property names, value types, and current normalization remain unchanged.
- XML tags, attributes, value conversions, and omission behavior remain unchanged.
- Startup import heuristic and `importBatch` behavior remain unchanged until separately approved.
- Reset still produces normalized demo/default state and persists through the same visible workflow.
- Repository/provider code contains no competition rules.
- UI and report/print behavior remain identical.

## Risks and controls

| Risk | Control |
|---|---|
| Missing storage mistaken for corrupt storage | Separate fixtures for null, empty, invalid JSON, and valid values. |
| Reset changes from save-default to remove-only | Repository owns reset orchestration; provider clear is never connected directly to UI. |
| JSON or XML drift | Byte/deep-equality fixtures before migration. |
| Startup import runs twice or clears operational data | Explicit importBatch/startup fixtures and one coordinator. |
| Storage errors become newly visible/hidden | Characterize existing catch/fallback behavior first. |
| Two active persistence paths diverge | Shadow tests and one-call-at-a-time cutover. |
