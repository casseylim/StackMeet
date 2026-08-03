# Competition Flow Matrix

This matrix identifies the observable checkpoints that must remain equivalent during future refactoring.

| ID | Flow stage | Input/state | Observable checkpoint |
|---|---|---|---|
| FLOW-001 | Create competition | Empty or demo state | Default settings, divisions, events, and state schema are normalized. |
| FLOW-002 | Configure settings | Age cutoffs, event groups, competition date, Special split | Settings persist and generated divisions reflect the current configuration. |
| FLOW-003 | Register Individuals | DOB, gender, Special, imported/custom fields | ID, age, division, and stored override fields are correct. |
| FLOW-004 | Import Stackers | XML or SQL records | Imported state normalizes, standard divisions preserve, Special divisions recalculate as currently defined. |
| FLOW-005 | Generate divisions | Current settings and stackers | Visible list includes configured and participant-relevant divisions without team M/F suffixes. |
| FLOW-006 | Generate Doubles | Normal, Special, Child/Parent, custom teams | Membership validation, division, conflict removal, and stored team fields match baseline. |
| FLOW-007 | Generate Relay | Timed and head-to-head teams | Membership validation, completion status, oldest-member division, and stored fields match baseline. |
| FLOW-008 | Enter preliminaries | Individual, Doubles, Relay events | Participant resolution, time parsing, scratch/DNS handling, and persistence match baseline. |
| FLOW-009 | Generate finals | Prelim results and advancement settings | Qualification, lane order, and finalist data match baseline. |
| FLOW-010 | Record final results | Attempts, penalties, ties | Official result, placement, and tie-break behavior match baseline. |
| FLOW-011 | Awards | Events, divisions, places, award units | Current planner output or documented baseline defect is retained. |
| FLOW-012 | Reports | State, filters, ranking data | Rows, filters, grouping, and ordering match baseline. |
| FLOW-013 | Public results | Published payload and stored divisions | Individual, Doubles, and Relay grouping uses the correct stored division strings. |
| FLOW-014 | Export XML | Complete competition state | XML contains the current fields and division values. |
| FLOW-015 | Import XML | Previously exported XML | Re-imported state is equivalent at the documented comparison boundary. |

## Comparison boundary

State equivalence must compare normalized business fields, not transient UI state such as active tabs, selected rows, flash messages, or object key order.
