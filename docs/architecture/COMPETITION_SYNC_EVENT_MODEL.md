# Competition Synchronization Event Model

## Decision

NADITrack uses authoritative synchronization events based on data ownership.

| Event | Source | Meaning | Consumers |
| --- | --- | --- | --- |
| `CompetitionChanged` | CompetitionState save | Competition configuration or global state changed | Authenticated application and public Results portal |
| `ResultsChanged` | SQL CompetitionResult mutation | SQL-native competition result data changed | Authenticated application and public Results portal |

The former `ResultsUpdated` compatibility event has been retired.

New synchronization work must use the authoritative event that owns the changed data.

## CompetitionChanged

Source:

    CompetitionState save

Example payload:

    {
      "competitionKey": "TEST",
      "revision": 153,
      "scope": "global",
      "type": "CompetitionChanged",
      "updatedAt": "2026-08-26T00:00:00Z"
    }

Authenticated application behavior:

    reload latest CompetitionState

Public Results portal behavior:

    refresh the combined public Results projection

The public portal refreshes the combined projection because competition metadata,
settings, divisions, teams, branding, publication status, or other state-derived
information may have changed.

## ResultsChanged

Source:

    SQL CompetitionResult mutation

Example payload:

    {
      "competitionId": 1,
      "competitionKey": "TEST",
      "revision": 54,
      "scope": "results",
      "type": "ResultsChanged"
    }

Authenticated application behavior:

    refresh SQL-native CompetitionResults

Public Results portal behavior:

    refresh the combined public Results projection

The public portal refreshes the combined projection because rankings,
qualification positions, standings, and medal calculations depend on the latest
SQL result data.

## Client Rules

- Ignore events for another competition.
- Ignore stale result revisions where revision-aware handling is available.
- `CompetitionChanged` owns CompetitionState and global configuration refresh.
- `ResultsChanged` owns SQL CompetitionResult refresh.
- The authenticated application keeps those refresh paths separate.
- The public Results portal may use one combined public refresh for either event
  because its public endpoint is a read-only combined projection.
- Polling remains a fallback when SignalR is disconnected.
- SignalR clients must rejoin the competition group after reconnect.

## Conflict Boundaries

Editing one SQL result must not block an unrelated participant or event.

SQL result writes use:

- per-result revisions
- competition `ResultsRevision`
- transactional result mutation

CompetitionState writes use:

- `StateRevision`
- ETag
- `If-Match`
- optimistic concurrency
- serializable state-save locking

Competition configuration concurrency and SQL result concurrency therefore remain
separate authoritative boundaries.

## Current Architecture

Competition state synchronization:

    CompetitionState save
            |
            v
    CompetitionChanged
            |
            +---------------------------+
            |                           |
            v                           v
    Authenticated app           Public Results portal
    reload CompetitionState     refresh combined public view

SQL result synchronization:

    SQL CompetitionResult save
            |
            v
    ResultsChanged
            |
            +---------------------------+
            |                           |
            v                           v
    Authenticated app           Public Results portal
    refresh SQL results         refresh combined public view

## Legacy Event Retirement

`ResultsUpdated` previously existed as a compatibility event while competition
results and synchronization behavior were still coupled to legacy CompetitionState
JSON.

It was retired after:

1. SQL `CompetitionResult` became authoritative for results.
2. The authenticated application adopted `ResultsChanged`.
3. CompetitionState synchronization adopted `CompetitionChanged`.
4. The public Results portal migrated to listen to both authoritative events.

No new source, client, test, or documentation should depend on `ResultsUpdated`.

## Design Rule

When new synchronized data is added, emit and consume the event belonging to the
authoritative data owner rather than creating another generic full-refresh event.

This keeps configuration synchronization and SQL result synchronization
independently authoritative while allowing the public Results portal to remain
simple, live, and read-only.
