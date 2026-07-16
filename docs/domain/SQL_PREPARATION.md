# SQL Server Preparation

**Design only.** No DDL is created in Sprint 6. Use `uniqueidentifier`/ULID-style internal IDs (decision pending), SQL Server `rowversion`, UTC audit timestamps, and `CompetitionId` scope.

| Entity / table | Primary and natural key | Essential indexes | Audit / soft delete / version | Synchronization needs |
|---|---|---|---|---|
| Competition / `competitions` | PK `competition_id`; natural `public_code` unique | unique public code | created/updated/by; soft delete; rowversion | authority, checkpoint, package version |
| Settings / stages / events | PK internal IDs; natural `(competition_id, stage/event)` | enabled event/stage and category lookups | audit; soft delete for configuration; rowversion | config revision and compatibility version |
| Division / cutoff | PK `division_id`; natural `(competition_id,name)` and `(competition_id,cutoff_group,cutoff_age)` | division name, cutoff order | audit; retire rather than delete if referenced; rowversion | division revision for recalculation |
| Category | PK `category_id`; natural `(competition_id, participant_type, stage, event, division)` | planned award/final grouping | audit; soft delete; rowversion | configuration replication |
| Stacker | PK `stacker_id`; natural `(competition_id,bib_code)` | competition+division, display name, organization | audit; soft delete; rowversion | field-level conflict policy, tombstone |
| Guardian | PK `guardian_id`; natural optional normalized contact/name in competition | name/contact lookup | audit; soft delete; rowversion | PII protection, tombstone |
| Team / member | PK `team_id`, `team_member_id`; natural `(competition_id,team_code)`, relay name unique | competition+type; membership by stacker | audit; soft delete; rowversion | transactional membership command and conflict receipt |
| Result / attempt | PK `result_id`, `result_attempt_id`; natural participant+stage+event context | competition+stage+event; participant; unique attempt ordinal | immutable audit/correction, no hard delete, rowversion | protected writes, idempotency, conflict review |
| Final sheet / lane | PK `time_sheet_id`, `lane_id`; natural `(competition_id,sheet_code)`, `(sheet_id,lane_number)` | sheet status and event/division | audit; soft delete only before protected use; rowversion | sheet revision, assigned judge/official |
| Award plan | PK `award_plan_id`; natural competition/category/place/item | competition/category | audit; soft delete; rowversion | config revision; calculated rows are cacheable |
| Official/Judge role | PK role/assignment IDs; natural `(competition_id,user_id,role)` | actor + competition | immutable access audit; active flag; rowversion | authorization snapshot/device identity |
| Heat / entry | PK `heat_id`, `heat_entry_id`; natural competition+stage/event/sequence | schedule and participant lookup | audit; soft delete; rowversion | schedule revision/conflict policy |
| Audit Record | PK `audit_record_id`; natural operation/idempotency key | competition+time; entity reference; actor | append-only, no soft delete; version not applicable | change feed, correlation/device/operation IDs |
| Competition Package | PK `package_id`; natural `(competition_id,package_version)` | competition+created/status | immutable manifest/checksum; retain | checkpoint, schema/app compatibility, encrypted payload |

## Cross-cutting SQL rules

1. Every tenant-owned table includes non-null `competition_id` and has a leading competition-scoped index.
2. Use foreign keys for identity/reference integrity, check constraints for one-participant result shape, and services for competition rules.
3. Avoid `ON DELETE CASCADE` for official results, audit, packages, and synchronization history.
4. Commands write domain change, audit record and idempotency receipt atomically.
5. Offline synchronization uses operation ID, base rowversion, tombstone/version, actor/device, ordered checkpoint and conflict status.
