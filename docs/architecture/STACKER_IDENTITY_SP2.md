# NADITrack Stacker Identity v1 — SP-2 Historical Linking

Status: implementation candidate

## Purpose

SP-2 links existing and historical competition-scoped `Stacker` rows to permanent NADITrack Sport Stacking identities without rewriting historical registration snapshots and without introducing automatic identity merges.

The safety principle remains:

> A competition Stacker is an entry. A NADITrack Stacker is a person.

SP-2 builds on the reviewed SP-0B matcher, SP-0C resolution policy, and SP-1 transactional persistence boundary instead of creating a second matching or write path.

## Two-stage backfill workflow

Historical linking is intentionally split into two different operations.

### 1. Discovery

`StackerIdentityBackfillService.DiscoverAsync` is read-only. It inventories unlinked competition Stackers, recomputes candidate matches using the current permanent identity data, and returns a bounded review list.

Discovery never creates identities and never creates links.

The report classifies each unlinked Stacker as:

- `ReviewRequired` — one or more Strong/Possible candidates exist;
- `NoCandidates` — no current identity candidate was found.

There is deliberately no `AutoLink` classification. A single Strong candidate remains review-required because WSSA ID, name + birth date, email, and phone are evidence rather than merge authority.

Discovery can be scoped to one competition or run across all competitions. Results are bounded to at most 500 entries per call to avoid accidental unbounded historical processing.

Candidate summaries intentionally omit email and phone values even though those values can contribute to matching evidence. The report exposes only the minimum review attributes currently needed for operator disambiguation: NADITrack ID, display name, birth date, country, club, WSSA reference, match strength, and evidence labels.

### 2. Apply one decision

`StackerIdentityBackfillService.ApplyAsync` accepts one competition Stacker at a time.

Before any write, it reloads the Stacker and permanent identities and recomputes the current match result. A previously generated discovery report therefore cannot be treated as durable authority after data changes.

The recomputed match result is passed through `StackerIdentityResolutionPolicy` (SP-0C). Only an `Approved` resolution reaches `StackerIdentityPersistenceService` (SP-1).

This preserves the existing rules:

- an explicit valid existing NADITrack ID is authoritative;
- an invalid/unknown explicit NADITrack ID fails closed and cannot fall through to Create New;
- Strong/Possible candidates require explicit operator confirmation;
- Possible/manual links require a review note;
- Create New in the presence of candidates requires the separate duplicate override plus note;
- already-linked competition Stackers are surfaced as `AlreadyLinked` and are not rewritten;
- each durable apply remains protected by SP-1's serializable transaction and database uniqueness constraints.

## Historical Create New

An unlinked historical Stacker with no candidate may deliberately create a permanent NADITrack identity. This is not an automatic backfill operation: the caller must explicitly choose `CreateNew`, after which SP-0C and SP-1 validate and persist the decision.

If candidates exist, Create New remains blocked unless the reviewed duplicate-override confirmation and note are supplied.

## Historical snapshot integrity

SP-2 creates identity links only. It does not rewrite historical competition registration fields such as name, club, country, division, paid/check-in state, or StackerCode.

A later permanent-profile correction must not retroactively alter what was recorded for an older competition.

## Scope deliberately excluded from this slice

SP-2 does not yet:

- expose a public or organizer HTTP backfill endpoint;
- add a frontend bulk-backfill screen;
- auto-link unique Strong candidates;
- auto-create permanent identities for all unmatched historical Stackers;
- rewrite competition Stacker snapshots;
- introduce WSSA-ID uniqueness;
- publish athlete profiles or private attributes;
- aggregate career results;
- apply any migration or change to production.

The implementation is a backend review/apply foundation plus LocalDB and static regression coverage. A later integration slice can add an authorized operator workflow without weakening these rules.
