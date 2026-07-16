# StackMeet Coding Standards

## Scope

These rules apply to future changes and do not authorize behavior changes by themselves.

## Modules and size

- Target maximum: **400 lines per production module**, excluding generated data and static translation dictionaries.
- A module above 400 lines requires a documented reason and follow-up issue.
- Functions should normally remain below **40 lines** and one responsibility.
- UI route modules own mounting, events, rendering, and cleanup for one route.
- Business services must not import UI modules.
- Required dependency direction: Utilities/Schema -> Storage and Domain -> UI -> Application Shell.
- Circular dependencies are prohibited.

## Naming

- Files: lowercase kebab-case, e.g. `division-service.js`.
- Classes/services: PascalCase, e.g. `DivisionService`.
- Functions/variables: camelCase.
- Booleans start with `is`, `has`, `can`, `should`, or `was`.
- Event handlers use `handle<Action>`; renderers use `render<View>`; pure builders use `build<Model>`.
- Use precise IDs (`stackerId`, `teamId`, `sheetId`) instead of ambiguous cross-boundary `id` names.
- Canonical domain labels are internal; translation occurs at presentation boundaries.

## Business rules

- A competition rule has one authoritative implementation.
- Never duplicate division, membership, scratch, qualification, placement, tie-break, or award logic in UI/report/print modules.
- Rule changes require approved acceptance criteria, fixture/test updates, and `BUSINESS_RULES.md` updates.
- During modularization, preserve exact current behavior, including known limitations.
- Business functions receive all required state/configuration explicitly.

## State and storage

- No direct localStorage access outside the storage adapter/repository.
- XML parsing/serialization belongs only to the serialization/storage boundary.
- Storage key, JSON shape, XML format, or normalization changes require a CTO decision and migration plan.
- Persisted state and UI-session state use separate documented types.
- Mutations are explicit and return a result/new state; no silent unrelated collection changes.
- Multi-collection destructive operations require validation, impact reporting, and future transactional behavior.
- State schema changes require versioning, migration tests, and compatibility documentation.

## DOM separation

- No DOM manipulation inside business services.
- Business services must not call `document`, `window`, DOM methods, `alert`, `confirm`, `window.print`, or downloads.
- UI modules convert form values into typed commands and render service results/errors.
- Reports, results, divisions, teams, and awards do not query form controls directly.
- Print document creation and browser print invocation remain separate.
- New user-facing text requires approved translation coverage.

## Globals and dependencies

- New mutable globals are prohibited.
- No hidden global state.
- Dependencies are explicit imports or constructor/function inputs.
- Cached DOM elements stay private to their UI module.
- “Utility” modules contain pure cross-domain helpers only; stateful participant/team lookups belong to domain services.

## Errors and validation

- Never silently replace corrupt operational data without documented recovery behavior.
- Validate at forms, CSV, XML, storage, and API boundaries.
- Services return structured validation issues; UI formats messages.
- Preserve participant/team reference integrity.
- Do not use exceptions for ordinary validation failures.

## Testing

- Pure business services require unit tests.
- Normalization and serialization require fixtures and round-trip tests.
- Official result tests include blank, scratch, penalty, ties, incomplete teams, missing prelims, and advancement limits.
- UI changes require route smoke tests; print changes require visual validation.
- Bug fixes should first add a failing regression test where practical.
- `node --check` is mandatory but does not replace behavioral testing.

## Security and privacy

- Never commit passwords, access codes, tokens, keys, or production credentials.
- Minimize personal registration fields in logs, fixtures, screenshots, and exports.
- Hosted queries are always scoped by internal `competition_id`.
- Authorization is server-enforced; UI hiding is not access control.

## Documentation

- Update `CHANGELOG.md` for operational/user-visible changes.
- Update `BUSINESS_RULES.md` for rule changes.
- Update `STATE_SCHEMA.md` for state changes.
- Update service/roadmap documents when boundaries or order change.
- Record significant decisions in `CTO_DECISIONS.md`.
- Public functions document inputs, outputs, errors, and side effects.
- Migrations document rollback/recovery and validation evidence.

## Commit messages

Format:

```text
<type>(<scope>): <imperative summary>
```

Types: `docs`, `test`, `refactor`, `fix`, `feat`, `chore`, `build`.

Examples:

```text
docs(architecture): document global state schema
test(finals): characterize third-attempt tie break
refactor(storage): isolate localStorage adapter
```

- Keep the summary imperative and preferably no longer than 72 characters.
- One commit represents one logical change.
- High-risk commit bodies include test/validation evidence.
- Reference a CTO decision or issue for architecture, state, XML, storage, or rule changes.
- Do not mix formatting-only and behavioral changes.

## Review checklist

- Scope matches the approved task.
- No duplicated business rule or direct storage access was introduced.
- Services do not read DOM or hidden globals.
- Tests cover the change and its high-risk paths.
- Syntax, fixtures, routes, reports, and print output are validated proportionately.
- Required documentation is current.

