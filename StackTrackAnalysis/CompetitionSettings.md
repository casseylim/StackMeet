# Competition Settings

## Purpose
Define the operational context that all competition modules share.

## Observed Behaviour
An authorized, read-only settings screen is available and presents a single Save action with competition configuration controls, including competition type, rounds, and other operational options. Field-level save effects were not exercised. StackMeet requirements establish settings as competition-specific, not global.

## Competition Workflow
Create/select competition → set name, venue, date, events, age mode, and division options → save → regenerate affected derived views.

## Business Rules
- Settings belong to exactly one Competition.
- Age mode is either actual age on competition date or year-born only.
- Separate Divisions by Gender affects Special Stackers only.
- Saving settings refreshes divisions, registration, dashboard, and sidebar without a browser reload.

## Operator Workflow
Open Competition Settings → adjust consistent dropdown controls → save once → see confirmation and refreshed competition views.

## User Experience Observations
Controls should use one visual pattern, expose the currently selected competition, and avoid development branding.

## Data Model Recommendations
Versioned `CompetitionSettings` owned by `Competition`; store explicit settings values, not UI state. Record `UpdatedAtUtc` and actor where available.

## Suggested SQL-native StackMeet Implementation
Persist settings through a competition service that emits a refresh event/projection update consumed by dashboard and registration.

## Possible Improvements over StackTrack
Show downstream impact before save (for example, number of divisions/teams affected) and provide a lightweight settings audit trail.
