# Relay

## Purpose
Build competition-scoped relay teams that are safe to use in lane assignment, results, reports, and printing.

## Observed Behaviour
Authorized read-only inspection confirms a dedicated Relay Teams area with completed, pending, and needs-team queues, details, paperwork links, and Save/Delete actions. The rules below remain confirmed StackMeet requirements, not a claim about StackTrack implementation.

## Competition Workflow
Enable a Timed Relay or Head-to-Head event → configure relay divisions → form teams → verify Ready status → conduct prelims/results.

## Business Rules
- Relay setup is available when any Timed Relay or Head-to-Head event exists.
- Team size: 0 Draft; 1-3 Incomplete; 4-6 Ready; Locked after competition start.
- Only Ready teams may compete.
- Members must be registered in the current Competition and cannot be duplicated within a team.
- Timed Relay and Head-to-Head divisions remain independent.
- Division derives through the shared age/division service; current relay age rule uses the oldest member.

## Operator Workflow
Name a team → select 4-6 members → review auto-derived status/divisions → correct incomplete teams before event operations.

## User Experience Observations
Separate Ready, Incomplete, and Draft filters make operational risk visible. A member count and explanatory status prevent guesswork.

## Data Model Recommendations
`RelayTeam`, ordered `RelayTeamMember`, division projections by event category, and derived status; all owned by `CompetitionId`.

## Suggested SQL-native StackMeet Implementation
Use server-side validation for membership and readiness. Expose a single relay-team read model to setup, entry, print, and reporting.

## Possible Improvements over StackTrack
Surface a clear “minimum four required” callout and detect unassigned eligible stackers before the competition begins.
