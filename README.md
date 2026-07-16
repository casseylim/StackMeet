# StackMeet

StackMeet is a browser-based sport stacking competition manager backed by the StackMeet online API and SQL Server.

## Current capabilities

- Tournament, event, and division setup
- Individual stacker registration and CSV import
- Normal and Child/Parent Doubles management
- Relay team management
- Individual, Doubles, and Relay prelim result entry
- Finals qualification, judge sheets, attempt entry, ranking, and tie-breaking
- Awards planning from configured divisions and events
- Competition and administrative reports
- XML backup and restore
- English, Bahasa Malaysia, and Simplified Chinese UI support

## Run locally

Host the frontend behind the same HTTPS origin as the StackMeet API, then open the application in a modern browser. The application loads `styles.css`, the bundled stacker data file, and `app.js` directly; no frontend build step is required.

Tournament data is stored through `/api/state/DEFAULT` in SQL Server. Export XML backups regularly for operational recovery.

## Project layout

- `index.html` — application shell and page templates
- `app.js` — current application state, rules, rendering, reporting, and printing
- `styles.css` — screen and print styling
- `assets/` — logos and visual assets
- `data/` — bundled/import data
- `database/` — future SQL schema, seed data, ERD, and migration notes
- `docs/` — project documentation
- `backups/` — local backup staging
- `exports/` — generated export staging
- `releases/` — packaged releases
- `screenshots/` — UI reference images
- `temp/` — temporary local work

## Engineering baseline

The current architecture is documented in `ARCHITECTURE_REVIEW.md`. Planned behavior-preserving architecture work should begin with automated characterization tests and a versioned state contract before module extraction.
