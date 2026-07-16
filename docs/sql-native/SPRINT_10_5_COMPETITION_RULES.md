# Sprint 10.5 Competition Rules and Operator Workflow

## Relay Team Setup visibility

Relay Team Setup now appears when either `Timed Relay` or `Head To Head` has one or more selected events. It remains hidden only when both groups are empty. This is a navigation-availability change only; Relay data and behavior are unchanged.

| Timed Relay | Head To Head | Relay Team Setup |
| --- | --- | --- |
| Any event selected | Any state | Visible |
| Empty | Any event selected | Visible |
| Empty | Empty | Hidden |

## Competition age calculation

Competition Settings now contains the persisted **Age Calculation** mode:

- **Actual Age on Competition Date**: age is reduced by one when the birthday has not occurred by the competition date.
- **Year Born Only**: `competition year - birth year`.

The single `ageOnCompetitionDate` helper applies the selected mode across Registration, division assignment, doubles, relays, and any consumer that uses a stacker age. Saving Settings updates the mode, recalculates runtime stacker ages/divisions, and refreshes current UI state without reloading the browser.

Example for DOB `2014-07-12` on competition date `2026-07-11`:

| Mode | Age |
| --- | --- |
| Actual Age | 11 |
| Year Born Only | 12 |

## Stacker list polish

The Stacker table has a fixed, top-aligned ID column, a wider left-aligned Name column, consistent row padding, and safe wrapping from the left edge for long names. This is CSS-only; no list business behavior changed.

## Screenshots

![Stacker list](</C:/Users/clim/OneDrive - Golden Palm Tree Resort & Spa Sdn Bhd/Project/StackMeet/screenshots/sprint_10_5_stackers_list.png>)

![Competition rules](</C:/Users/clim/OneDrive - Golden Palm Tree Resort & Spa Sdn Bhd/Project/StackMeet/screenshots/sprint_10_5_competition_rules.png>)
