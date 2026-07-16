# Division Setup

## Purpose
Turn competition rules into usable individual and relay competition divisions.

## Observed Behaviour
An authorized, read-only Division Setup screen is available with a Save action. Its detailed cutoff controls were not changed or exercised. User-provided StackMeet requirements establish independent individual, Timed Relay, and Head-to-Head Relay cutoff panels.

## Competition Workflow
Configure age mode and cutoffs → register stackers/teams → assign divisions → review counts → use divisions in entry, results, awards, and reports.

## Business Rules
- Combined, Male, Female, and Special divisions are independent.
- Timed Relay and Head-to-Head Relay cutoff sets are independent.
- Relay division uses the oldest eligible member for the current competition rule.
- Standard division generation must not be changed by the Special gender-split option.

## Operator Workflow
Review labelled Individual Divisions and Relay Divisions sections; change a cutoff; save; confirm teams/counts refresh immediately.

## User Experience Observations
Separating relay panels from individual panels prevents operators missing a second row of configuration.

## Data Model Recommendations
`DivisionRuleSet` with `CompetitionId`, `DivisionKind`, cutoff ordering, and effective settings version; derived assignments should be recalculable.

## Suggested SQL-native StackMeet Implementation
Store rules as normalized rows or a versioned JSON rule document owned by the Competition, then calculate assignments through a shared domain service.

## Possible Improvements over StackTrack
Provide a preview table showing who moves when a cutoff changes before committing the configuration.
