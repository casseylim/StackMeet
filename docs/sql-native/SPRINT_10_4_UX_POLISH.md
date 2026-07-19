# Sprint 10.4 Competition Settings and Registration UX Polish

## Scope

This sprint changes only the Competition Settings and Individual Stacker registration experience. SQL/API contracts, Dashboard polling, Results, Relays, Awards, Reports, and Printing are unchanged.

## Competition Settings

- The title is now **Competition Settings**.
- **Separate Divisions by Gender** is off by default.
- With the setting off, Special divisions retain their existing names.
- With it on, only Special divisions gain `M` or `F`; standard divisions are never changed.
- Saving Settings immediately recalculates runtime stacker divisions, refreshes the available division list and badge counts, and updates the sidebar/Dashboard state without a browser reload.

## Registration UX

| Before | After |
| --- | --- |
| Registration form always shown above the list. | Stackers List is shown first; the form is hidden. |
| Save action permanently occupied the screen header. | **+ Add New Stacker** opens the form when needed. |
| Edit used the inline form without an explicit list-first state. | Edit opens the populated registration form. |
| Cancel left the form visible. | Cancel hides the form and keeps the stacker list unchanged. |
| Successful save retained the form. | Successful SQL save hides the form and returns focus to Stackers List. |

## Screenshots

![Stackers List – form hidden](/C:/Users/clim/OneDrive%20-%20Golden%20Palm%20Tree%20Resort%20%26%20Spa%20Sdn%20Bhd/Project/StackMeet/screenshots/sprint_10_4_stackers_list.png)

![Competition Settings](/C:/Users/clim/OneDrive%20-%20Golden%20Palm%20Tree%20Resort%20%26%20Spa%20Sdn%20Bhd/Project/StackMeet/screenshots/sprint_10_4_competition_settings.png)

## Branding cleanup

The active UI no longer displays StackTrack Settings, Reset Demo, or the former SQL setup wording. Existing database values remain data, rather than hardcoded UI labels, and can be updated through the normal Competition workflow.
