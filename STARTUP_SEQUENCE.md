# StackMeet RC1 Startup Sequence

## Fixed competition key

`Repository` constructs one `ApiProvider` with the literal key `DEFAULT`. No browser-specific, user-specific, URL-derived, or randomly generated key exists in the current code.

## Sequence

```text
Browser loads index.html
  -> ApiProvider.js loads
  -> Repository.js loads
  -> app.js constructs Repository(DEFAULT)
  -> app.js creates an in-memory initial state (not yet persisted)
  -> initializeApplication()
  -> loadState()
  -> Repository.load()
  -> ApiProvider.load()
  -> GET /api/state/DEFAULT
  -> CompetitionStateController.Get()
  -> SQL dbo.CompetitionState
```

### GET returns 200

The returned JSON is passed through `applyStartupImport()` and `normalizeState()`, then rendered. `render()` invokes `saveState()`, which POSTs the resulting complete JSON document back to `/api/state/DEFAULT`.

### GET returns 404

`ApiProvider.load()` returns `null`. `loadState()` calls `createInitialState()`, immediately POSTs that state to `/api/state/DEFAULT`, and then renders it. The render also invokes the regular save path.

### GET fails for another reason

For network errors and non-404 HTTP responses, `ApiProvider.load()` throws. `loadState()` does not catch that rejection, so it does not substitute or persist a default state and `render()` is not reached.

## Synchronization behavior

All current browsers address the same SQL row (`CompetitionKey = DEFAULT`), but there is no polling, push synchronization, concurrency token, or conflict protection. A browser sees another browser's save only after it reloads. Each render POSTs its full in-memory snapshot, so the last completed writer wins.
