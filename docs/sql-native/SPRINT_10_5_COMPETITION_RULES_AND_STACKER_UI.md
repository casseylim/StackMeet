# Sprint 10.5 Final Competition Rules, Settings Persistence and Stacker UI

## Relay Team Setup rule

Relay Team Setup is visible whenever `Timed Relay` or `Head To Head` contains one or more selected events. It is hidden only when both event groups are empty. The Relay module's business behavior is unchanged.

## Competition-scoped age calculation

Age Calculation is a normal Competition Settings dropdown:

- **Actual Age on Competition Date**: full DOB calculation, including whether the birthday has occurred.
- **Year Born Only**: `competition year - birth year`.

The mode is persisted through the existing state API under `competition-{CompetitionId}-settings`, rather than the legacy global state. The global legacy save explicitly omits the field. On reopening the selected SQL competition, the scoped setting is loaded before the SQL stacker list is refreshed.

For DOB `2015-07-20` and competition date `2026-07-15`:

| Mode | Age |
| --- | --- |
| Actual Age on Competition Date | 10 |
| Year Born Only | 11 |

The shared `ageOnCompetitionDate` helper is the only calculation path used by registration, stacker display, division assignment, dashboard counts, doubles, relays, results, and awards consumers.

## Persistence verification

Using the selected Competition `7`, both `actual` and `yearBorn` values were saved to the competition-scoped key and read successfully by two independent requests. The original selected-competition setting was restored after the check.

## Individual Stacker list

- Added Age after Name.
- Removed the Time Sheet header and row Print buttons.
- Retained printing through its existing non-list workflows.
- Kept ID fixed-width/top-aligned.
- Enlarged and left-aligned Name; wrapping continues at the left edge.
- Kept all multi-line cells top-aligned with consistent row padding.

## Screenshots

![Final stacker list](/C:/Users/clim/OneDrive%20-%20Golden%20Palm%20Tree%20Resort%20%26%20Spa%20Sdn%20Bhd/Project/StackMeet/screenshots/sprint_10_5_final_stackers_list.png)

![Age calculation dropdown](/C:/Users/clim/OneDrive%20-%20Golden%20Palm%20Tree%20Resort%20%26%20Spa%20Sdn%20Bhd/Project/StackMeet/screenshots/sprint_10_5_final_age_dropdown.png)

## Rollback

1. Restore the prior `app.js`, `index.html`, and `styles.css` from the previous IIS package.
2. The scoped `competition-{CompetitionId}-settings` state entry is additive; leaving it in place is harmless after frontend rollback.
3. No SQL migrations, API changes, or XML changes were made in this sprint.
