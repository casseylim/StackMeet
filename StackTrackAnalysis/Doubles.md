# Doubles

## Purpose
Pair eligible registered stackers into a competition entry that can receive Doubles events and results.

## Observed Behaviour
Authorized read-only inspection confirms a dedicated Doubles area with completed, pending, and needs-partner queues plus Save/Delete actions. Team-formation validation was not exercised.

## Competition Workflow
Register stackers → form Doubles team → calculate team division → print prelim sheet → enter prelims → qualify and run finals.

## Business Rules
- A team belongs to the current Competition.
- Doubles IDs use an independent `2.x` sequence.
- Members are selected from registered stackers; no arbitrary typed participant should enter results.
- A normal team requires the applicable partners; Child/Parent supports its defined parent workflow.
- A result references the team ID, not duplicated names.

## Operator Workflow
Find two stackers → verify availability and division → save team → review status and correct conflicts before competition.

## User Experience Observations
Availability indicators prevent accidental double assignment. Team names must display both partners consistently in setup, entry, print, and results.

## Data Model Recommendations
`DoublesTeam`, `DoublesTeamMember`, team status, division projection, and unique membership constraints scoped to a Competition.

## Suggested SQL-native StackMeet Implementation
Validate membership server-side inside a transaction, expose a team DTO with member display data, and retain result history against the team key.

## Possible Improvements over StackTrack
Show division and eligibility as the team is assembled; offer one-click conflict resolution with an audit message.
