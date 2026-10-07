# Personal Career Profile operations

These operations mutate data and require separate production authorization. Test using a generated LocalDB database. Confirm identity ownership and publication approval; competition registration or a matching name does not establish consent. Publication applies to the entire permanent identity and every eligible linked competition.

## Read-only review

All management routes below use `/api/admin/stacker-identities` and inherit existing system-admin account-session or admin-key middleware. Ordinary accounts, competition sessions and anonymous callers cannot operate them. Never put credentials in URLs, evidence or source control.

- `GET candidates?competitionId=<numeric ID>&take=100`: bounded unlinked registration/candidate review. Matching evidence is a review hint; names never select a person automatically.
- `GET profiles/{NadiTrackId}`: current publication flag, public path and link/stacker IDs, including private identities within this admin boundary.

Responses omit DOB, email, phone, external membership ID and full domain entity graphs. Permanent IDs are `NDT-` identifiers, not competition numeric IDs or participant codes.

## Separate linking and publication

`POST links` accepts exactly one registration:

```json
{
  "stackerId": 123,
  "requestedAction": 2,
  "explicitNadiTrackId": null,
  "selectedNadiTrackId": null,
  "candidateConfirmed": false,
  "createNewOverrideConfirmed": false,
  "resolutionNote": "Reviewed synthetic test participant; no duplicate candidates"
}
```

Action `2` creates a new private identity and its link. Action `1` links an existing identity: supply its explicitly reviewed `explicitNadiTrackId` and `selectedNadiTrackId`. The existing policy requires candidate confirmations; creation despite candidates requires a distinct override confirmation and note. Matching is recomputed before persistence. New identities are private; linking never changes an existing identity's flag.

Success returns `linkId`, `stackerId`, `nadiTrackId`, `isPublicProfile`, `identityCreated` and `profilePath`. Keep this receipt for rollback. Duplicate/repeated links, stale decisions and concurrency conflicts return 409; nonexistent stackers return 404. Refresh and review rather than automatically retrying.

After private verification and approval, use `PUT profiles/{NadiTrackId}/publication`:

```json
{ "isPublicProfile": true, "reason": "Explicit publication approval reference" }
```

The boolean is required. Set false to withdraw public access. Each successful transition writes AuditLog in the same serializable transaction. Actor comes from the validated server session; admin-key operations may have a null actor and retain IP auditing.

## Public request flow

`GET /Stackers/{NadiTrackId}` validates syntax and serves the generic static profile shell. Its script fetches `GET /api/public/stackers/{NadiTrackId}`. Malformed, unknown and private identities receive safe API 404; public identities receive only the public DTO. A valid private/unknown ID can still serve the generic shell, which contains no identity data. Page/API are non-cacheable; page has noindex/nofollow.

Only explicitly linked registrations in publicly listed Closed/Archived competitions, or those with ArchivedAt set, contribute. Normal Active and private competitions are excluded. Current persisted Individual Prelims/Finals valid attempts plus penalties determine bests; historical revisions are not replayed. Tournament history reduces each competition/event to its best current performance. Governed Finals/team projections keep their additional certification/publication gates. No aggregation rule was relaxed for Active competition 18.

## Rollback without deleting history

Restore the previous publication flag first. For a previously private controlled test, unpublish and verify API 404. Do not blindly unpublish a previously public shared identity: the flag affects all its eligible appearances.

`POST links/{capturedLinkId}/unlink` requires:

```json
{ "stackerId": 123, "nadiTrackId": "NDT-XXXXXXX", "reason": "Controlled test rollback" }
```

All three identifiers must match. Mismatch returns 409; missing/repeated removal returns 404. Only that StackerIdentityLink is removed. The permanent identity, other links, Stacker snapshots, CompetitionResult history and ranking data remain. AuditLog commits atomically with unlink. Retaining an unlinked private identity is intentional, not a dangling foreign key. No identity-deletion API is added. Profiles query current data; no materialized cache/index cleanup is required beyond normal relational indexes.

Unique indexes enforce NadiTrackId uniqueness and one link per Stacker; NO_ACTION FKs reject dangling links. Serializable transactions cover matching, persistence and auditing; persistence rechecks candidates under the transaction. Explicit reviewed override remains possible for distinct people sharing evidence. Real-world person uniqueness cannot be established solely from names or registration fields.

## Governed CLI compatibility

`tools/StackerIdentityActivation` is dry-run by default, using existing user-secrets/environment connection settings. Never run execution mode accidentally. It retains expected display name, operator note and exact target phrase `ACTIVATE PUBLIC PROFILE <competition-id>/<stacker-code>`.

Create/link now requires `--execute` **without** `--public`. Combining them for a new link fails before writing. Review the private identity, then a separate existing-link invocation with `--execute --public` can publish and verify. Existing-link execution without `--public` changes nothing. Both write paths are audited transactionally. Use the admin API for unpublish/unlink.

## Release requirement

No schema migration is required. A separately authorized governed application release must deploy the corrected DLL while preserving production web.config and appsettings. Validate malformed/unknown 404 and controlled privacy/rollback after deployment before activation. Building this branch or CLI does not update production.
