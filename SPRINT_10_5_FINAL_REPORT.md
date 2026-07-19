# Sprint 10.5 Final Report

## Files modified

- `app.js`: Relay route availability, competition-scoped age setting load/save, shared age calculation, and Stackers list Age rendering.
- `index.html`: dropdown control plus updated Stacker columns.
- `styles.css`: wider, left-aligned wrapped Name column and table alignment.
- `tests/characterization.test.js`: existing 17 scenarios now also cover both age modes and Head To Head relay visibility.
- `backend/StackMeet.Api/wwwroot`: synchronized runtime assets.
- `backend/StackMeet.Api/publish`: regenerated IIS package.

## Verification

- Relay setup: Timed Relay enabled, Head To Head enabled, and both-off rule paths covered in the characterization scenario.
- Age examples: Actual Age and Year Born assertions pass in the characterization suite.
- Persistence: both scoped values saved and independently reloaded using the state API; original setting restored.
- JavaScript syntax checks: passed.
- Storage smoke test: passed.
- Characterization suite: passed all 17 scenarios.
- Phase 1A API suite: passed all 24 checks.
- Release build: passed with 0 warnings and 0 errors.
- Publish: succeeded; source/package hashes verified.

## Deployment notes

Deploy `backend/StackMeet.Api/publish` under the myASP.NET SOP. Post-deployment, verify the scoped age mode after a browser refresh and a second browser/device, then perform the listed Relay visibility and Stacker list checks. The hosted deployment remains external to this workspace.
