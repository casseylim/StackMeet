# NADITrack Stacker Identity v1 — SP-3A Career Profile Read Model

Status: implementation foundation only; not publicly routed

## Purpose

SP-3A introduces the first read-only career projection for a permanent NADITrack Sport Stacker identity. It proves that linked competition snapshots and SQL-authoritative competition results can be aggregated safely into a privacy-minimized public career model without changing registration, results entry, or historical snapshots.

This phase deliberately stops before HTTP exposure or frontend presentation.

## Public visibility boundary

A permanent identity is eligible for the public projection only when `SportStackerIdentity.IsPublicProfile` is explicitly `true`.

Malformed, unknown, and private NADITrack IDs all return no public profile. Public callers therefore do not gain a private-profile existence oracle.

The public model contains only:

- permanent NADITrack ID;
- display name;
- country;
- optional club and region;
- finalized public competition count and date range;
- event personal bests with competition/stage provenance.

The model deliberately excludes exact birth date, email, phone, gender, WSSA/external ID, payment/check-in state, and other registration-only information.

## Finalized competition boundary

Career appearances and PB candidates come only from competitions that are:

1. `IsPubliclyListed = true`; and
2. finalized by `Status = Closed`, `Status = Archived`, or an existing `ArchivedAt` timestamp.

Draft/Active competitions do not contribute. This prevents live/provisional entries from silently becoming career records before a competition is finalized.

A finalized public linked competition counts as a career appearance even when the athlete has no result rows in that competition.

## Personal-best boundary

SP-3A personal bests cover only `Individual` Sport Stacking results. Doubles and Timed Relay are team result domains and are excluded from individual PB calculation.

Supported events remain the existing Sport Stacking events:

- `3-3-3`;
- `3-6-3`;
- `Cycle`.

Both Prelims and Finals may contribute because either can contain the athlete's best finalized performance.

The timing calculation mirrors the existing `BestResultEngine.js` semantics:

- valid attempt: greater than `0` and less than `999`;
- raw best time: lowest valid attempt;
- applicable penalty: greater than `0` and less than `999`;
- official PB time: raw best time + applicable penalty.

Scratch-only, missing, unsupported, or malformed result rows are ignored rather than breaking the public profile.

If multiple rows produce the same official time, deterministic selection uses competition date and then the persisted result ID as tie breakers.

## Historical linkage

Career aggregation follows the reviewed identity chain:

`SportStackerIdentity -> StackerIdentityLink -> competition Stacker snapshot -> CompetitionResult`

The historical `StackerCode` stored for each competition remains the participant key used to resolve that competition's Individual result rows. No historical registration snapshot is rewritten.

Multiple linked Stackers from different competitions may therefore contribute to one permanent NADITrack career profile.

## Read-only implementation

`SportStackerCareerProfileService.GetPublicAsync` uses no-tracking queries and performs no `SaveChanges`, inserts, updates, deletes, migrations, or state mutations.

SP-3A adds no schema change. It uses the SP-1 identity/link schema plus the existing SQL-authoritative Competition and CompetitionResult tables.

## Deliberately excluded from SP-3A

SP-3A includes **no public HTTP endpoint** and **no frontend route**.

It also includes:

- no profile publishing/editing workflow;
- no athlete ownership/account-claim workflow;
- no minor/guardian consent workflow;
- no profile photo;
- no tournament-history presentation beyond PB provenance;
- no medals/rankings/awards aggregation;
- no Doubles/Relay career statistics;
- no historical data rewrite;
- no production migration;
- **no production deployment**.

Those boundaries must be reviewed separately before a WSSA-style public profile page is exposed.

## Validation

The SP-3A LocalDB characterization proves that:

- private identities do not resolve publicly;
- only finalized publicly-listed linked competitions count;
- Active and non-public competitions are excluded even when they contain faster times;
- an appearance with no result still counts as a finalized career appearance;
- PBs use valid attempts plus applicable penalties;
- scratch/malformed rows do not displace valid PBs;
- team result types cannot become individual PBs;
- private identity attributes are absent from the public contract.
