# SQL-Native Core Phase 1B Test Results

Date: 2026-07-12

## Completed verification

- Applied `20260711175415_AddStackerRegistrationFields` successfully. It adds only Stacker registration metadata and does not modify CompetitionState.
- Selected explicit SQL Competition: `Id=7`, code `STACKMEET-P1B-2026`.
- Phase 1A API suite passed all 24 checks against the updated API, including create/list/get/update/delete, duplicate rejections, 400/404 behavior, foreign-key delete protection, UTC timestamps, and the new registration-field persistence assertion.
- `node --check` passed for source and IIS-web-root `app.js` and `StackerApi.js`.
- Storage smoke test passed.
- Existing characterization suite passed all 17 scenarios.
- `dotnet build StackMeet.sln -c Release --no-restore` passed with zero warnings and zero errors.
- `dotnet publish` completed to `backend/StackMeet.Api/publish`.
- SHA-256 hashes match between local and IIS package for `index.html`, `app.js`, `styles.css`, and `js/storage/StackerApi.js`.

## Hosted two-browser test

Not yet run. This workspace contains the IIS publish package but no deployed IIS hostname, browser access to that host, or authority to replace the live IIS site. Phase 1B must not be marked complete until the hosted test is performed using:

1. Browser A: open `/?competitionId=7#stackers`, create Stacker A, wait for Saved.
2. Browser B: open the same URL, refresh, and confirm A appears; create B.
3. Browser A: refresh and confirm B appears; edit and delete a record in one browser and confirm the change after refresh in the other.
4. Create two different StackerCodes concurrently and confirm both remain; attempt a duplicate and confirm rejection.
5. Restart/reload and confirm SQL records persist; verify a legacy module still loads.
