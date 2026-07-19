# XML To SQL Mapping

This keeps XML useful now while making the later SQL migration straightforward.

## Main Objects

| XML path | SQL table |
| --- | --- |
| `/stackmeet/settings` | `competitions` |
| `/stackmeet/leaderboard` | `leaderboard_settings` |
| `/stackmeet/events/group/event` | `competition_events` |
| stage settings such as prelim/final/SOC | `competition_stages` |
| `/stackmeet/divisionSettings/group/age` | `division_cutoffs` |
| `/stackmeet/divisions/division` | `divisions` |
| `/stackmeet/stackers/stacker` | `stackers`, `organizations` |
| `/stackmeet/doubles/team` | `teams`, `team_members` |
| `/stackmeet/results/result` | `results`, `result_attempts` |
| `/stackmeet/notifications/notification` | `notifications` |
| `/stackmeet/users/user` | `users`, `user_sessions` |

## ID Strategy

The XML/app currently uses public codes such as `1.1`, `2.1`, and `r1`.

In SQL, keep both:

- Internal numeric `id`: used for joins and performance.
- Public code fields like `bib_code`, `team_code`, and `sheet_code`: shown on time sheets and screens.

The tournament login ID goes into `competitions.public_code`. For example, StackTrack competition ID `4257` becomes:

```sql
INSERT INTO competitions (public_code, name, competition_type, start_date, end_date)
VALUES ('4257', 'WSSA NS Sport Stacking Centre', 'Sanctioned', '2026-07-05', '2026-07-05');
```

All tournament-specific rows then use the internal `competitions.id`, not the public code, as their `competition_id`.

Access passwords are not stored in the current XML export. In SQL they belong in `competition_access_codes`, stored as password hashes, not plain text.

## Stage Flexibility

The app should not assume prelims are required.

Examples:

| Tournament format | `competition_stages` setup |
| --- | --- |
| Final only | `finals.enabled = true`, `prelims.enabled = false` |
| Prelims only | `prelims.enabled = true`, `finals.enabled = false` |
| Prelims + finals | both enabled |
| SOC only | `soc.enabled = true`, others optional |

The old XML fields like `prelims` and `finals` should migrate into `competition_stages.round_count`.

## Division Cutoff Migration

XML division settings store cutoff ages:

```xml
<divisionSettings>
  <group name="combined"><age>6</age></group>
  <group name="male"><age>8</age><age>10</age></group>
</divisionSettings>
```

This migrates to `division_cutoffs`.

Generated divisions then migrate to `divisions`:

- `6 & Under Combined`
- `8 & Under Male`
- `9-10 Male`

Stackers with a custom division should keep that custom value as their assigned `division_id`.

## Result Storage

Do not store attempts only as one text field.

Use:

- `results`: one row per participant, stage, event, and entry.
- `result_attempts`: one row per raw attempt.
- `results.official_time`: calculated best time plus penalty, or blank when scratched.

This makes rankings and reports much easier.

## Participant Model

Individual results point to `stackers`.

Doubles, relay, and HTH results point to `teams`.

The shared fields are:

- `participant_type`: `stacker` or `team`
- `stacker_id`: used only when participant is a stacker
- `team_id`: used only when participant is a team

## Recommended Migration Order

1. Insert lookup rows from `seed.sql`.
2. Create the competition row from XML settings.
3. Insert divisions.
4. Insert organizations found in stackers.
5. Insert stackers.
6. Insert teams and team members.
7. Insert enabled events.
8. Insert results and attempts.
9. Insert leaderboard settings, notifications, and users.

## Later Backend API Shape

Good first API routes:

- `GET /competitions/:id/dashboard`
- `GET /competitions/:id/stackers`
- `POST /competitions/:id/stackers`
- `GET /competitions/:id/teams`
- `POST /competitions/:id/results`
- `GET /competitions/:id/reports/division-counts`
- `GET /competitions/:id/leaderboard`
- `POST /competitions/:id/import-xml`
- `GET /competitions/:id/export-xml`
