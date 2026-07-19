# CTO Decisions

Use this log for approved architecture and engineering decisions. Add one entry per decision and never reuse a number.

---

## Decision Number

`CTO-___`

## Title

`[Short decision title]`

## Reason

`[Business or engineering reason, alternatives considered, constraints, and consequences]`

## Status

`[Proposed | Approved | Rejected | Superseded | Deprecated]`

## Date

`YYYY-MM-DD`

---

## Decision Number

`CTO-___`

## Title

`[Placeholder]`

## Reason

`[Placeholder]`

## Status

`[Placeholder]`

## Date

`[Placeholder]`


---

## Decision Number

`CTO-001`

## Title

StackMeet is online-first with Competition Safe Mode fallback

## Reason

Normal operation should use the central API while a controlled, auditable offline mode preserves competition continuity during temporary connectivity or server outages.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-002`

## Title

Microsoft SQL Server is the authoritative central database

## Reason

One authoritative transactional database is required for competition scoping, concurrency, official results, audit, backup, and recovery. Offline stores are replicas and queues, not independent authorities.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-003`

## Title

IndexedDB is the intended browser offline store

## Reason

IndexedDB supports structured, transactional, competition-scoped packages, outbox operations, checkpoints, tombstones, and larger datasets beyond the appropriate use of localStorage.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-004`

## Title

Official result conflicts must never be resolved silently

## Reason

Attempts, scratches, and placements determine official outcomes. Conflicting values must preserve both versions and require authorized head-judge or results-admin review with a complete audit trail.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-005`

## Title

Offline changes use an outbox with unique operation IDs

## Reason

A durable outbox and globally unique idempotency key allow safe retries, partial synchronization, restart recovery, acknowledgements, and duplicate prevention.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-006`

## Title

Safe Mode data is scoped to one downloaded competition package

## Reason

Strict competition isolation limits data exposure, prevents cross-tournament writes, improves integrity/readiness validation, and keeps offline data operationally manageable.

## Status

Proposed

## Date

2026-07-11

---

## Decision Number

`CTO-007`

## Title

Initial offline operation uses designated device and data ownership

## Reason

Assigning devices to stations, tables, events, divisions, or workflow areas reduces overlapping offline edits and conflict risk during the first production rollout without weakening version checks or audit.

## Status

Proposed

## Date

2026-07-11
