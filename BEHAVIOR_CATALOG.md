# StackMeet Behavior Catalog

Sprint 5 records observable current behavior, not a preferred future design. Each ID is used by the characterization suite or is a browser-runtime scenario scheduled for its next expansion. Regression reports must list failed IDs in priority order: Critical, High, Medium, Low.

## Registration

| ID | Criticality | Description | Given | When | Then / Expected result |
|---|---|---|---|---|---|
| REG-001 | High | Individual registration ID | no stackers | an individual is added | ID starts at `1.1` and increments numerically. |
| REG-002 | High | ID sequences | existing public IDs | a stacker/doubles/relay is added | each prefix (`1.`, `2.`, `3.`) advances independently. |
| REG-003 | High | DOB normalization | supported DOB input | it is saved | valid dates normalize to ISO; invalid dates are rejected. |
| REG-004 | Critical | Age calculation | DOB and competition start | division is calculated | age is as of the start date, including birthday boundary. |
| REG-005 | Critical | Division assignment | age, gender, Special flag, configured cutoffs | division is generated | applicable combined/gender/Special division is assigned; custom division overrides it. |
| REG-006 | High | Edit and delete | a registered stacker | edit/delete is confirmed | edit preserves public ID; delete removes the stacker. |
| REG-007 | High | Search and sorting | registered stackers | search/filter/sort is selected | matching rows are returned; numeric public IDs sort numerically. |

## Teams

| ID | Criticality | Description | Given | When | Then / Expected result |
|---|---|---|---|---|---|
| TEAM-001 | Critical | Doubles validation | a proposed team | it is saved | normal complete doubles require two distinct registered stackers. |
| TEAM-002 | Critical | Child/Parent validation | a Child/Parent team | it is saved | child plus registered parent or external parent name is required. |
| TEAM-003 | Critical | Doubles conflict prevention | a member has another doubles team | reassigned | conflicting team membership is removed. |
| TEAM-004 | Critical | Relay validation | a proposed relay | it is saved | name is mandatory/unique, members are unique, one to six members are allowed. |
| TEAM-005 | Critical | Relay completion | a relay | members are evaluated | four or more members means complete. |
| TEAM-006 | Critical | Relay conflict prevention | member belongs to another relay | reassigned | member is removed from conflicting relay; empty teams are removed. |

## Results and Finals

| ID | Criticality | Description | Given | When | Then / Expected result |
|---|---|---|---|---|---|
| RES-001 | Critical | Compact result IDs | `12`, `23`, or `34` | entry ID is resolved | IDs become `1.2`, `2.3`, and `3.4`; unsupported prefix is rejected. |
| RES-002 | Critical | Preliminary entry | enabled participant/event | attempts are saved | valid attempts are stored against stage, type, participant and event. |
| RES-003 | Critical | Blank, scratch and DNS | blank or `999` input | a result is evaluated | blank is no recorded time/DNS; `999` is scratch and cannot rank. |
| RES-004 | Critical | Attempts | one to three valid attempts | result is evaluated | lowest valid attempt is official, plus any penalty. |
| RES-005 | Critical | Persistence | results are saved | page is reloaded | current state is retained under the existing localStorage key. |
| FIN-001 | Critical | Qualification | prelim results and configured limits | finals sheets are formed | eligible qualifiers are selected from prelim ranking. |
| FIN-002 | Critical | Lane order | qualifiers exist | final sheet is generated | slowest qualifier is listed first for judges. |
| FIN-003 | Critical | Winner and ties | valid final attempts | placement is calculated | fastest valid result wins; best, second-best, then third-best attempts break ties. |

## Awards and Reports

| ID | Criticality | Description | Given | When | Then / Expected result |
|---|---|---|---|---|---|
| AWD-001 | Critical | Planner calculation | enabled events/divisions and award configuration | plan is generated | **Current observed defect:** planner throws because `generatedDivisions` is absent; preserve as a failing-current behavior until an approved corrective sprint changes the expected result. |
| AWD-002 | Critical | Individual/Doubles/Relay awards | places and item choices | plan is generated | individual unit is 1; doubles unit is 2; relay unit uses configured awards per team. |
| AWD-003 | Critical | Overall awards | overall categories and limits | plan is generated | configured category, item and Top-N quantity appear. |
| RPT-001 | High | Generated reports | current registrations/results | report is run | rows reflect report type and available data. |
| RPT-002 | High | Report filters and sorting | a report with multiple values | filters/sort apply | matching rows are returned in current ranking/sort order. |
| RPT-003 | High | Special stacker reporting | normal and Special rows | Special mode is selected | combined, Special-only, and normal-only modes retain their current meanings. |

## Storage and interface

| ID | Criticality | Description | Given | When | Then / Expected result |
|---|---|---|---|---|---|
| STO-001 | Critical | Load/save/reset | current browser state | load, save or Reset Demo is used | behavior continues using `stackmeet-stacktrack-style-v1`; reset returns normalized demo data. |
| STO-002 | Critical | XML export | current state | Export XML is used | StackMeet version 1 XML is generated as portable backup. |
| STO-003 | Critical | XML import | valid StackMeet version 1 XML | import is confirmed | imported state replaces current state, is normalized, and persists. |
| STO-004 | Critical | Invalid storage/XML | corrupt JSON or invalid XML | app loads/imports | current recovery/error behavior is retained. |
| UI-001 | Medium | Dashboard/settings/language | app route is selected | view renders/saves | current administrative workflow remains available. |
| UI-002 | Medium | Print preview | paperwork/report is generated | print is requested | generated content remains available until replaced or navigation changes. |
| UI-003 | Low | Presentation | existing templates/styles | page renders | layout and styling remain unchanged; not regression-owned by this suite. |

## Priority policy

- **Critical:** stop refactoring and investigate before migration continues.
- **High:** fix before release unless an explicit tournament-owner waiver exists.
- **Medium:** track and resolve within the affected workflow.
- **Low:** document and schedule with presentation work.
