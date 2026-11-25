# ENG-04A — Storage authority and recovery baseline (draft)

Source candidate: `eb3b61f4faa3f99e89b851e0f5860f0b09313a8c`, stacked on ENG-03G (PR #100). [CI run 45621439877](https://github.com/casseylim/StackMeet/actions/runs/45621439877) succeeded on that exact push SHA; all three jobs succeeded. This document is a read-only source assessment, not a database inventory, migration, performance measurement or owner acceptance. Paths below are relative to backend/StackMeet.Api unless stated.

## 1. Current authority matrix

| Aggregate / field group | Current authority and write boundary | Legacy / projection / recovery boundary |
|---|---|---|
| Competition code, key, name, venue, dates, lifecycle, public listing, activity, results revision | SQL Competition; competition admin routes; key/code uniqueness | JSON settings also contain display name and dates; public response exposes SQL competition fields and separate JSON settings. Explicitly resolve discrepancies before any consolidation; backup both. |
| Individual participant code and competition association | SQL Stacker (competition ID + StackerCode unique); StackersController create/update/delete | PublicResultsController chooses entire SQL roster if nonempty, else JSON stackers; not a row-by-row merge. Operator refresh replaces roster with SQL response, including empty. Empty SQL roster is ambiguous without migration evidence. |
| Participant names, WSSA ID, gender, birth date, country, club, region, email, phone, custom division, paid, checked-in, special flag and timestamps | SQL Stacker; code immutable through normal update | Public projection limits fields; public division uses custom division or derived age + JSON division settings; JSON historical roster is a fallback only for public reads. Preserve private SQL fields, never publish them in reconciliation evidence. |
| Doubles and relay ID, membership, name, type, readiness and division | CompetitionState.JsonData; state ETag write; CompetitionTeamResultIntegrityService | JSON team graph is validated against SQL members and existing SQL team results. No SQL-native team entity in current DbContext. Preserve stable team IDs and parent/partner representation; do not synthesize parent identities. |
| Result identity, stage, participant type/code, event, attempts, penalty, revision, timestamps and actor | SQL CompetitionResult; logical unique index; per-row revision and Competition.ResultsRevision; results batch API | AttemptsJson is a SQL column, not legacy state. Public results always project SQL result rows, even when empty. Operator runtime is refreshed from SQL; normal state save clears legacy stackers/results. Legacy result copies are not an automatic authoritative fallback. |
| Qualification selections and settings | JSON settings, division settings, events and finalQualificationSnapshots; state write with ETag | Draft/Approved/Superseded snapshots are operator artifacts (app.js generate/approve); XML v2 represents snapshots. Distinguish these from certified Finals ranking source evidence; never regenerate approved selections in a reconciliation pass. |
| Overall / All-Around ranking | Derived from eligible Finals results under owner rule ENG-03E0; Prelims report remains distinct | Never use preliminary attempts as fallback for missing Finals. Preliminary seeding/qualification references are not Overall scores. Verify live display separately from immutable historical certification. |
| Permanent person identity and competition link | SQL SportStackerIdentity and StackerIdentityLink; approved identity persistence transaction | Competition Stacker remains its own historical snapshot. Identity link changes must not rewrite registration evidence. Public identity and career eligibility are separately governed. |
| Certified historical Finals placement / publication | SQL FinalsRankingGovernance raw SQL persistence; immutable SnapshotJson and SnapshotSha256, rule/schema version, source state/results revisions and capture metadata | Snapshot captures state and sorted SQL Finals rows. Verify digest over original UTF-8 JSON bytes, never reserialize/recertify or silently apply new ranking rules. Public career views apply additional identity and publication gates. |
| Asset identity, type, filenames, content type, byte count, SHA-256 and audit fields | SQL CompetitionAsset; unique competition/type | Bytes live under configured CompetitionAssetsPath (or content-root competition-assets). Upload saves file before DB metadata, deletes old file after DB save; failure cleanup is best effort. SQL backup alone is insufficient. |
| Accounts, roles, assignments, tokens, audit and protected settings | SQL AppUser, AppRole, CompetitionUser, AppUserToken, AuditLog, AppSetting | Configuration and protection keys required separately for decryption and runtime. Do not include secrets, tokens, private fields, or config content in CI evidence. |
| Browser state and portable exchange | Runtime projection, provider revision and local cache; XML v2 / competition package exchange | Not a replacement for authoritative SQL backup, identity links, authorization, protected keys, asset bytes or certified evidence. Import is an explicit write path, not automatic remediation. |

Source anchors: Models/Competition.cs, Models/Stacker.cs, Models/CompetitionResult.cs, Models/CompetitionAsset.cs, Data/StackMeetDbContext.cs, Controllers/StackersController.cs, Controllers/CompetitionStateController.cs, Controllers/CompetitionResultsController.cs, Controllers/PublicResultsController.cs, Controllers/CompetitionAssetsController.cs, Services/CompetitionTeamResultIntegrityService.cs, Services/CompetitionAssetStorage.cs, Activities/SportStacking/Identity/StackerIdentityPersistenceService.cs, Activities/SportStacking/Ranking/FinalsRankingGovernanceService.cs, wwwroot/js/storage/Repository.js and wwwroot/app.js. The public precedence is explicit in PublicResultsController.Get; the operator SQL refresh is in app.js refreshSqlStackers / applySqlResultSnapshot; Repository.legacyStateForSave clears both arrays on normal snapshot saves. The state controller persists the submitted object, so this browser convention alone is not proof that all callers omit those arrays.

## 2. Read-only discrepancy classification (design, not executed)

One competition at a time, consistent point-in-time reads on an isolated restored or synthetic database first. Record schema/version, state and result revisions, provenance and capture timestamp. If consistent reads cannot be established, label the result inconclusive rather than compare moving snapshots. No mutation, publication, repair, automatic migration, or JSON fallback expansion.

| Code | Condition | Required disposition |
|---|---|---|
| AUTH-01 | SQL roster empty, JSON roster nonempty | Legacy-only candidate or stale copy; operator/public divergence possible. Investigate owner and migration evidence. |
| AUTH-02 | SQL and JSON roster coexist | Compare normalized codes and overlapping public fields only; classify matching copy, missing code, duplicate, or value conflict. SQL precedence is whole-roster, not union. |
| AUTH-03 | Legacy result copies exist | Compare logical key (competition, stage, type, participant, event), attempt values and penalty; classify duplicates, SQL-only, JSON-only and conflict. Do not resurrect deleted rows or score from JSON. |
| AUTH-04 | Team or result references missing/invalid participants | Validate with existing readiness/reference services; distinguish cross-competition codes, duplicate team IDs and changed composition. Preserve legitimate parent/partner exceptions. |
| AUTH-05 | Qualification snapshot refers to invalid/missing member/team/event or mismatched source | Report state and approval status; do not recalculate or supersede. Historical qualification need not equal current live standings. |
| AUTH-06 | Certified evidence missing, invalid digest, unsupported version or inconsistent capture metadata | Block historical evidence acceptance; do not alter bytes or certification metadata. Older valid capture revisions can legitimately differ from current live revision. |
| AUTH-07 | Missing referenced asset, byte count/hash mismatch or unreferenced file | Report missing/corrupt/orphan separately; no automatic deletion or repair. |
| AUTH-08 | SQL competition metadata conflicts with JSON display settings | Show field categories and disagreement counts; owner determines intentional overrides. |
| AUTH-09 | Missing or malformed state; duplicate/ambiguous key; unknown schema | Fail closed for that competition; do not coerce malformed state to empty. |

Expected differences are not automatically defects: SQL-only normal modern rows, valid historical snapshot revision differences, derived division labels, and deliberately private fields. Report severity, reason code, counts and aggregate outcome (pass/review/inconclusive). Keep raw records private. A read-only SQL principal and no file write/delete permissions are required for eventual production execution, separately approved.

## 3. Transaction and performance baseline

ENG-01/02 remediations already serialize state, participant and result changes on a competition row and enforce result preconditions; do not reintroduce prior audit races. Current state PUT validates JSON and readiness before lock, then obtains Competition and CompetitionState locks under Serializable, checks lifecycle, queries member codes in chunks of 500, validates team references against existing results, and only then compares expected StateRevision. This is a candidate for measuring stale-request wasted work, not authorization to change error precedence. Result batch uses competition UPDLOCK/ROWLOCK, validates dataset revision, team state and SQL membership before updates, increments ResultsRevision once for changed batch and sends notifications after commit. Stacker CRUD also locks the Competition row. Verify against concurrent lifecycle/registration/result/state fixtures.

Retain current indexes: unique CompetitionCode, CompetitionKey, CompetitionState.CompetitionKey, (CompetitionId, StackerCode), result PublicId and (CompetitionId, Stage, ParticipantType, ParticipantCode, EventCode), and (CompetitionId, AssetType). The public results path has five collection/record queries (competition, assets, state, stackers, results); no coordinated snapshot transaction is shown here. Measure consistency, logical reads, query plans, p50/p95 and transaction/lock duration separately on matched synthetic sizes before proposing indexes or shorter lock duration. Do not claim production latency improvement from source inspection. Public refresh optimization belongs in ENG-07 unless integrity requires an earlier change.

## 4. Recovery units and acceptance

| Unit | Required evidence in isolated rehearsal |
|---|---|
| SQL database | Approved full/log backup strategy; restore to disposable instance, CHECKDB, schema/migration ledger, counts and referential invariants, state/results revisions, identity links, governance and publication flags. |
| Asset bytes | Same logical recovery point as SQL; manifest of file size and SHA-256 matched to SQL metadata; distinguish orphan files from missing referenced assets. |
| Protected settings and key material | Authorized private backup/restore of required configuration and protection keys; validate decryption and authentication without printing secrets. |
| Application release | Exact reviewed binary/frontend inventory; compatible database schema; coherent rollback artifacts. Production IIS web.config must be excluded from upload/sync/rollback, backed up securely and have identical pre/post SHA-256. Stop if any mismatch or preservation cannot be assured. |
| Competition exchange | Test XML and package round-trip for roster, teams, attempts, qualification and divisions; report differences from full recovery coverage. |
| Certified snapshots | Byte-for-byte original digest and rule/schema metadata, unchanged historical placement and publication behavior. |

Recovery point coordination must prevent SQL metadata from referring to asset bytes from a different backup window. Owner must approve RPO/RTO, retention, access and restore procedure. Prior ENG-00 synthetic SQL backup/restore is evidence only for that drill; combined full-stack recovery remains unproven. Do not overwrite live production during a rehearsal.

## 5. Proposed ENG-04 work sequence

- ENG-04A: this source authority matrix, discrepancy taxonomy, recovery boundaries and baseline. Documentation only.
- ENG-04B: characterize existing SQL/JSON precedence and edge fixtures (legacy-only, modern SQL-only, mixed, empty/deleted, malformed, qualification and certified digest) before changes.
- ENG-04C: implement a read-only reconciliation candidate against disposable fixtures, with privacy-safe output and no repair mode.
- ENG-04D: measure query and lock durations under matched contention; only then propose a tested optimization preserving observable validation precedence.
- ENG-04E: coordinated recovery rehearsal and recovery evidence on isolated infrastructure.
- ENG-04F: consolidate results, known exceptions and owner gates. Any SQL team/schema migration is a separately reviewed proposal only after reconciliation and recovery evidence.

This split is a proposed engineering sequence under ENG-01_TO_ENG-09_IMPLEMENTATION_PLANS.md, not a preapproved migration scope. Outstanding ENG-03 browser, accessibility, historical Finals display and release acceptance gates carry forward unchanged. No application behavior, schema, migration, deployment workflow, historical data, or web.config changes in ENG-04A. No master merge, no production access. CI on the new draft is pending until independently verified.
