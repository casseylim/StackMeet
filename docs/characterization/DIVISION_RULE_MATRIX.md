# Division Rule Matrix

This matrix records current observable behavior. It is a characterization specification, not a proposal for improved rules.

## Individual divisions

| ID | Category | Gender | Special | Imported standard | Custom | Split enabled | Expected behavior |
|---|---|---|---|---|---|---|---|
| DIV-001 | Individual | M/F | No | No | No | Either | Uses the configured normal male/female/combined age path. |
| DIV-002 | Individual | M/F | No | Yes | No | Either | Preserves the imported standard division. |
| DIV-003 | Individual | M/F | No | Yes | Yes | Either | Custom division wins over imported and calculated values. |
| DIV-004 | Individual | M/F | Yes | No | No | Off | Uses the combined Special/SS individual division. |
| DIV-005 | Individual | M | Yes | No | No | On | Uses the Special individual division with ` M` suffix. |
| DIV-006 | Individual | F | Yes | No | No | On | Uses the Special individual division with ` F` suffix. |
| DIV-007 | Individual | M/F | Yes | Yes | No | On | Recalculates the Special division; imported Special text does not block M/F separation. |
| DIV-008 | Individual | M/F | Yes | Yes | Yes | On/Off | Custom division remains unchanged. |
| DIV-009 | Individual | M/F | No | No | No | Either | Missing or unusable age follows the current `Open`/existing-division fallback. |
| DIV-010 | Individual | M/F | No/Yes | No | No | Either | Competition-date age honors actual-age and year-born modes. |

## Team divisions

| ID | Team type | Members | Special status | Gender mix | Expected behavior |
|---|---|---|---|---|---|
| DIV-020 | Normal Doubles | 2 | Neither | Any | Uses the configured doubles cutoff from the oldest member. No M/F suffix. |
| DIV-021 | Special Doubles | 2 | Either member Special | Any | Uses the configured Special Doubles cutoff. No M/F suffix. |
| DIV-022 | Child/Parent Doubles | Registered/external parent | Normal | Any | Uses the configured Child/Parent cutoff. No M/F suffix. |
| DIV-023 | Special Child/Parent Doubles | Registered/external parent | Current first-member Special rule | Any | Uses the current Special Child/Parent behavior. No M/F suffix. |
| DIV-024 | Timed Relay | 1-6 members | Any | Any | Uses the configured relay cutoff from the oldest member. No M/F suffix. |
| DIV-025 | Head-to-Head Relay | 1-6 members | Any | Any | Uses the configured head-to-head cutoff from the oldest member. No M/F suffix. |
| DIV-026 | Any team | Any | Any | Any | Custom team division overrides generated team division. |

## Generated division list invariants

- Configured normal Individual divisions are generated from male, female, and combined cutoffs.
- Configured Special Individual divisions are combined when the split is off, and produce M/F entries when the split is on.
- Doubles and Relay entries never receive M/F suffixes.
- Imported and participant-specific divisions may be appended so visible divisions remain usable after import or recalculation.
- Duplicates are removed using the current division comparison/sorting behavior.

## Required regression examples

The minimum locked examples are:

1. Special male Individual with split on -> `SS ... M`.
2. Special female Individual with split on -> `SS ... F`.
3. Mixed-gender Doubles with a Special member -> `SS ...U`, without M/F.
4. Mixed-gender Relay with a Special member -> `...U`, without M/F.
5. Normal imported standard division remains preserved.
6. Imported Special division recalculates when split is enabled.
7. Custom Individual, Doubles, and Relay divisions remain unchanged.
