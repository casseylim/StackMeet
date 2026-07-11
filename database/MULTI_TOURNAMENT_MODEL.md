# Multi-Tournament Model

The same web app should serve many competitions.

The competition ID typed on the login screen is not the same as every table's database ID.

## Two IDs

| Purpose | Field | Example |
| --- | --- | --- |
| User-facing tournament login code | `competitions.public_code` | `4257` |
| Internal relational database key | `competitions.id` | `1` |

Users type the public code. The app looks up the internal ID, then every query filters by that internal competition ID.

## Login Flow

```text
Name: Cassey
Competition ID: 4257
Password: ********
```

Backend flow:

1. Find competition by `public_code`.
2. Find active access code for that competition.
3. Compare password hash.
4. Create or update a `users` row for the entered name.
5. Create a `user_sessions` row.
6. Store session with `competition_id`, `user_id`, and `access_level`.

## Query Pattern

Every tournament page should filter by competition:

```sql
SELECT *
FROM stackers
WHERE competition_id = :competition_id
ORDER BY display_name;
```

Never load stackers, results, teams, or divisions without filtering by `competition_id`.

## Why Not Use Public Code Everywhere?

Public codes are for humans and URLs.

Internal numeric IDs are better for:

- Joins
- Indexes
- Renaming/changing external competition codes later
- Preventing accidental cross-tournament data mixing

## Suggested URLs Later

```text
/login
/competitions/4257/dashboard
/competitions/4257/stackers
/competitions/4257/competition/prelims
/competitions/4257/leaderboard
```

The backend should convert `4257` to the internal `competition_id` before querying.

## Tournament Stage Variations

The app should read active stages from `competition_stages`.

Do not hard-code prelims as required.

Valid examples:

- Competition `4257`: prelims + finals.
- Competition `5001`: finals only.
- Competition `5002`: prelims only, no finals.
- Competition `5003`: SOC showcase only.

Page menus and result entry screens should show only enabled stages for that competition.
