# ENG-02E — Shared API session and resource authorization (draft)

Base: 5b893a1069dc9e05d12062798db517dff9738a6f. Stacked on CI-passed ENG-02D. This is an incremental consolidation, not a full ASP.NET authentication-scheme migration.

## Changes

- Shared CurrentAccountQuery enforces account ID, active status, session version and admin flag in the HTTP middleware, SignalR join and auth identity/logout handlers. In particular /api/auth/me with a maintenance key plus revoked bearer must not return account data. Normal logout remains a best-effort no-content operation, but invalid account sessions do not write a logout identity audit.
- CompetitionPermissionService now owns competition resource scope checks for key and SQL ID; middleware and hub reuse them. Legacy state and roster controllers independently enforce scope rather than trusting the path gate alone. Existing capability checks remain at the action boundary.
- Central read-scope projection used by competition list, detail, activity descriptor and Finals ranking rules. Missing authority produces no rows; maintenance authority remains explicit; account assignments require active user and recognized role. System admin and scoped legacy behavior retained.
- Existing route names, status codes for supported roles, session formats, public publication, key separation, lifecycle checks, result revisions and persisted data remain. Role checks are based on current database state; bulk notification fanout retains its batched authorization implementation.

## Regression matrix

Synthetic loopback tests cover Viewer/DataEntry/CompetitionManager reads, management and result-entry gates, cross-competition key/ID, unassigned account, legacy and maintenance separation, admin key separation, revoked bearer combined with maintenance key. Existing admin account, revocation and historical tests remain. Hosted execution pending.

## Remaining work

Route-prefix middleware and controller checks remain; no claim of framework-wide declarative policy migration. A subsequent endpoint inventory/metadata policy migration must explicitly cover every route, distinguish public endpoints, retain recovery tooling and verify status compatibility. Authorization check versus concurrent mutation is not transactionally serialized everywhere. Key rotation, legacy retirement, permission-change races, load performance, owner publication policy and deployment approval remain outstanding. No database migration, production access, historical result/certificate edits or master merge.
