# Printing

## Purpose
Produce judge-ready paperwork that reduces transcription risk during live competition operations.

## Observed Behaviour
User-provided preliminary-sheet examples show a compact two-up A4 portrait format with identity header, event/attempt grid, and concise instructions. Authorized read-only inspection also confirms a Paperwork area with packet, preliminary/final timesheet, SOC timesheet, and Head-to-Head bracket categories. Generated documents were not opened.

## Competition Workflow
Finalize entries/teams → print preliminary sheets → judges record attempts → operators enter results → print finals, reports, and awards outputs.

## Business Rules
- Printing is competition-scoped and uses current participant/team identity.
- Individual, Doubles, and Timed Relay preliminary sheets use one print standard.
- A4 portrait, Chrome/Edge 100%, default margins, exactly two preliminary sheets per page.
- Prelim sheets use Attempt 1-3 with a Best checkbox; Best Time write-in column is removed.
- Finals printing is a separate workflow and remains unchanged by preliminary standards.

## Operator Workflow
Choose paperwork type → preview range/team list → print with normal browser settings → use printed ID to drive result lookup.

## User Experience Observations
The most valuable print design is one that keeps writing space, avoids repeat transcription, and has stable pagination across normal browsers.

## Data Model Recommendations
Print jobs should be generated from competition DTO projections and record template/version plus selected scope when operational traceability is needed.

## Suggested SQL-native StackMeet Implementation
Keep rendering in a dedicated print view/component and source data from server read models; retain the shared preliminary-sheet template.

## Possible Improvements over StackTrack
Add print preflight (missing teams, incomplete records), batch IDs/QR verification where required, and a printable audit manifest.
