# Sprint 10.7A - Relay Division Configuration

## Competition-scoped configuration

Division Setup now contains two independent cutoff configurations, using the existing age-cutoff checkbox pattern:

- Timed Relay
- Head-to-Head Relay

Each selected age is the upper boundary of a relay division. For example, selecting `10`, `12`, `14`, and `18` creates the effective relay boundaries `10U`, `12U`, `14U`, and `18U`.

## RC2 team-division rule

For each relay format, StackMeet finds the oldest registered team member using the shared competition age-calculation function and assigns the first configured cutoff that includes that age.

Example:

| Team member ages | Timed Relay cutoffs | Team division |
| --- | --- | --- |
| 10, 10, 11, 12 | 10, 12, 14, 18 | 12U |

Timed Relay and Head-to-Head Relay use their own cutoff arrays. A team may therefore resolve to different divisions in the two formats.

## Recalculation

Saving Division Setup immediately recalculates the cached relay division values for all existing teams. Relay lists also derive their displayed value from the current configuration, so they refresh without changing roster data.

## Exclusions

- No 19+ youngest-member rule is implemented.
- No changes were made to registration, results, awards, reports, printing, SQL, or APIs.
- Future adult relay handling, including any youngest-member rule, must be specified in a separate sprint.
