# Technical Decisions — Master Index

**Governance:** This consolidates status; it does not change it. Source record: `../CTO_DECISIONS.md`. Decision status may change only by explicit Product Owner instruction. IDs are stable and never reused.

## Approved

No numbered CTO decisions are currently Approved.

## Proposed

| ID | Title | Date | Architectural consequence |
|---|---|---|---|
| CTO-001 | StackMeet is online-first with Competition Safe Mode fallback | 2026-07-11 | Online normal mode plus controlled offline/sync modes |
| CTO-002 | Microsoft SQL Server is the authoritative central database | 2026-07-11 | Offline/local stores are replicas and queues |
| CTO-003 | IndexedDB is the intended browser offline store | 2026-07-11 | Transactional packages/outbox/checkpoints beyond localStorage |
| CTO-004 | Official result conflicts must never be resolved silently | 2026-07-11 | Protected conflict review and immutable audit |
| CTO-005 | Offline changes use an outbox with unique operation IDs | 2026-07-11 | Durable retry/idempotency/restart model |
| CTO-006 | Safe Mode data is scoped to one downloaded competition package | 2026-07-11 | Strict competition partition and package readiness |
| CTO-007 | Initial offline operation uses designated device and data ownership | 2026-07-11 | Reduced initial multi-device overlap/conflict risk |

Before implementation depending on these, Product Owner must approve or explicitly permit a bounded proof-of-concept that does not create production commitment.

## Deferred

No numbered decisions are currently Deferred.

Candidate decisions not yet assigned IDs: authentication platform; offline authorization lifetime; stable UUID strategy; package freshness/expiry; SQL hosting/HA/RPO/RTO; payment model; award/translation schema; change-feed/audit retention; finals second-person review; emergency backup encryption/retention; supported browser/device matrix.

## Deprecated

No numbered decisions are currently Deprecated.

## Decision process

1. Create next stable `CTO-###` in source log with reason, alternatives/consequences, Proposed status, and date.
2. Review with Product Owner and affected engineering/operations/security owners.
3. Change status only on explicit instruction; record approval/rejection/supersession date and superseding ID.
4. Update this index, primary architecture, affected specifications, roadmap/gates, and tests.
5. Never edit history to imply an unapproved decision was approved.
