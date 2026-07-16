# Finals

## Purpose
Run qualified finalists in a controlled, ranked event stage after preliminary results are complete.

## Observed Behaviour
Authorized read-only inspection confirms a Finals area with Missing Divisions and Details views. Qualification, seeding, and save behaviour were not exercised. StackMeet currently has final-sheet and placement workflow behaviour.

## Competition Workflow
Complete prelims → calculate qualifiers by event/division → generate final sheet → enter final attempts → calculate places → publish/print outputs.

## Business Rules
- Qualification is based on valid prelim result rules, including tie-break handling.
- A final sheet belongs to a competition, division, event, and participant type.
- Final result entry must not silently alter preliminary history.
- No-attempt finalists remain unplaced; valid attempts determine placing.

## Operator Workflow
Select/find final sheet → verify finalists and prelim seeds → record attempts → review computed ranks → save and print.

## User Experience Observations
Showing prelim seed, participant identity, current best, and place together supports judge verification. Finals need a deliberate print/entry handoff.

## Data Model Recommendations
`QualificationRun`, `FinalSheet`, `FinalSheetEntry`, and final `Result` records with links to qualifying prelim data.

## Suggested SQL-native StackMeet Implementation
Generate sheets from an explicit qualification snapshot so later edits are auditable; calculate placements in a domain service.

## Possible Improvements over StackTrack
Show qualification exceptions before locking the sheet and provide a clear recalculation/approval action rather than implicit regeneration.
