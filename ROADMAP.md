# StackMeet Roadmap

## Phase 1 — Engineering baseline

- Add characterization tests for current business rules.
- Define a versioned state schema and recovery policy.
- Document storage, import, export, and validation contracts.
- Establish repeatable syntax and smoke-test checks.

## Phase 2 — Behavior-preserving modularization

- Extract pure utilities.
- Isolate storage and XML serialization.
- Separate translation dictionaries and services.
- Extract settings, divisions, stackers, teams, results, finals, awards, reports, printing, and leaderboard modules in dependency order.

## Phase 3 — Workflow completion

- Complete competition paperwork generators.
- Complete Head-to-Head and SOC workflows.
- Improve leaderboard behavior and operational displays.
- Expand validation and recovery tooling.

## Phase 4 — Hosted architecture

- Introduce a repository/API boundary.
- Implement the documented relational schema.
- Add competition-scoped authentication and authorization.
- Support concurrent devices, durable storage, and multi-tournament hosting.

Roadmap items are planning targets and should be promoted into a sprint only after scope, rules, tests, and acceptance criteria are agreed.

