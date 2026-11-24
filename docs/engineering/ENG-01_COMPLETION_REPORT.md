# ENG-01 data integrity

Baseline de24245383a0940d4da1c369bdcd251b49868692. Draft for engineering and competition-owner acceptance.

| Path | Root cause and change | Evidence |
|---|---|---|
| Controllers/CompetitionResultsController.cs and Dtos/CompetitionResultDtos.cs | Missing revision could overwrite an existing row, and a stale edit could enter the create branch. Positive revisions now target existing rows only. Existing rows reject an unversioned upsert with 428. Explicit creates carry zero plus an observed dataset revision. Deletes require a positive row revision. A delete of an already absent valid participant result remains a successful no-op. | Two concurrent creates and two concurrent edits each yielded one commit and one 409. Stale edits and stale explicit creates could not recreate a deleted row. An old delete could not remove its replacement. |
| Controllers/CompetitionResultsController.cs | The read verifies the dataset revision after reading rows. Validation and the logical key lookup run under the existing competition write lock. The revision and result mutations commit together. | In a batch with a valid first member and stale second member, zero changes persisted and the dataset revision held. |
| Controllers/CompetitionStateController.cs and Controllers/StackersController.cs | Participant checks and lifecycle restrictions now run after acquiring the competition lock. Registration writes acquire the same lock before mutation. Closure and archive use committed row status. State audit attribution is derived from the session or maintenance channel. | Independent HTTP requests for removal and state replacement produced a legal serial outcome. Close and save produced a legal outcome. Closed normal state writes were rejected even on the maintenance channel. SQL rows recorded authenticated account and maintenance origins. |
| wwwroot/app.js and wwwroot/js/storage/ResultApi.js | Browser creates use the dataset revision. A skipped or out-of-order delta triggers a full read instead of labeling incomplete cached state current. | Delta regression covers ordinary no-extra-GET paths and refresh after a missed revision. |
| tests/CoreIntegrityIntegrationTests/Program.cs and tests/result-batch-delta-client.test.js | Added synthetic SQL-backed HTTP and browser-client regression cases at existing test entry points. | Clean Release build. LocalDB suite passed after database warmup following an initial handshake timeout. New cases are tagged ENG01 in the suite log. |

Paths in the table are relative to backend/StackMeet.Api except tests. At ENG-00 the result controller had zero mapped lines covered by the .NET collector. ENG-06A measures the combined path against that baseline. The invariants above are outcome measurements. This change has no comparative latency result.

## Compatibility and acceptance

Cached older clients can create with an omitted/null row revision when no row exists. A legacy create has no dataset token, so a late retry can still create an absent logical key. Inventory clients and approve deprecation of that fallback before requiring dataset revisions universally. An older client attempting a blind overwrite or delete receives 428 and must refresh or upgrade. A versioned retry after a commit can receive 409. Read current data to reconcile before resubmitting. A dataset revision conflict may also follow an unrelated concurrent edit because it protects the observed absence of a key.

Accept when the SQL and Node suites pass, operator clients verify refresh and retry behavior in their supported workflows, and maintenance owners acknowledge the lifecycle enforcement on ordinary writes. The concurrency scenarios establish outcomes for these synthetic pairs. They do not model production load, every lifecycle endpoint or every timing interleaving.

Rollback is a reviewed revert of this coordinated API and browser change. No database schema or stored scoring data conversion is involved. Reverting reopens the documented race risks. Use the existing governed release process for any future deployment decision.
