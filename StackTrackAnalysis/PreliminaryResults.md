# Preliminary Results

## Purpose
Capture valid preliminary performance quickly and consistently, providing the basis for qualification and reporting.

## Observed Behaviour
Authorized read-only inspection confirms a Round 1 prelims area with Missing Times and Details views. Entry controls and save behaviour were not exercised. User-provided StackMeet workflows establish the current universal entry design.

## Competition Workflow
Print prelim sheets → lookup participant/team → record event values → save → refresh missing-times and result views → qualify finals.

## Business Rules
- One shared entry workflow serves Individual `1.x`, Doubles `2.x`, and Relay `3.x` IDs.
- Compact input is the public ID with its decimal removed: `115` means `1.15`; dotted input is also accepted.
- `999` means Scratch; blank means did not compete where allowed.
- Existing participant/event results load for editing and update rather than duplicate records.
- Shared result parsing retains timer precision and accepted time formats.

## Operator Workflow
Key compact/dotted ID → confirm identity/division → see Ready for Entry or Editing Existing Result → enter values → save → receive confirmation, clear details, and return focus to lookup.

## User Experience Observations
One keypad-led lookup and automatic focus reduce queue time. State labels prevent an official overwriting a record unknowingly.

## Data Model Recommendations
`Result` keyed by Competition, stage, participant type/key, and event; unique constraint prevents duplicate preliminary records; store attempts and official/penalty state separately.

## Suggested SQL-native StackMeet Implementation
Create an idempotent upsert command with server-side participant/event validation. Return an entry-state DTO and a current result projection.

## Possible Improvements over StackTrack
Support scanner input, explicit scratch button, undo window, and a visible write-through/save indicator for unreliable venues.
