# Reports

## Purpose
Provide operational visibility, compliance exports, and post-event records without forcing officials to reconstruct data manually.

## Observed Behaviour
Authorized read-only inspection confirms separate Competition Results and Admin Reports areas. Report selection and generated outputs were not exercised.

## Competition Workflow
Configure and run competition → review registrations/results → filter a report → print or export → retain as an official record.

## Business Rules
- Reports are scoped to the current Competition.
- Filters must not change source records.
- Participant type, division, event, location, and status are common reporting dimensions.
- Result reports must distinguish prelim/final stages and valid/scratch/no-result states.

## Operator Workflow
Choose report type → apply filters → review row count and totals → print/export a snapshot.

## User Experience Observations
Operators need predictable columns, filter summaries, readable print layouts, and export formats suitable for officials rather than raw database data.

## Data Model Recommendations
SQL views/read models for roster, teams, results, division counts, qualification, and award outputs; report-request audit metadata when needed.

## Suggested SQL-native StackMeet Implementation
Query competition-scoped read models with parameterized filters. Generate CSV/XLSX/PDF from the same DTO projection used for screen display.

## StackMeet Finals Reporting Addition
Observed StackTrack behavior remains limited to separate Competition Results and Admin Reports; finals report details were not exercised. StackMeet adds auditable qualification snapshots, finals-only All-Around eligibility, Top Performance filters, and organization placement credits. These are StackMeet-exclusive additions, not inferred StackTrack behavior.

## Possible Improvements over StackTrack
Saved report presets, print-preview validation, scheduled exports, and a “data as of” timestamp on every output.
