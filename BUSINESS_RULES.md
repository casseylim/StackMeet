# StackMeet Business Rules

This document records the current known rules. Any rule change should include test cases and tournament-owner approval.

## Participant identifiers

- Individual IDs use the `1.x` series.
- Doubles IDs use the `2.x` series.
- Relay IDs use the `3.x` series.
- Compact result entry resolves values such as `12`, `23`, and `34` to `1.2`, `2.3`, and `3.4`.

## Age and divisions

- Age is calculated using the competition start date.
- Standard divisions are generated from configured combined, male, and female age cutoffs.
- Special stackers are assigned through the configured Special/SS division rules.
- A custom division overrides the generated division when present.

## Doubles

- Normal Doubles use two registered stackers.
- Child/Parent Doubles can use a registered partner or an external parent/guardian name.
- Conflicting active team membership is prevented/removed by the current workflow.
- Team division is generated from member age/special status unless a custom division is supplied.

## Relay

- Relay team names are compulsory and unique.
- A Relay may contain up to six registered members.
- A Relay is complete when it has at least four members.
- A stacker cannot remain assigned to conflicting Relay teams.

## Results

- Individual events are 3-3-3, 3-6-3, and Cycle when enabled.
- Doubles uses Cycle when enabled.
- Timed Relay uses 3-6-3 when enabled.
- `999` represents a scratch in compact competition entry.
- Blank indicates that the participant did not compete/has no recorded time.
- Finals qualification uses configured advancement limits and prelim ranking.
- Final judge sheets place the slowest qualifier first.
- The fastest valid final result wins.
- Ties are resolved using second-best and then third-best attempts.

## Awards planning

- Individual awards equal planned individual divisions × enabled individual events × awarded places.
- Doubles awards equal planned Doubles categories × Cycle × awarded places × two awards per team.
- Relay awards equal planned Relay categories × 3-6-3 × awarded places × configured awards per team.
- Overall awards are planned separately.
- Awards planning is based on configured competition structure, not current registration counts.

## Interface and paperwork

- Event-group menus are hidden when their group is disabled.
- Generated paperwork remains visible on screen for review until replaced or navigation changes.
- XML export is the current portable tournament backup.

