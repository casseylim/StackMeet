# StackMeet RC1 Online Sync Investigation

## Scope

Investigation only. No production code, controllers, business rules, API routes, EF Core migrations, or SQL were changed.

## Findings

### 1. Competition key

The current `Repository` creates `ApiProvider("DEFAULT")`. Therefore every browser that receives the current hosted frontend uses `GET /api/state/DEFAULT` and `POST /api/state/DEFAULT`.

### 2. Startup does load before selecting the initial state

`app.js` creates an in-memory initial state during script evaluation, but the visible application startup is `initializeApplication()` -> `loadState()` -> `Repository.load()`.

There is no `createDefaultCompetition()` function. The equivalent current function is `createInitialState()`, and it is used only after a `404` response from `Repository.load()`.

### 3. Load failures are not silently replaced with a default

`ApiProvider.load()` returns `null` only for HTTP `404`. Other HTTP responses and network failures throw. `loadState()` has no catch block, so those failures do not select or save a default state.

### 4. SQL read/write path

The controller GET performs an `AsNoTracking()` lookup by `CompetitionKey` and returns the stored JSON unchanged. POST finds the same key, then inserts or overwrites its full JSON document. The two operations use the same key and table path.

The live read-only check returned `200 OK` for `DEFAULT`; its stored JSON had 137 stackers, an `importBatch`, and 0 relays. The current state is therefore reachable through SQL and is not a separate-browser key issue.

### 5. Relay disappearance

Relay creation appends the relay to `state.relays`, after which the shared click handler renders. Rendering calls `saveState()`, which queues a full-state POST.

`normalizeRelays()` maps each incoming relay and limits its members to six, but it does not remove a relay. Therefore normalization is not the direct cause of a persisted relay disappearing.

The stored SQL JSON currently has no relays. From source and current-state evidence, this proves the relay is absent at load time; it cannot prove whether its originating POST never completed or a later full-state POST overwrote it.

Two code paths can produce the observed loss:

1. The relay POST is still in flight when the page refreshes or closes. UI callers do not await `saveState()`, and `fetch` is not configured with `keepalive`, so the new page can load the earlier SQL document.
2. Another browser, or a refreshed stale browser, loads an older snapshot and renders. Because render always POSTs the full state, that browser can overwrite the newer relay-containing document. There is no version, ETag, merge, or concurrency check to reject this last-write-wins overwrite.

For the currently stored row, `importBatch` is present and the stacker count is above the demo count. This means `applyStartupImport()` returns without clearing relays on normal reload of that particular stored document. The import path does contain a separate relay-clearing branch for a small, unmarked state, but that branch is not selected for the live row observed in this investigation.

## Conclusion

Current browsers use the same `DEFAULT` SQL competition. The primary RC1 synchronization risk is whole-document last-write-wins persistence combined with automatic saves on every render and saves that callers do not await. The evidence does not support normalization as the immediate cause of the missing relay.
