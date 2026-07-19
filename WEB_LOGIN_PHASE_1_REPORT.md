# Web Login Phase 1 Report

Date: 2026-07-13

## Scope

Added a StackMeet web-app login layer on top of API Security Phase 1.

The login page requires:

- Competition ID / Key
- Name
- Password

The logged-in Competition ID / Key is used as the runtime `CompetitionState` key. Existing verified data can still be opened by logging in with `DEFAULT`.

## Backend Changes

- Added `POST /api/auth/login`.
- Added signed bearer session tokens using HMAC SHA-256.
- Protected API endpoints now accept either:
  - server API key via `X-StackMeet-Api-Key`, or
  - browser session token via `Authorization: Bearer <token>`.
- `/api/health`, `/api/version`, and `/api/auth/login` remain public.
- `/api/state/{competitionKey}` checks that a browser session can only access its own competition key.
- No login password or signing key is stored in `appsettings.json`.

## Frontend Changes

- Added `js/auth/AuthSession.js`.
- Added a login screen to `index.html`.
- Added logout and current competition display in the top bar.
- Updated `ApiProvider`, `Repository`, and `StackerApi` to send the bearer token.
- App startup now waits for login before loading competition data.
- Updated files were synced into `backend/StackMeet.Api/wwwroot`.

## Runtime Configuration Required

Set these outside source control:

```powershell
$env:Security__LoginPassword = "<strong-login-password>"
$env:Security__SessionSigningKey = "<long-random-signing-key>"
```

Keep the Phase 1 server API key if you still want direct API/admin testing:

```powershell
$env:Security__ApiKey = "<strong-api-key>"
```

Optional session duration:

```powershell
$env:Security__SessionMinutes = "480"
```

## Verification Completed

- `dotnet build StackMeet.sln -c Release --no-restore` passed.
- JavaScript syntax checks passed for app/auth/storage files.
- Existing storage smoke test passed.

## Runtime Test Checklist

1. Open the hosted web app.
2. Confirm the login page appears before the dashboard.
3. Login with Competition ID / Key `DEFAULT` and the configured login password.
4. Confirm the dashboard loads existing competition JSON.
5. Confirm protected API calls now use `Authorization: Bearer ...`.
6. Click Log Out and confirm the login screen returns.
7. Try wrong password and confirm login is rejected.