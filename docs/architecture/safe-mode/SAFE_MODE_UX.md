# Competition Safe Mode UX

## Principles

- Status is persistent, plain-language, accessible, and never color-only.
- “Saved locally” is distinct from “synced to server.”
- Do not block unrelated work because one operation conflicts.
- Official-result risk is prominent and requires named authorized decisions.
- No Safe Mode UI is implemented in Sprint 3.

## Status indicator

Persistent top-bar status with icon/text and last verification:

- **Online — Synced** (green): pending 0, current checkpoint.
- **Online — Pending** (blue/amber): API reachable, N operations syncing.
- **Connection uncertain** (amber): checking API; avoid claiming offline too early.
- **Safe Mode — Offline** (amber/red): package age, device name, pending count.
- **Syncing**: pulled/uploaded/remaining/conflicts and cancellable detail panel, not a fake single percentage.
- **Action required** (red): failed operations/conflicts/auth/package expiry.

## Safe Mode banner

Sticky banner: “Competition Safe Mode: changes are saved on this device and pending server sync.” Show Competition, package generated/last refresh, DeviceId friendly name, current user/role, pending count, and details. Do not cover result-entry actions.

## Synchronization progress

Panel stages: checking service, downloading changes, validating, uploading operations, resolving acknowledgements, final verification. Show counts and last success. User may continue permitted work. Never say complete while blocking conflicts or eligible pending operations remain.

## Pending and failed operations

Pending drawer groups by entity/workstation and shows safe summary, created by/time, dependency, attempts, and status. Do not expose sensitive payload unnecessarily. Failed rows show retryable/nonretryable reason, next retry/manual action, correlation ID, and preserve original entry.

## Conflict review

Authorized screen shows server vs device values, base version, user/device/time, affected participant/team/event/sheet, policy, dependencies, and audit history. Actions: keep server, apply device as new authorized version, enter corrected value, or reject local operation. Official result conflicts require head-judge/results-admin role, reason, confirmation, and preferably second-person verification for finals.

## Package readiness

Pre-competition checklist card:

- competition and scope;
- Ready/Refreshing/Stale/Expired/Invalid;
- package/checkpoint/generated/last-refresh/expiry;
- app/schema compatibility;
- entity counts, integrity, quota/free space;
- offline authorization expiry and assigned station/data ownership;
- “Test Safe Mode readiness” action.

Stale warnings escalate by stage. Finals must not begin on an unacknowledged stale package without authorized emergency decision.

## Device identification

Display friendly device name plus short DeviceId on status/details and every printed/exported incident report. Registration records station/table/event/division ownership. Device rename/reassignment requires admin and audit.

## Manual Safe Mode activation

Available only to authorized TD/admin. Dialog displays current API health, last checkpoint, package readiness/age, unsynced count, device assignment, consequences, and reason. Require confirmation. Activation creates audit entry. Manual activation is blocked if no valid package unless an explicit emergency recovery policy permits read-only/limited use.

## Manual retry

“Retry now” triggers verified health/auth then idempotent sync. It does not create new OperationIds. Disable repeated clicks while active. Provide retry for one failed operation only when dependencies/policy allow.

## Emergency backup/export

Guided authorized flow: estimate size, select protected destination, create encryption credential/recovery key per policy, export, verify checksum, display receipt/BackupId. Clearly state backup is not server sync. Recovery imports to staging/review, never overwrites official results.

## Accessibility and operations

- ARIA live announcements for mode transition without interrupting time entry.
- Keyboard-accessible status/conflict actions and focus management.
- Icons + text + color; WCAG contrast.
- Localized messages with UTC/server and local time clearly labeled.
- Printed incident summary available for prolonged outage: package/checkpoint, devices, pending/conflicts, backups, and head-judge sign-off.

