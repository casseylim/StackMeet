# Sprint 10.6 - Relay Team Business Rules

## Scope

Relay Team Setup is a competition-scoped workflow. It is available when the current competition has one or more Timed Relay events or one or more Head-to-Head events. Division-grouping controls do not determine module availability.

## Team model

- A team belongs to the current competition and contains registered stackers only.
- Team members are selected from existing stacker records; the same stacker cannot be selected twice in a team.
- A team supports Members 1-4 and Optional Members 5-6. The maximum is six.
- Timed Relay Division and Head-to-Head Division are separate values. Each defaults from the shared competition age and division service, then may be set independently by the operator.
- A team has a name, optional coordinator details, and optional location.

## Derived status

| Members | Status before competition start | Competition eligibility |
| --- | --- | --- |
| 0 | Draft | Not eligible |
| 1-3 | Incomplete | Not eligible |
| 4-6 | Ready | Eligible |
| Any, after the competition start date | Locked | Membership and divisions cannot be edited; four-to-six-member teams retain the existing eligibility threshold |

Status is never manually edited. Draft and Incomplete teams can be saved before the competition begins. Incomplete teams display the minimum-four-members rule through their status and are excluded by the existing relay eligibility check.

## Workflow

1. Configure relay events for the competition.
2. Configure the competition's division and age-calculation rules.
3. Register stackers.
4. Create relay teams using registered stackers.
5. Complete four to six member teams before competition operation.
6. Use only eligible teams in the existing relay competition flow.

## Validation examples

- Timed Relay only: Relay Team Setup is visible.
- Head-to-Head only: Relay Team Setup is visible.
- No relay events: Relay Team Setup is hidden.
- Three members: Incomplete and not eligible.
- Four, five, or six members: Ready and eligible before the competition starts.
- Timed Relay Division `10U` and Head-to-Head Division `11U` remain independent.

## Compatibility

Existing relay records retain their prior division as their Timed Relay Division. The current result, report, printing, SQL, Competition API, and Stacker API flows are not redesigned by this sprint.
