# Registration

## Purpose
Create and maintain a competition's participant roster; it is the source for divisions, teams, results, and paperwork.

## Observed Behaviour
Authorized read-only inspection confirms a dedicated Individuals registration area and separate Doubles and Relay registration modules. The roster-edit controls and validation rules were not exercised.

## Competition Workflow
Competition settings → register stackers → calculate age/division → form teams → print and enter results.

## Business Rules
- A stacker belongs to one competition context.
- Public IDs use the Individual sequence `1.x`.
- DOB, gender, Special status, and competition date drive division assignment.
- Changes must recalculate derived presentation data without rewriting historical results.

## Operator Workflow
Search list → add or edit stacker → save → return to roster; use an explicit Add New control so the form does not obscure the list.

## User Experience Observations
Fast lookup, visible current division, and immediate list refresh reduce registration-desk errors. Long names must remain left aligned and readable.

## Data Model Recommendations
`Competition`, `Stacker`, and immutable result references by stacker ID; unique `(CompetitionId, StackerCode)`; UTC audit timestamps.

## Suggested SQL-native StackMeet Implementation
Use Competition-scoped CRUD endpoints and persist raw registration facts. Calculate age/division in one shared service; project DTOs to the UI.

## Possible Improvements over StackTrack
Inline duplicate-code feedback, keyboard-first create/edit, clear save status, and automatic dashboard/sidebar count refresh.
