# Business Rule Mapping

`BR-xxx` identifiers are introduced here as stable Sprint 6 design IDs. They map every rule area in `BUSINESS_RULE_INDEX.md`; they do not replace Sprint 5 characterization IDs.

| BR ID | Rule | Entity | Future module/service | Future repository operation | Characterization IDs | Known defect / note |
|---|---|---|---|---|---|---|
| BR-001 | Generate public participant/team IDs by prefix and competition. | Stacker, Doubles Team, Relay Team | StackerService, TeamService | `nextPublicCode(competitionId,prefix)` | REG-001, REG-002, RES-001 | Public code is not a global primary key. |
| BR-002 | Normalize DOB and calculate age at competition start. | Stacker, Competition Settings | DivisionService | `getCompetitionSettings` | REG-003, REG-004 | Current date-format compatibility retained. |
| BR-003 | Assign division using cutoff, gender, Special and custom override. | Division, Stacker | DivisionService | `listDivisions`, `saveStacker` | REG-005 | Custom override browser scenario pending. |
| BR-004 | Create, edit, delete, search and sort stackers. | Stacker | StackerService | CRUD/query stackers | REG-006, REG-007 | Deletion impact plan required. |
| BR-005 | Validate normal doubles and Child/Parent participation. | Doubles Team, Guardian | TeamService | `saveTeam` transaction | TEAM-001, TEAM-002 | External guardian is not a Stacker. |
| BR-006 | Prevent or resolve conflicting doubles membership. | Doubles Team, TeamMember | TeamService | `saveTeam` transaction | TEAM-003 | Current behavior removes conflict; preserve until approved change. |
| BR-007 | Validate relay name/members and completion. | Relay Team, TeamMember | TeamService | `saveTeam` transaction | TEAM-004, TEAM-005 | Name unique per competition; complete at four. |
| BR-008 | Resolve conflicting relay membership. | Relay Team, TeamMember | TeamService | `saveTeam` transaction | TEAM-006 | Current behavior removes member/empty team. |
| BR-009 | Resolve compact IDs to the correct participant. | Stacker, Team, Result | ResultService | `resolveParticipant` query | RES-001 | Prefix validates participant type. |
| BR-010 | Parse prelim result input and retain blank/scratch semantics. | Result, ResultAttempt | ResultService | `saveResult` | RES-002, RES-003 | `999` remains scratch; blank remains no recorded time. |
| BR-011 | Determine official result from attempts and penalty. | Result, ResultAttempt | ResultService | `getResultRanking` | RES-004 | Scratch is excluded from official ranking. |
| BR-012 | Persist/reset/import/export current competition state. | Competition Package, Record | CompetitionRepository, PackageService | load/save/reset/import/export | RES-005, STO-001..STO-004 | Preserve current JSON/XML until a separately approved migration. |
| BR-013 | Qualify finalists and order judge lanes. | Final Result, Lane, TimeSheet | FinalsService | `buildFinalSheets` | FIN-001, FIN-002 | Qualification browser coverage pending. |
| BR-014 | Calculate final placement and tie break. | Final Result, ResultAttempt | FinalsService | `saveFinalSheet`, `getPlacements` | FIN-003 | Best → second → third attempt. |
| BR-015 | Plan individual, doubles, relay and overall awards. | Award, Category | AwardService | `getAwardPlan` | AWD-001..AWD-003 | Core individual, doubles, and relay quantities are protected by the characterization suite. |
| BR-016 | Generate/filter/sort reports, including Special modes. | Report projection, Stacker, Team, Result | ReportService | report query/export | RPT-001..RPT-003 | Projection only; no HTML/domain coupling. |
| BR-017 | Retain administrative routes and print behavior. | Competition Settings, Record | SettingsService, PrintDocumentService | settings/query/document model | UI-001, UI-002, UI-003 | Medium/Low browser coverage pending. |

## Mapping constraints

- `CompetitionRepository` is the only persistence boundary; services do not access localStorage, XML, IndexedDB, HTTP, or SQL.
- All command/query operations receive an authorized `CompetitionId`.
- A future result command carries `BaseVersion`, actor/device identity and idempotency key; protected conflicts require Official review.
- Defect correction is separate from parity migration. BR-015 cannot be silently repaired in a Repository refactor.
