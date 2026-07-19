# Special Stackers

## Purpose
Support Special Stacker divisions without changing standard-division competition rules.

## Observed Behaviour
The authenticated Division Setup area was inspected read-only, but Special Stacker-specific controls were not exercised or isolated; their reference behaviour remains unverified. The design reflects approved StackMeet competition rules.

## Competition Workflow
Register Special status → apply Special cutoffs → optionally split Special divisions by gender → use the resulting division everywhere.

## Business Rules
- Special status is a participant attribute.
- With gender split off: `SS 7`, `SS 8`, and similar combined divisions remain combined.
- With gender split on: only Special divisions become `SS 7 M` / `SS 7 F`.
- Standard divisions, age calculation, and registration workflow do not change.

## Operator Workflow
Select the competition-specific setting, save, then verify Special division counts and participant assignments.

## User Experience Observations
The setting needs plain language and an immediate preview because an incorrect interpretation affects many entries.

## Data Model Recommendations
Persist `IsSpecialStacker` on `Stacker` and `SeparateSpecialDivisionsByGender` on Competition settings; do not encode these rules in labels.

## Suggested SQL-native StackMeet Implementation
Have one division-assignment service accept age, gender, Special status, and current Competition settings.

## Possible Improvements over StackTrack
Add a before/after assignment summary and an exception list for incomplete demographic data.
