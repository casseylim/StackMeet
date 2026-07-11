# StackMeet Database Structure

The current website can keep using XML as the portable tournament database. When you are ready for a full hosted website, this folder gives the SQL structure to migrate into.

## Files

- `schema.sql`: main relational database schema.
- `seed.sql`: starter lookup rows for event groups, events, and stages.
- `XML_TO_SQL_MAPPING.md`: how the current XML fields move into SQL tables.

## Core Tables

- `competitions`: one row per tournament, including StackTrack setup such as KBS logo, prelim time mode, paperless mode, and advance counts.
- `competition_access_codes`: password levels for each tournament login.
- `stackers`: individual competitors.
- `teams` and `team_members`: doubles, relay, and head-to-head teams.
- `divisions`: award groups such as `7-8 M`, `10-12 F`, `SS 11-14 L1`.
- `division_cutoffs`: selected age cutoff settings used to auto-generate divisions.
- `stackers.is_special`: marks Special / Disability stackers so their generated division uses the SS cutoff group.
- `events`, `event_groups`, `competition_events`: enabled event setup.
- `stages` and `competition_stages`: prelims, finals, SOC, and HTH configuration per tournament.
- `time_sheets`: printable/data-entry sheet IDs.
- `results` and `result_attempts`: official times and raw attempts.
- `leaderboard_settings`: display configuration.
- `users` and `user_sessions`: access and activity.
- `notifications`: imported registration/sync messages.
- `paperwork_jobs`: generated packets, badges, brackets, and sheets.

## Why This Structure

The design supports one tournament now, but also supports many tournaments later.

`competitions.public_code` is the competition ID typed by the user, for example `4257`.

`competitions.id` is the internal database ID used by other tables.

It also keeps public IDs like `1.1` and `2.1` for time sheets while using internal numeric IDs for reliable database relationships.

## Login Model

The same web app can open different tournaments by competition ID:

1. User enters name.
2. User enters competition ID, such as `4257`.
3. App finds `competitions.public_code = '4257'`.
4. App checks the password against `competition_access_codes`.
5. App creates a `users` row/session for activity tracking.

This means passwords can be different per tournament and per access level.

## Stage Model

Do not assume every tournament has prelims and finals.

Use `competition_stages` to decide which stages are active:

- Final-only tournament: enable `finals`, disable `prelims`.
- Prelim-only tournament: enable `prelims`, disable `finals`.
- Normal tournament: enable both `prelims` and `finals`.
- Special tournament: enable `soc` or `hth` only when needed.

Each active stage can also set its own `round_count`.

## Division Cutoff Model

Divisions are generated from selected cutoff ages:

- `combined`: male and female together.
- `male`: male-only age groups.
- `female`: female-only age groups.

Example:

- `combined` cutoff `6` -> `6 & Under Combined`
- `male` cutoff `8` -> `8 & Under Male`
- next `male` cutoff `10` -> `9-10 Male`

If a stacker has a custom division, use the custom division instead of the generated division.

## Suggested First SQL Engine

Use SQLite for early local testing. It is simple, file-based, and easy to replace later.

When the hosted site is ready, migrate to PostgreSQL or MySQL.
