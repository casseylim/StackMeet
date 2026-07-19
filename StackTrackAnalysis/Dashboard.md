# Competition Dashboard

## Purpose
Give officials a current competition snapshot without manual refresh or spreadsheet reconciliation.

## Observed Behaviour
Authorized read-only inspection confirms a competition dashboard/landing page with aggregate competition cards and module navigation. Live refresh behaviour was not exercised. StackMeet requirements establish a dashboard with automatic refresh while it is open.

## Competition Workflow
Select competition → configure settings → register/edit entries and teams → observe live counts/divisions → conduct events and review progress.

## Business Rules
- Hero title, competition name, stacker counts, sidebar count, and division badges reflect the current Competition.
- Updates after settings save or roster changes must appear automatically.
- Dashboard state is a projection, not a separate source of truth.

## Operator Workflow
Keep dashboard open on a second display/browser; perform registration or settings changes elsewhere; verify counts update within the polling interval.

## User Experience Observations
A concise snapshot works best: event identity, counts, attention items, and timestamp/refresh indication. Avoid requiring page reloads.

## Data Model Recommendations
Competition-scoped dashboard read model: settings summary, entry counts, division counts, team readiness, and update version/time.

## Suggested SQL-native StackMeet Implementation
Expose a read-only dashboard endpoint/projection and poll every five seconds only while the dashboard is open; refresh client components from the returned version.

## Possible Improvements over StackTrack
Show incomplete relay teams, missing prelim results, and data-quality exceptions as actionable dashboard items.
