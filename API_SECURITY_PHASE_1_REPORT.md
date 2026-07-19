# API Security Phase 1 Report

Date: 2026-07-13

## Scope

Implemented a backend-only API security foundation for `backend/StackMeet.Api`.

No frontend storage activation was changed. The frontend can continue using `LocalStorageProvider`.

## Changes

- Added API key protection for data endpoints under `/api`.
- Kept `/api/health` and `/api/version` public for uptime checks.
- Kept Swagger and `/debug` available only in Development.
- Added configurable CORS via `Security:AllowedOrigins`.
- Added request body limit of 10 MB.
- Added security response headers:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
  - `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- Enabled HTTPS redirection and HSTS outside Development.

## Runtime Configuration Required

Set the API key outside source control:

```powershell
$env:Security__ApiKey = "<strong-secret>"
```

Optional production CORS configuration:

```powershell
$env:Security__AllowedOrigins__0 = "https://your-stackmeet-host.example"
```

The request header expected by default is:

```text
X-StackMeet-Api-Key
```

## Protected Endpoints

These now require the configured API key:

- `/api/state/{competitionKey}`
- `/api/competitions`
- `/api/competitions/{competitionId}/stackers`

## Open Follow-Up

This phase protects the API surface with a shared deployment key. It does not yet implement per-user login, roles, audit identity, password policy, or session management.
