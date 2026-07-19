# Sprint 10.6 Report - Relay Team Business Rules

## Implemented

- Competition-scoped Relay Team Setup remains visible for Timed Relay or Head-to-Head events.
- Added independent Timed Relay and Head-to-Head division values to the browser relay-team model and UI.
- Added Member 1-4 and Optional Member 5-6 presentation.
- Added derived Draft, Incomplete, Ready, and Locked status presentation.
- Preserved draft teams with zero members and allowed Draft/Incomplete saves.
- Retained the existing four-member eligibility threshold for relay participation.
- Derived default team divisions through the shared stacker division and age-calculation service.
- Prevented changes and deletion after the configured competition start date.

## Files modified

- `app.js`
- `index.html`
- `tests/characterization.test.js`
- `docs/sql-native/SPRINT_10_6_RELAY_TEAM_RULES.md`

## Regression results

- JavaScript syntax checks passed for `app.js`, hosted `wwwroot/app.js`, and all `js/**/*.js` files.
- Storage smoke test passed.
- Characterization suite passed: 17 scenarios, including the expanded relay status, four-member minimum, six-member maximum, independent-division, Head-to-Head availability, and shared-age-rule coverage.
- `dotnet restore StackMeet.sln --configfile NuGet.Config` passed.
- `dotnet build StackMeet.sln -c Release --no-restore` passed with zero warnings and zero errors.
- `dotnet publish` passed to `backend/StackMeet.Api/bin/Release/net8.0/publish`.
- Source, hosted `wwwroot`, and publish hashes match for `app.js` and `index.html`.

## Additional API regression note

The existing Phase 1A API suite was also attempted. The API health endpoint started successfully, but its first database-backed Competition request closed the connection unexpectedly. This is a configured local SQL runtime/infrastructure issue outside the Sprint 10.6 relay changes; no API or SQL source was changed to work around it.

## Deployment notes

Publish copies the static application assets into `backend/StackMeet.Api/wwwroot` before producing the Release publish package. Hosted deployment remains a separate IIS operation.
