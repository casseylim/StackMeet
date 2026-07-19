# StackMeet Developer Guide

## Philosophy

Protect the competition first. Correct official results, recoverable data, clear operator feedback, and audit evidence matter more than fast architectural change. Improve incrementally around the working prototype. A refactor preserves behavior; a rule/feature change needs separate approval.

Start with `SYSTEM_ARCHITECTURE_v1.md`, then read `DATABASE_MASTER_PLAN.md`, `MODULE_DEPENDENCY_MAP.md`, `SYSTEM_ROADMAP.md`, `TECHNICAL_DECISIONS.md`, `../CODING_STANDARDS.md`, `../../../BUSINESS_RULES.md`, and affected source specifications.

## Principles

- One authoritative business rule implementation.
- Explicit dependencies/state; no hidden mutable globals in new modules.
- UI -> application/domain -> Repository -> Provider direction.
- No DOM in services; no direct storage in UI/services.
- CompetitionId scope and authorization at every hosted boundary.
- Version/idempotency/audit for mutations; no silent result conflict.
- Backward-compatible, versioned, reversible migration.
- Tests and operational drills before cutover.

## Coding standards

Follow `../CODING_STANDARDS.md`: target modules <=400 lines; focused functions; kebab-case files except established contracts; PascalCase services; camelCase functions; structured errors; no duplicated rules; document public contracts and side effects. Existing explicitly named files remain unchanged unless approved.

## Branch strategy

Until team/release tooling is approved:

- Protected `main` is recommended for releasable state (current repository may still use `master`; renaming requires explicit repository decision).
- Short-lived branches: `docs/<topic>`, `test/<topic>`, `refactor/<topic>`, `fix/<topic>`, `feat/<topic>`.
- Rebase/update before review; no long-lived divergence.
- One concern per PR; architecture/storage/rule changes are not mixed.
- Releases use annotated tags and release notes after CI/acceptance.
- Emergency fixes branch from production tag and receive retrospective tests/review.

## Commit format

`<type>(<scope>): <imperative summary>` using `docs`, `test`, `refactor`, `fix`, `feat`, `chore`, or `build`.

Examples: `test(storage): characterize missing local state`; `refactor(storage): delegate save through repository`. Prefer <=72-character summary. Body records why, risks, validation, rollback, and decision/issue IDs. Never mix formatting and behavior.

## Testing expectations

1. Syntax/static checks for changed JavaScript.
2. Unit tests for pure rules/services.
3. Fixture/round-trip tests for state, JSON, XML, package, and migrations.
4. Provider contract tests with in-memory/fault doubles.
5. Integration tests for Repository/API/SQL transactions.
6. Route smoke and print visual validation for UI changes.
7. Multi-device/outage/restart/security/recovery tests for Safe Mode.

Official results require blank, scratch, penalty, tie, incomplete team, missing prelim, advancement, conflict, idempotency, and restart coverage. A production cutover needs rollback evidence and reconciliation.

## Sprint workflow

1. Confirm Product Owner scope and relevant decision statuses.
2. Read master and affected source documents/business rules.
3. Record baseline hashes/tests/state fixtures.
4. Define acceptance criteria, risks, rollback, and prohibited changes.
5. Implement smallest bounded change; no opportunistic feature/refactor.
6. Run proportional tests plus preserved validation procedures (`node --check app.js`, storage smoke tests while applicable).
7. Verify git diff contains only intended files; update docs/changelog/decision index.
8. Review, address findings, obtain product/operations sign-off where needed.
9. Merge through protected branch/CI; monitor and retain rollback.

## Code review checklist

- Scope/behavior matches approval and decision status.
- Dependency direction and module boundaries are respected.
- No direct UI/service localStorage, IndexedDB, fetch, or SQL.
- No DOM/browser API inside business services.
- Competition scope, authorization, validation, version, idempotency, audit, and deletion policy are correct.
- Official results cannot be silently overwritten.
- No duplicate rule or derived-field divergence.
- Error, offline, partial failure, restart, and rollback paths are covered.
- Tests fail before/fix after where applicable; fixtures and visual checks pass.
- State/XML/API/database compatibility and migration/rollback are documented.
- Sensitive data, credentials, logs, backups, and retention follow policy.
- Master/source docs, changelog, and decisions are updated incrementally.

## Documentation rules

Master documents consolidate direction; source documents retain detail/evidence. Update only affected documents. Significant architecture changes require a stable CTO decision. Rule changes update `BUSINESS_RULES.md`; state changes update `STATE_SCHEMA.md`; storage/sync changes update migration/protocol/schema documents.

## Contributor start checklist

- Run app syntax and storage smoke tests.
- Inspect git status; preserve user changes.
- Understand current static behavior and localStorage/XML compatibility.
- Identify module owner and forbidden dependencies.
- Confirm whether proposal depends on CTO-001–007; they remain Proposed.
- Ask before expanding scope or changing official competition behavior.

