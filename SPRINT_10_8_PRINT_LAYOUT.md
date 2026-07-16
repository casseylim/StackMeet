# Sprint 10.8 - Preliminary Timesheet Print Layout

## Target

Two complete preliminary timesheets on one A4 portrait page at Chrome print scale 100%, with readable print text and no browser-scale adjustment. This applies to Individual, Doubles, and Timed Relay preliminary sheets.

## Standard layout reference

The supplied compact two-per-page Doubles and Relay Timesheets are the visual reference: a short identity header, compact attempt grid, concise judge instructions, and clear writing space in each half-page. Individual sheets retain their three-event workflow; Doubles and Relay sheets use their configured preliminary events.

## Before and after measurements

| Measure | Before | After |
| --- | ---: | ---: |
| Sheet allocation | 138 mm | 132 mm |
| Two-sheet allocation | 276 mm | 264 mm |
| A4 height | 297 mm | 297 mm |
| Approximate printable height with 10 mm page margins | 277 mm | 277 mm |
| Remaining tolerance | 1 mm | 13 mm |

The 132 mm target is approximately 48% of the 277 mm print area. It also leaves approximately 7.6 mm tolerance when Chrome applies its typical default margins of about 12.7 mm per edge.

## Layout changes

- Reduced the print-only header gap, label spacing, identity heading, and ID size.
- Reduced event table header and cell padding.
- Reduced event row height from 19 mm to 17 mm.
- Removed the Best Time write-in column from the shared preliminary template.
- Uses the recovered horizontal space for wider Attempt 1, Attempt 2, and Attempt 3 columns.
- Restored one compact `□ Best` control below the writing line in every attempt cell.
- Replaced the instruction block with the concise record / tick / scratch / blank workflow.
- Reduced notes and judge-signoff spacing.
- Reduced sheet padding and removed the near-zero page tolerance that caused second-sheet clipping.

## Scope

`app.js` now renders actual preliminary sheets for the existing Doubles and Relay print buttons, using completed Doubles teams and ready Relay teams respectively. `styles.css` provides the shared compact print layout. No registration, scoring, results, award, report, relay-rule, SQL, API, or printing entry-point behavior changed.

## Before and after / print verification

Before: five columns (Event, three attempts, and a Best Time write-in field) required the judge to rewrite the winning time.

After: four columns (Event and three attempts); each attempt has its own writing line and `□ Best` checkbox below it. This applies identically to Individual, Doubles, and Timed Relay preliminaries. The shared 132 mm allocation remains unchanged, preserving the two-up A4 portrait geometry.

The supplied standard Doubles and Relay Timesheet screenshots were used as visual references. Hosted Chrome print-preview screenshots remain the final operational check because the local sandbox could not produce a headless Chromium PDF artifact.
