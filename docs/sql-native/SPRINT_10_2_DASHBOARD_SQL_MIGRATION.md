# Sprint 10.2 Dashboard SQL Migration

## Scope

Dashboard and Registration only. The selected SQL Competition (`Id=7`) supplies Dashboard competition presentation; the SQL Stacker list supplies all registration-count and division-count calculations.

## SQL-backed Dashboard widgets

- Hero event title: `Competition.CompetitionName`
- Tournament snapshot name, dates, and venue: selected `Competition`
- Sidebar Stackers badge: SQL Stacker count
- Dashboard Stackers metric and male/female sub-count: SQL Stacker list
- Division badge counts: recalculated from the SQL-loaded Stacker list whenever it refreshes
- Special division generation remains outside this Dashboard scope; its optional gender split is defined by Sprint 10.3.

The existing Fastest Official, notifications, and other non-listed widgets remain unchanged.

## Polling

While `#dashboard` is open, the client requests the SQL Stacker list every five seconds. On a successful response it updates only the Dashboard SQL widgets and sidebar badge. The timer is cleared whenever the user navigates away from Dashboard. Registration still refreshes on entering its own screen and retains its manual Refresh button.

## Boundaries

No changes were made to Results, Relays, Awards, Reports, or Printing. The legacy CompetitionState persistence continues to exclude stackers, so no JSON save can overwrite the SQL list.
