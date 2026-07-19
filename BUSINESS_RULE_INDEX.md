# StackMeet Business Rule Index

This index connects business rules to their behavior IDs, priority, and current Sprint 5 protection. It is the entry point for future migration work.

| Rule area | Rule | Behavior IDs | Criticality | Sprint 5 status |
|---|---|---|---|---|
| Public IDs | Individual, doubles and relay IDs use `1.x`, `2.x`, `3.x` sequences. | REG-001, REG-002, RES-001 | High / Critical | Automated |
| DOB and age | DOB normalizes when valid; age is taken on competition start date. | REG-003, REG-004 | High / Critical | Automated |
| Divisions | Generated division uses configured age/gender/Special rules; custom division takes precedence. | REG-005 | Critical | Automated core; custom override browser follow-up |
| Registration lifecycle | Stackers can be added, edited, deleted, searched, and sorted. | REG-006, REG-007 | High | Sort automated; interaction browser follow-up |
| Doubles | Normal team has two distinct stackers; Child/Parent supports registered or external parent; conflicts are displaced. | TEAM-001, TEAM-002, TEAM-003 | Critical | Automated |
| Relay | Name is required and unique; one-to-six unique members; four makes a completed relay; conflicts are removed. | TEAM-004, TEAM-005, TEAM-006 | Critical | Automated core |
| Result identity | Compact IDs map to public entity IDs. | RES-001 | Critical | Automated |
| Result semantics | Blank means no recorded time/DNS; `999` is scratch; valid attempt parsing and official time use best attempt plus penalty. | RES-002, RES-003, RES-004 | Critical | Automated core |
| Result storage | Current JSON state persists under the existing localStorage key. | RES-005, STO-001 | Critical | Automated core; reload browser follow-up |
| Finals | Prelim ranking qualifies configured limits; judge order is slowest qualifier first; fastest final wins with three-attempt tie break. | FIN-001, FIN-002, FIN-003 | Critical | Tie break automated; sheet rendering/qualification browser follow-up |
| Awards | Planned structure drives individual/doubles/relay/overall quantities. | AWD-001, AWD-002, AWD-003 | Critical | Current planner throw automated as known defect |
| Reports | Reports reflect source data, filters/sort, and Special modes. | RPT-001, RPT-002, RPT-003 | High | Special filter automated; UI/browser follow-up |
| XML | Version 1 XML is current portable backup; import replaces and normalizes state. | STO-002, STO-003 | Critical | Export automated; `DOMParser` import browser follow-up |
| Recovery | Existing corrupt storage/XML recovery must not drift. | STO-004 | Critical | Browser follow-up |
| Administration | Dashboard/settings/language and print preview remain available. | UI-001, UI-002 | Medium | Browser follow-up |
| Presentation | Layout, styling and button placement remain unchanged. | UI-003 | Low | Hash-protected production assets |

## Critical defect register

| Defect | Behavior ID | Observed current outcome | Migration rule |
|---|---|---|---|
| Awards Planner references an undefined `generatedDivisions` identifier. | AWD-001 | Opening/calculating the plan throws before rows are produced. | Do not silently alter this during Repository migration. Correct it in a separately approved change with replacement expected outcomes and regression tests. |

## Usage in future regression reports

List failures as `Critical`, `High`, `Medium`, then `Low`; include behavior ID, fixture, observed result, expected result, and migration decision. A Critical failure blocks the Sprint 6 Repository cutover.
