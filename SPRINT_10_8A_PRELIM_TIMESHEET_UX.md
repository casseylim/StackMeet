# Sprint 10.8A - Preliminary Timesheet UX Improvement

## Scope

This print-only refinement applies the same shared preliminary-timesheet component to Individual, Doubles, and Timed Relay sheets. Finals sheets, result entry, scoring, registration, awards, reports, SQL, and APIs are unchanged.

## Before and after

| Area | Before | After |
| --- | --- | --- |
| Table columns | Event, Attempt 1-3, Best Time | Event, Attempt 1-3 |
| Judge action | Rewrites the fastest time in a separate cell | Ticks `Best` below the fastest valid attempt |
| Scratch | `999` recorded in an attempt | Unchanged; it must not receive a Best tick |
| Two-up print allocation | 132 mm per sheet | 132 mm per sheet |

The Best Time write-in column is removed. Every attempt cell now has a writing line and one compact `□ Best` control beneath it. The existing 17 mm print row height is retained, so the writing area and two-sheet A4 portrait allocation are preserved.

## Judge workflow

1. Record Attempts 1, 2, and 3.
2. Tick `Best` only for the fastest valid attempt.
3. Record `999` for a scratch; do not tick it as Best.
4. Leave all attempt fields blank if the competitor or team did not compete.

This avoids copying the winning time into a fourth location and reduces transcription risk.

## Print standard

- A4 portrait
- Chrome scale: 100%
- Default margins
- Two preliminary sheets per page

The common 132 mm sheet allocation totals 264 mm per pair, leaving 13 mm inside the application's 277 mm A4 printable area with 10 mm page margins.

## Visual verification

The supplied Doubles and Relay preliminary-sheet screenshots remain the visual reference for the compact two-up StackMeet standard. A hosted Chrome print-preview check remains required for final physical-print confirmation because the local sandbox could not create a headless Chromium PDF artifact.
