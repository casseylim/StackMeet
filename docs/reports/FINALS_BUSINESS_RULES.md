# Finals Business Rules

## Current operator behavior — `legacy-finals-v1`

The current live/operator Finals report remains governed by the existing `FinalsReportEngine.js` behavior characterized in SP-4E. SP-4F does not change it.

- Finals placement is Finals-stage only.
- Placement groups are participant type + competition-snapshot division + event after the selected category/gender/report filters.
- Only rows classified Valid receive numeric placement.
- The legacy tie key is best, second-best, then third-best **raw valid final attempts**.
- Equal complete keys receive equal competition rank (`1, 1, 3`). Name/ID only stabilizes display within an equal rank.
- The current legacy tie key does not apply a normal finite result-level penalty even though the shared `BestResultEngine.rankingTime()` does.
- The current shared classifier accepts an otherwise-valid attempt before checking a `999` result-level penalty. This is frozen compatibility behavior, not the governed future rule.
- Finals All-Around is Individual-only and Finals-only: 3-3-3 + 3-6-3 + Cycle. Every event needs a valid Final result; Prelims never fill a missing Final.
- Qualification requires an approved snapshot. A cutoff tie requires an explicit exception decision/rationale.
- Location is intentionally omitted pending a real competition-scoped field. `AWD-001` remains out of scope.

Unversioned historical competitions are interpreted as `legacy-finals-v1`; they must never be silently re-ranked under a newer rule.

## Governed future contract — `governed-finals-v2`

SP-4F defines but does **not activate** `governed-finals-v2`.

- A result-level penalty of `999` or greater is Scratch and makes the result ineligible for ranking, even when valid attempts are present.
- A `999` attempt is never a time. When there is no result-level scratch penalty, a separate valid attempt may still make the row Valid.
- A normal finite penalty is greater than zero and less than `999`.
- The governed-v2 primary comparator is the official best time: lowest valid attempt + finite result-level penalty.
- If official best times are equal, second-best and then third-best raw valid attempts break the tie.
- Equal complete governed-v2 keys retain equal competition rank (`1, 1, 3`). Name/ID remains display-only.
- A permanent placement claim must carry explicit rule version, participant type, one competition-snapshot division, event, category and gender scope. A bare numeric rank is not a valid permanent career claim.

`governed-finals-v2` must not become operational until a later phase explicitly persists the selected rule version and an immutable ranking/scope snapshot. Unknown non-blank rule versions fail closed.
