# Competition Synchronization Event Model

## Decision

StackMeet uses three synchronization scopes:

| Scope | Examples | Client action |
| --- | --- | --- |
| Record | Individual result, final result, stacker, Doubles team, Relay team | Apply or reload only the affected record |
| Collection | Generated divisions, qualification, awards | Refresh the affected module |
| Global | Settings, division rules, age rules, imports, reset, close | Queue while editing and require a broad refresh |

The current `ResultsUpdated` event remains a compatibility event. New work should evolve it toward `CompetitionChanged` with a small payload:

```json
{
  "competitionKey": "TEST",
  "revision": 153,
  "scope": "record",
  "type": "ResultUpdated",
  "participantType": "Individual",
  "participantId": "1.112",
  "event": "3-3-3",
  "stage": "Prelim"
}
```

## Client Rules

- Ignore events whose competition key does not match the active session.
- Ignore events at or below the client revision.
- Record events update only the affected record when a read endpoint exists; otherwise reload the affected collection.
- Collection events refresh only that module.
- Global events queue while any editor is active and show a refresh-pending message.
- A queued global refresh is applied after save or explicit refresh.
- Polling remains as a fallback for disconnected SignalR clients.

## Conflict Boundaries

Editing one Individual does not block another Individual. Editing one result does not block a result for another participant or event. Global operations remain protected because they can invalidate many records.

The first implementation may continue using full-state reads as a compatibility fallback. Record-level API reads and server revisions should be added before removing that fallback.

## Migration Order

1. Add a server-side competition revision and atomic increment on state changes.
2. Add `CompetitionChanged` alongside `ResultsUpdated`.
3. Add change scope and record identity to save requests.
4. Implement record and collection refresh handlers.
5. Retain full-state reload for global events and unknown event types.
6. Add two-client characterization tests for independent and conflicting edits.
