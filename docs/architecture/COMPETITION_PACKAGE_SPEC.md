# Competition Package Specification

## Purpose

A Competition Package is the portable, versioned snapshot used to move one competition between online and offline operation. It is the boundary between storage providers; division, results, awards, reports, and import/export rules remain the same in both modes.

## Package identity

Every package must contain:

| Field | Rule |
|---|---|
| `packageFormat` | Stable identifier, currently `StackMeet.CompetitionPackage`. |
| `packageVersion` | Integer schema version. The first implementation is version `1`. |
| `competitionKey` | Immutable online competition key. |
| `competitionId` | Online database identifier when available. |
| `competitionCode` | Public competition code. |
| `exportedAt` | ISO-8601 UTC timestamp. |
| `exportedBy` | User or system actor when known. |
| `sourceMode` | `online` or `offline`. |
| `sourceRevision` | Online state revision or ETag when available. |
| `contentHash` | SHA-256 hash of canonical package content, excluding the hash field itself. |

The package identity must be checked before import. A package for competition `A` must never be imported into competition `B`.

## Package contents

```text
CompetitionPackage
├── manifest
│   ├── packageFormat
│   ├── packageVersion
│   ├── competition identity
│   ├── source mode/revision
│   └── integrity metadata
├── competition
│   ├── metadata
│   ├── settings
│   ├── event configuration
│   ├── division settings
│   └── generated divisions
├── participants
│   ├── stackers
│   ├── doubles
│   └── relays
├── competitionData
│   ├── preliminary results
│   ├── final qualification snapshots
│   ├── finals
│   ├── awards configuration
│   └── notifications/relevant audit data
└── metadata
    ├── export summary
    └── package warnings
```

The package must preserve the current business fields, including `division`, `customDivision`, `standardDivision`, Special status, gender, team membership, results, finals, and qualification snapshots.

## Modes

### Online download

1. Authenticate the user.
2. Read the authoritative competition state from the API.
3. Build and validate the package.
4. Record the source revision and content hash.
5. Download the package for offline use.

### Offline operation

1. Verify package format, identity, schema version, and hash.
2. Copy the package into an offline working area.
3. Run the existing competition workflows against the local package.
4. Record local changes and export a new package when finished.
5. Keep the original downloaded package as the restore point.

### Online upload

1. Authenticate the user.
2. Validate package identity, schema, hash, references, and business rules.
3. Compare `sourceRevision` with the current online revision.
4. Create an automatic backup of the current online state.
5. Show an upload preview and warnings.
6. Require explicit confirmation.
7. Store the package as the new authoritative competition state.
8. Record the upload actor, time, source revision, new revision, and hash.

## Validation rules

- Required manifest fields must be present.
- `packageVersion` must be supported.
- `competitionKey` must match the selected destination competition.
- All participant, team, result, and qualification references must resolve.
- Individual Special M/F values are permitted only for Individual divisions.
- Doubles, Child/Parent Doubles, and Relay divisions must not contain M/F suffixes unless explicitly custom and approved by the current rules.
- Custom divisions must remain unchanged during validation.
- Normal imported standard divisions must remain preserved.
- Imported Special divisions must follow the current recalculation setting.
- Duplicate IDs and malformed result records must be rejected.
- The content hash must match the package body.

## Conflict policy

If the online revision differs from `sourceRevision`, upload must stop before replacement. The first implementation should provide:

- Current online revision.
- Offline package revision.
- Export timestamps.
- Counts of stackers, Doubles, Relays, results, and finals.
- A backup/download option.
- Explicit choices to cancel or replace after review.

Silent last-write-wins behavior is not acceptable for competition data.

## Recovery policy

Every accepted upload must preserve the previous online state as a timestamped backup. A failed upload must leave the online state unchanged. An interrupted offline session must reopen the last locally saved package and must not erase the original download.

## Implementation gates

1. Add a package schema and canonical serializer.
2. Add package validation tests using representative small, medium, large, and edge-case competitions.
3. Add download tests.
4. Add offline open/save tests.
5. Add upload preview and backup tests.
6. Add stale-revision conflict tests.
7. Pilot with a non-critical competition before enabling the workflow for live events.

The machine-readable contract for the first package version is `docs/architecture/competition-package-v1.schema.json`.
