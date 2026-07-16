# StackMeet Domain Model

**Status:** Sprint 6 design baseline. This describes the target domain; it does not change the current browser aggregate, XML, localStorage, or runtime.

Every persistent operation is scoped by internal `CompetitionId`. Public codes (`1.x`, `2.x`, `3.x`, competition code) remain human-facing natural identifiers, not cross-competition keys.

| Entity | Purpose and key business rules | Relationships | Future SQL table | Future API resource | Owner service |
|---|---|---|---|---|---|
| Competition | Competition aggregate root. Owns public code, dates, lifecycle and isolation boundary. | 1:M settings/events/divisions/participants/teams/results/packages. | `competitions` | `/competitions` | SettingsService |
| Competition Settings | Operational dates, stages, advancement and feature flags. Age uses start date. | 1:1 Competition; references stages/events. | `competitions`, `competition_stages`, `leaderboard_settings` | `/competitions/{id}/settings` | SettingsService |
| Event | Catalog event such as 3-3-3, 3-6-3, Cycle, enabled by group. | M:N Competition through CompetitionEvent; 1:M Results. | `events`, `event_groups`, `competition_events` | `/competitions/{id}/events` | SettingsService |
| Division | Named competition-scoped class generated from age/gender/Special cutoffs or custom override. | 1:M Stackers/Teams; configured by DivisionCutoff. | `divisions`, `division_cutoffs` | `/competitions/{id}/divisions` | DivisionService |
| Category | Planned award/report/final grouping independent of registration count. | references division, event, participant type and stage. | proposed `competition_categories` | `/competitions/{id}/categories` | SettingsService |
| Stacker | Registered individual. Public ID is `1.x`; DOB, age, gender and Special rules determine division. | M:1 Competition/Division/Organization; M:N Teams; 1:M individual Results. | `stackers` | `/competitions/{id}/stackers` | StackerService |
| Parent/Guardian | External adult named on Child/Parent doubles when not a registered stacker. | 1:M Child/Parent Team memberships; optionally contacts Stacker. | proposed `guardians` | `/competitions/{id}/guardians` | TeamService |
| Doubles Team | Normal or Child/Parent team. Public ID `2.x`; normal complete team has two distinct stackers; membership conflicts are prohibited. | M:1 Competition/Division; 1:M TeamMember; 1:M team Results. | `teams`, `team_members` | `/competitions/{id}/teams` | TeamService |
| Relay Team | Named relay. Public ID `3.x`; name unique per competition; one to six unique members; complete at four. | M:1 Competition/Division; 1:M TeamMember; 1:M team Results. | `teams`, `team_members` | `/competitions/{id}/teams` | TeamService |
| Result | Authoritative scored performance. Blank is no recorded time; `999` is scratch; best valid attempt plus penalty is official. | M:1 Competition/Event/Stage; exactly one Stacker or Team; 1:M ResultAttempt. | `results`, `result_attempts` | `/competitions/{id}/results` | ResultService |
| Final Result | Result in Finals plus qualified placement. Fastest valid result wins; tie break uses best, second, then third attempt. | specialization of Result; belongs to Final Sheet/Lane. | `results`, `result_attempts` | `/competitions/{id}/final-results` | FinalsService |
| Award | Planned award configuration/output. Individual=1 unit, doubles=2, relay=configured units; overall is separate. | M:1 Competition; references Category/Division/Event. | proposed `award_plans`, `award_plan_items` | `/competitions/{id}/awards` | AwardService |
| Official | Authorized competition operator who approves protected results and resolves conflicts. | M:N Competition through access assignment; 1:M audit actions. | `users`, proposed `competition_roles` | `/competitions/{id}/officials` | AuthorizationService |
| Judge | Official assigned to score/operate a sheet or lane; does not change scoring rules. | M:N TimeSheet/Lane assignment. | proposed `sheet_assignments` | `/competitions/{id}/judges` | FinalsService |
| Lane | Physical or ordered final-sheet position. Lane order is slowest qualifier first in current behavior. | M:1 TimeSheet; 0..1 Finalist/Final Result. | proposed `sheet_lanes` | `/competitions/{id}/time-sheets/{sheetId}/lanes` | FinalsService |
| Heat | Scheduled grouping for a stage/event/category. Not yet a current persisted runtime entity; required for future scheduling. | M:1 Competition/Event/Stage; M:N participants via HeatEntry. | proposed `heats`, `heat_entries` | `/competitions/{id}/heats` | SchedulingService |
| Record | Auditable immutable fact: result approval, rule-relevant change, import, sync outcome or correction. | M:1 Competition; optionally references actor/entity. | proposed `audit_records` | `/competitions/{id}/records` | AuditService |
| Competition Package | Versioned portable snapshot for Safe Mode/recovery. XML remains current backup compatibility format until separately migrated. | M:1 Competition; contains projections, audit/checkpoint/outbox metadata. | proposed `competition_packages` | `/competitions/{id}/packages` | PackageService |

## Aggregate and invariant boundaries

- **Competition** is the transaction and authorization boundary.
- **Team** owns memberships; a Stacker is referenced, never copied into team identity.
- **Result** owns attempts and is immutable after protected approval except through an audited correction workflow.
- **Final Result** is a Result specialization; it must not become a competing scoring model.
- **Competition Package** is an offline replica/exchange envelope, never the SQL authority.

## Deliberate design decisions

- Division name is presentation; a future division ID is the persistent relationship.
- Current public IDs are unique only inside their competition and entity prefix.
- Parent/Guardian is separate because an external adult is not a Stacker.
- Category is explicit to prevent award/final/report planning from depending on current registration counts.
