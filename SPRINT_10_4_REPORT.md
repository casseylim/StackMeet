# Sprint 10.4 Report

## Files modified

- `index.html`: Competition Settings wording, list-first registration layout, production title/labels, and development reset removal.
- `app.js`: Special-only gender split handling, immediate division regeneration, list-first registration form state, focus return, and branding cleanup.
- `tests/characterization.test.js`: retains the setting-off and setting-on Special division behavior checks.
- `backend/StackMeet.Api/wwwroot/index.html` and `app.js`: synchronized runtime copies.
- `backend/StackMeet.Api/publish`: regenerated IIS package.
- `docs/sql-native/SPRINT_10_4_UX_POLISH.md`: UX design, before/after flow, and local screenshots.

## Regression results

- JavaScript syntax checks: passed for source, IIS web-root, and storage scripts.
- Storage smoke tests: passed.
- Characterization suite: passed all 17 scenarios.
- Phase 1A API regression suite: passed all 24 checks.
- Release build: passed with zero warnings and zero errors.
- Publish: succeeded.
- Source/published runtime hashes: matched.

## Deployment notes

The validated publish folder is `backend/StackMeet.Api/publish`. Deploy it using the myASP.NET SOP: stop the pool, upload the publish folder, verify DLL timestamp, start the pool, then verify health, Swagger, frontend, and the two-browser smoke test.

Hosted deployment is still required before Sprint 10.4 can be declared complete. The workspace has no remote IIS hostname or authority to perform that final verification.
