# Conflict Policy

## Universal policy

Official results, attempts, scratches, finalist selection, and placements are protected. They must never be resolved silently or by last-write-wins (LWW). Server time/client time alone does not decide truth. Every conflict preserves both versions and full audit context.

| Entity | Likelihood | Auto merge | LWW | Approval | Audit requirement |
|---|---|---|---|---|---|
| Competition settings | Medium | Only disjoint non-rule display fields when base confirms no overlap | No | Tournament Director/admin for any rule/date/stage/advance change | Before/after, base/server versions, user/device, reason |
| Stackers identity/profile | Medium | Disjoint low-risk contact fields; never identity/division-driving fields without review | No for identity; possibly approved low-risk contact policy later | Admin for name, DOB, gender, special/custom division or delete | Full field diff and reference impact |
| Check-in | High across desks | Yes only monotonic `not checked-in -> checked-in` if participant exists | No blind LWW; monotonic rule | Admin to reverse check-in or resolve deleted participant | Actor/device/time and reversal reason |
| Payment | Medium | No automatic financial merge | No | Authorized finance/admin | Amount/status before/after, source, receipt/reference, reviewer |
| Doubles | Medium–High | No when membership/division conflicts; identical operations idempotent | No | Admin/TD; head judge if competition/results already depend on team | Both rosters, dependencies, affected results/sheets |
| Relay | Medium–High | No membership/name/division merge; identical operations idempotent | No | Admin/TD; head judge once active | Full membership order, dependencies and decision |
| Prelim attempts | High | Only identical attempt values/operation retry | Never | Head judge or authorized results admin | Both raw values, scratch/penalty, sheet/event, operators/devices |
| Finals attempts | Very High | Only identical retry | Never | Head judge; second-person confirmation recommended | Immutable compared values and signed resolution reason |
| Scratches | High | Only if both sides identically scratch same attempt | Never | Head judge for scratch vs time or scratch reversal | Original timer entry, both intents, reason/reviewer |
| Placements | Very High | Recalculate only after authoritative attempts resolved; do not merge stored placement | Never | Head judge approves any manual override | Inputs, algorithm/version, old/new places, override reason |
| Awards plan | Medium | Disjoint planning notes may merge; quantities recompute from approved config | No for plan configuration | TD/admin for conflicting places/items/units | Config diff, calculated impact, approver |
| Translations | Low | Per-key merge if different keys; same key conflict can show choice | Allowed only by explicit low-risk translation policy, not timestamp alone | Language/admin reviewer for same key | Key, languages, both values, choice |

## Conflict classes

- **Equivalent:** same semantic command/payload; acknowledge idempotently.
- **Disjoint mergeable:** policy explicitly allows nonoverlapping fields; server creates a new audited version.
- **Protected:** result, scratch, membership affecting active results, financial, rule, or placement conflict; human review required.
- **Deleted/stale reference:** update targets tombstone or invalid dependency; restore/reassign/reject by authorized reviewer.
- **Authorization/state:** user/device lacks capability or competition/stage is locked; never auto-merge.
- **Schema/package:** incompatible version; refresh/upgrade before resolution.

## Review workflow

Conflict record includes entity snapshot at BaseVersion when available, local intent, current server version, related entities, operation/user/device, timestamps, and policy classification. Reviewer may accept server, apply local as a new authorized version, construct a corrected value, or reject operation. Protected resolution generates a new operation/audit record; original evidence remains immutable.

## Designated ownership

Initial Safe Mode rollout assigns data ownership by station/table/event/division/device. The server/package carries ownership policy. Out-of-assignment offline writes are blocked or marked high-risk. Ownership reduces conflicts but does not bypass version checks or audit.

