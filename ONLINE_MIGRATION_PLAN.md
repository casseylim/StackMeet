# Online migration plan

## Sprint 1: compatibility layer (this delivery)

- Add an ASP.NET Core API that stores exactly one full JSON state document per `CompetitionKey`.
- Add an inactive `ApiProvider` with `load()` and `save()` methods.
- Keep `LocalStorageProvider`, `CompetitionRepository`, application logic, XML, printing, and all browser behavior unchanged.
- Keep `config.js` at `STORAGE_MODE = "local"` and do not load it into the existing page yet.

## Sprint 2: controlled activation

1. Deploy the API behind the same production HTTPS origin at `/api`.
2. Add a separate release-controlled frontend integration that loads `config.js` and `ApiProvider.js`.
3. Preserve the existing repository call shape while choosing `LocalStorageProvider` only when the explicitly approved mode is `local` and `ApiProvider` only when it is `api`.
4. Import an approved XML snapshot into the selected online competition key through a controlled one-time procedure.
5. Run full characterization, storage, browser, XML, print, and multi-browser save/load tests before enabling the API mode for event operations.

## Explicitly deferred

- API redesign and normalized entities.
- Authentication, authorization, audit policy, synchronization, offline mode, conflict resolution, versioning, and concurrency policy.
- SQL migration of business entities or XML format changes.
- Business-rule, division, awards, results, or printing changes.

Until those items are designed and approved, this API is a temporary full-state compatibility store rather than the long-term StackMeet service.

