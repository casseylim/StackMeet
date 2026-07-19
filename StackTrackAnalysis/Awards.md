# Awards

## Purpose
Translate completed competition results into award quantities, recipient lists, and presentation-ready outputs.

## Observed Behaviour
No award-specific screen was located in this read-only pass. StackMeet's awards planner has a known characterized defect that must be corrected before operational reliance.

## Competition Workflow
Configure places/items → confirm final results → generate award plan → review quantities and recipients → print/export distribution lists.

## Business Rules
- Awards derive from approved result/placement data, not manual duplicate entries.
- Competition settings define places and award items by category.
- Relay awards may require a configurable number of physical recipient units per team.
- Special and standard divisions remain distinct where configured.

## Operator Workflow
Choose award configuration → preview totals and recipients → correct source data if necessary → export or print.

## User Experience Observations
The planner should identify missing results and show why an expected group has no awards, rather than silently omitting it.

## Data Model Recommendations
`AwardConfiguration`, `AwardPlan`, `AwardPlanLine`, source result references, and approval/audit metadata.

## Suggested SQL-native StackMeet Implementation
Generate award plans as reproducible projections from a result snapshot; do not use browser-only calculated state as the authoritative source.

## Possible Improvements over StackTrack
Include stock reconciliation, packing lists by division, and an approval checkpoint before award printing.
