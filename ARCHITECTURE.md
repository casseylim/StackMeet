# StackMeet Architecture

## Current runtime architecture

StackMeet is a static browser SPA using plain HTML, CSS, and JavaScript.

```text
index.html templates
        |
        v
      app.js <---- data/stacktrack-4257-stackers.js
        |
        +---- DOM rendering and event delegation
        +---- competition business rules
        +---- localStorage JSON state
        +---- XML import/export
        +---- browser printing and downloads
```

`index.html` provides the permanent shell and route templates. `app.js` loads state, normalizes it, mounts the active hash route, handles mutations, generates reports/paperwork, and persists the full state object. `styles.css` supplies shared screen, responsive, report, and print presentation.

## State boundary

The authoritative browser state is stored as one JSON value under `stackmeet-stacktrack-style-v1`. XML is the portable backup format. UI-session values such as selected tabs, active edit IDs, and sort state are held in top-level variables and are not persisted.

## Future architecture direction

The `database/` folder defines a multi-tournament relational model. The intended hosted direction is:

```text
Browser UI -> application/domain modules -> repository/API -> SQL database
```

Every hosted query must be scoped by an internal competition ID. Human-facing competition codes remain separate public identifiers.

## Dependency direction target

```text
Utilities and schema
    -> storage and domain rules
        -> UI modules
            -> application shell/router
```

See `ARCHITECTURE_REVIEW.md` for the full Sprint 0 analysis, dependency map, debt ranking, risks, and extraction roadmap.

