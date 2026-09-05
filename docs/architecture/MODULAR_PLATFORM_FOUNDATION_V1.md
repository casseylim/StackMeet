# NADITrack Modular Platform Foundation v1

Status: Architecture baseline only  
Scope: No runtime behavior changes, no database migration, no production deployment  
Baseline master: `aea1cf3099a8151c9e8d6ed8b61ae454159dedf8`

## 1. Purpose

NADITrack is evolving from a Sport Stacking application into a modular activity and competition management platform.

The platform must preserve the existing Sport Stacking implementation as a stable module while allowing future activity modules such as Chess, Swimming, Athletics, Archery, and other school or community competitions to reuse a common competition platform.

This document establishes the first formal module boundary. It does not move code yet. It defines what is shared, what remains Sport Stacking-specific, what contracts will eventually be required, and the safe extraction sequence.

## 2. Non-negotiable constraints

1. Existing Sport Stacking workflows remain behaviorally stable.
2. Existing permanent public results URLs remain valid: `/{CompetitionID}/Results`.
3. Existing production deployment safety rules remain unchanged.
4. No database migration is part of Foundation v1 Phase 1.
5. No existing table, controller, JavaScript module, route, JSON payload, or report is renamed in Phase 1.
6. `FinalsReportEngine.js` is not modified as part of this architecture phase.
7. Certificate Phase 5C remains outside scope pending its separate dependency decision.
8. Activity-specific competition rules must not be forced into one generic scoring engine.
9. Shared Core may own lifecycle, identity, persistence infrastructure, and integration contracts, but each activity module owns its competition semantics.
10. Production remains on its currently verified deployed binary until a future deployment is separately approved.

## 3. Current-state findings

### 3.1 The backend already contains a useful shared-platform skeleton

The current `Competition` model is mostly activity-neutral. It already owns:

- competition identity/code/key;
- name and venue;
- start/end dates;
- lifecycle status and archive metadata;
- public-directory visibility;
- results revision;
- competition user membership;
- audit logs;
- assets;
- participant/result relationships.

Current file:

`backend/StackMeet.Api/Models/Competition.cs`

This is a strong candidate for Shared Core rather than Sport Stacking ownership.

### 3.2 Participant persistence is useful but named and shaped around Sport Stacking

The current SQL participant model is `Stacker` and contains a mixture of general participant fields and Sport Stacking-specific fields.

General candidate fields:

- competition ownership;
- participant code;
- first/last name;
- gender;
- birth date;
- country/region;
- organization/club;
- email/phone;
- payment/check-in status;
- timestamps.

Sport Stacking-specific fields or semantics:

- model name `Stacker`;
- `WssaId`;
- `CustomDivision` as currently interpreted;
- `IsSpecialStacker`;
- downstream division calculation semantics.

Current file:

`backend/StackMeet.Api/Models/Stacker.cs`

Foundation rule: do not rename the SQL model yet. Introduce a future platform-level participant contract first, then adapt `Stacker` behind it.

### 3.3 Result storage is structurally reusable, but validation is Sport Stacking-specific

`CompetitionResult` already provides a useful generic persistence envelope:

- competition ownership;
- public row identity;
- stage;
- participant type;
- participant code;
- event code;
- attempt payload;
- penalty;
- competition-scoped revision;
- audit attribution.

Current file:

`backend/StackMeet.Api/Models/CompetitionResult.cs`

However, `CompetitionResultRules` hard-codes Sport Stacking semantics:

- stages: `Prelims`, `Finals`;
- participant types: `Individual`, `Doubles`, `Timed Relay`;
- events: `3-3-3`, `3-6-3`, `Cycle`;
- Timed Relay event restriction;
- one-to-three timed attempts;
- decimal timing and penalty rules.

Current file:

`backend/StackMeet.Api/Services/CompetitionResultRules.cs`

Foundation rule: keep the SQL row/revision infrastructure in Shared Core, but move identity validation, attempt validation, scoring, ranking, and stage semantics behind the activity module contract.

### 3.4 Competition JSON state is currently a Sport Stacking document

`EmptyCompetitionStateFactory` creates settings and structures that are explicitly Sport Stacking-oriented:

- preliminary/final controls;
- qualification counts;
- Sport Stacking event codes;
- Individual/Doubles/TimedRelay/HeadToHead groups;
- male/female/special/relay division structures;
- stackers/doubles/relays;
- final qualification snapshots.

Current file:

`backend/StackMeet.Api/Services/EmptyCompetitionStateFactory.cs`

Foundation rule: existing state format remains a compatibility document for the Sport Stacking module. Future activity modules must not copy this full schema.

A future platform state envelope should separate:

- shared competition settings;
- shared presentation/i18n metadata;
- module identifier and module version;
- activity-owned module state.

No state schema change is made in Phase 1.

### 3.5 The public results controller mixes platform and Sport Stacking responsibilities

`PublicResultsController` currently performs both reusable platform work and Sport Stacking work.

Reusable responsibilities:

- competition lookup and lifecycle filtering;
- branding assets;
- last-updated calculation;
- SQL result retrieval;
- SQL participant retrieval;
- public-safe payload construction;
- translation exposure.

Sport Stacking responsibilities:

- stacker terminology;
- division reconstruction;
- special-stacker rules;
- year-born division behavior;
- doubles/relay extraction;
- qualification counts;
- Sport Stacking-specific public result shape.

Current file:

`backend/StackMeet.Api/Controllers/PublicResultsController.cs`

Foundation rule: keep the route and public portal shell stable. Later introduce a module-specific public-results projector behind the existing route.

### 3.6 Shared infrastructure is already identifiable

The following current areas are strong Shared Core candidates:

- authentication/session management;
- account/user administration;
- competition permission checks;
- audit logging;
- protected settings;
- competition asset storage;
- competition key rules;
- SignalR transport/revision notification infrastructure;
- API provider/repository abstractions;
- competition package transport mechanics;
- i18n framework;
- production safety/deployment tooling.

Examples:

- `backend/StackMeet.Api/Program.cs`
- `backend/StackMeet.Api/Models/AppUser.cs`
- `backend/StackMeet.Api/Models/AppRole.cs`
- `backend/StackMeet.Api/Models/CompetitionUser.cs`
- `backend/StackMeet.Api/Models/AuditLog.cs`
- `backend/StackMeet.Api/Services/CompetitionPermissionService.cs`
- `backend/StackMeet.Api/Services/CompetitionAssetStorage.cs`
- `backend/StackMeet.Api/Services/AuditLogService.cs`
- `backend/StackMeet.Api/wwwroot/js/storage/Repository.js`
- `backend/StackMeet.Api/wwwroot/js/storage/ApiProvider.js`
- `backend/StackMeet.Api/wwwroot/js/i18n/`

### 3.7 Sport Stacking domain logic is already identifiable

The following current areas belong to the Sport Stacking module even if they are not physically moved yet:

- Sport Stacking registration semantics;
- WSSA-specific participant metadata;
- division calculation and gender/special rules;
- 3-3-3 / 3-6-3 / Cycle event definitions;
- Doubles rules including Child/Parent;
- Relay rules and membership;
- preliminary timing workflow;
- final qualification and placement;
- best-result calculation;
- all-around logic;
- awards planning;
- Sport Stacking report generation;
- Sport Stacking leaderboard/result presentation.

Examples:

- `backend/StackMeet.Api/Services/CompetitionResultRules.cs`
- `backend/StackMeet.Api/wwwroot/js/results/BestResultEngine.js`
- `backend/StackMeet.Api/wwwroot/js/reports/FinalsReportEngine.js`
- Sport Stacking sections currently contained in `backend/StackMeet.Api/wwwroot/app.js`
- Sport Stacking-specific structures produced by `EmptyCompetitionStateFactory`

## 4. Target platform architecture

```text
NADITrack
|
+-- Shared Core
|   +-- Identity & Access
|   +-- Competition Lifecycle
|   +-- Participant Directory / Competition Entries
|   +-- Organizations / Schools / Clubs
|   +-- Activity Module Registry
|   +-- Generic Result Persistence + Revisions
|   +-- Assets / Branding
|   +-- Audit
|   +-- i18n
|   +-- Public Portal Shell
|   +-- Notifications / Integrations
|   +-- Offline Package / Sync Transport
|
+-- Activity Modules
|   +-- Sport Stacking   [existing behavior preserved]
|   +-- Chess            [first proof module later]
|   +-- Swimming         [future]
|   +-- Athletics        [future]
|   +-- Archery          [future]
|   +-- Other Activities [future]
|
+-- Infrastructure Adapters
    +-- SQL Server
    +-- HTTP API
    +-- SignalR
    +-- IndexedDB / Offline Package
    +-- Email
    +-- File / Asset Storage
    +-- PDF / Certificate Adapter
```

## 5. Shared Core ownership

### 5.1 Identity and access

Shared Core owns:

- application users;
- roles;
- sessions/tokens;
- competition membership;
- competition-scoped permissions;
- system administration;
- audit attribution.

Activity modules must not implement their own login/session systems.

### 5.2 Competition lifecycle

Shared Core owns:

- competition ID/key/code;
- competition name;
- venue;
- date range;
- status lifecycle;
- archive state;
- public listing;
- branding assets;
- ownership/access.

Activity modules may validate module-specific readiness but must not replace the platform lifecycle.

### 5.3 Participants and organizations

Shared Core should eventually expose neutral contracts such as:

```text
Participant
CompetitionEntry
Organization
ParticipantOrganizationMembership
```

Existing `Stacker` persistence remains in place until a dedicated migration phase is approved.

The module contract may add activity-specific registration fields, for example:

- Sport Stacking: WSSA ID, Special Stacker;
- Chess: federation/rating data;
- Swimming: seed time, stroke eligibility;
- Athletics: personal best, event eligibility.

Activity-specific fields must not pollute the shared participant contract unless they are genuinely common across activities.

### 5.4 Generic results substrate

Shared Core owns transport and consistency, not sport rules.

Shared responsibilities:

- result row identity;
- competition ownership;
- revision allocation;
- optimistic concurrency;
- transactional batch write mechanics;
- audit attribution;
- closed/archive protection;
- SignalR change notification;
- public-safe projection pipeline.

Module responsibilities:

- allowed stages;
- event codes;
- entry types;
- result value schema;
- validation;
- scoring;
- ranking;
- ties;
- qualification;
- penalties/disqualification semantics;
- final/public presentation fields.

### 5.5 Public portal shell

The permanent route remains:

`/{CompetitionID}/Results`

Shared Core owns:

- route resolution;
- competition header;
- branding;
- official/provisional status;
- last-updated indicator;
- language selection;
- common loading/error states;
- module navigation host.

Each activity module owns the result sections it contributes.

For Sport Stacking this continues to include areas such as:

- Dashboard;
- Preliminary;
- Final;
- All-Around;
- Doubles;
- Relay;
- Medals/Awards;
- Certificates;
- Downloads.

A Chess module could instead contribute sections such as Standings, Pairings, Rounds, Tie-breaks, and Cross Table without changing the permanent competition route.

## 6. Activity module contract — conceptual v1

This is an architectural contract only. No interface is added to production code in Phase 1.

A future activity module must be able to provide:

```text
IActivityModule
  Identity
    moduleCode
    displayName
    moduleVersion

  Capabilities
    supportsTeams
    supportsDivisionsOrCategories
    supportsPreliminaryFinalStages
    supportsLiveResults
    supportsCertificates
    supportsOfflinePackage

  Registration
    registrationFieldSchema
    validateRegistration(entry, competitionContext)

  Competition Setup
    moduleSettingsSchema
    validateSetup(settings)
    eventDefinitions(settings)
    categoryDefinitions(settings, participants)

  Results
    resultSchema(event, stage, entryType)
    validateResult(result, context)
    normalizeResult(result)
    rank(entries, context)
    qualify(entries, context)

  Public Projection
    projectPublicCompetition(context)
    publicSections(context)

  Reporting
    reportDefinitions(context)
    certificateData(context)

  Offline
    exportModuleState(context)
    validateModuleState(package)
```

The final interface may be split into smaller interfaces. The important rule is ownership, not the exact method names.

## 7. Sport Stacking compatibility adapter

The first module implementation must be an adapter around existing behavior rather than a rewrite.

Proposed future shape:

```text
SportStackingModule
  +-- SportStackingRegistrationRules
  +-- SportStackingDivisionRules
  +-- SportStackingTeamRules
  +-- SportStackingResultRules
  +-- SportStackingQualificationRules
  +-- SportStackingRankingRules
  +-- SportStackingPublicResultsProjector
  +-- SportStackingReports
```

Initially these components may delegate to existing code. Extraction happens only after characterization tests prove behavioral equivalence.

The adapter must preserve:

- participant IDs and team ID conventions;
- age calculation modes;
- Normal/Special semantics;
- gender separation behavior;
- Doubles and Child/Parent rules;
- Relay membership rules;
- scratch/blank/attempt behavior;
- Prelims/Finals semantics;
- qualification counts;
- tie-break behavior;
- current report output;
- existing public result payload behavior for Sport Stacking.

## 8. Boundary map of current files

The table is classification guidance. It does not require files to move immediately.

| Current area | Target owner | Phase-1 action |
|---|---|---|
| `Models/AppUser.cs`, `AppRole.cs`, `AppUserToken.cs` | Shared Core / Identity | Keep |
| `Models/Competition.cs` | Shared Core / Competition | Keep |
| `Models/CompetitionUser.cs` | Shared Core / Access | Keep |
| `Models/AuditLog.cs` | Shared Core / Audit | Keep |
| `Models/CompetitionAsset.cs` | Shared Core / Assets | Keep |
| `Models/Stacker.cs` | Transitional: Shared participant persistence + Sport Stacking fields | Keep; do not rename yet |
| `Models/CompetitionResult.cs` | Shared Core result persistence envelope | Keep |
| `Models/CompetitionState.cs` | Transitional compatibility state | Keep |
| `Services/CompetitionPermissionService.cs` | Shared Core / Access | Keep |
| `Services/CompetitionAssetStorage.cs` | Shared Core / Assets | Keep |
| `Services/CompetitionParticipantReferenceService.cs` | Shared Core with module-aware extension later | Keep |
| `Services/CompetitionResultRules.cs` | Sport Stacking module | Keep in place; later adapt |
| `Services/EmptyCompetitionStateFactory.cs` | Sport Stacking compatibility bootstrap + future shared envelope | Keep in place |
| `Controllers/CompetitionsController.cs` | Shared Core / Competition API | Keep |
| `Controllers/CompetitionAdminController.cs` | Shared Core / Administration | Keep |
| `Controllers/CompetitionAssetsController.cs` | Shared Core / Assets | Keep |
| `Controllers/StackersController.cs` | Transitional participant API | Keep; later front with neutral entry contract |
| `Controllers/CompetitionResultsController.cs` | Shared persistence endpoint + module validation | Keep; later separate responsibilities |
| `Controllers/PublicCompetitionDirectoryController.cs` | Shared Core / Public directory | Keep |
| `Controllers/PublicResultsController.cs` | Shared shell + Sport Stacking projector currently mixed | Keep route; later introduce projector |
| `wwwroot/js/storage/Repository.js` | Shared Core client infrastructure | Keep |
| `wwwroot/js/storage/ApiProvider.js` | Shared Core client infrastructure | Keep |
| `wwwroot/js/storage/ResultApi.js` | Shared persistence client; module semantics above it | Keep |
| `wwwroot/js/storage/StackerApi.js` | Transitional participant client | Keep |
| `wwwroot/js/CompetitionPackage*.js` | Shared transport with module payload extension later | Keep |
| `wwwroot/js/i18n/` | Shared Core / i18n | Keep |
| `wwwroot/js/results/BestResultEngine.js` | Sport Stacking module | Preserve behavior |
| `wwwroot/js/reports/FinalsReportEngine.js` | Sport Stacking module | Preserve untouched |
| Sport Stacking workflows in `wwwroot/app.js` | Sport Stacking module + application shell currently mixed | Characterize before extraction |

## 9. Dependency rule

The target dependency direction is:

```text
Application Shell
      |
      v
Shared Core Contracts <----- Activity Module
      |                           |
      v                           v
Infrastructure Adapters      Activity Rules
```

Forbidden target dependency:

```text
Shared Core ---> Sport Stacking implementation
```

Shared Core must not know that `3-3-3`, `Cycle`, WSSA ID, Child/Parent Doubles, or Special Stacker exist.

The Sport Stacking module may depend on Shared Core contracts.

## 10. Activity capability model

Not every activity has the same concepts. The module system must support capabilities rather than assuming every competition has every Sport Stacking feature.

Examples:

| Capability | Sport Stacking | Chess | Swimming | Athletics |
|---|---:|---:|---:|---:|
| Individual entries | Yes | Yes | Yes | Yes |
| Team entries | Yes | Optional | Relay | Relay/team score |
| Preliminary/final stages | Yes | No/format-dependent | Yes | Yes |
| Timed attempts | Yes | No | Yes | Yes |
| Pairings | No | Yes | No | No |
| Lanes/heats | No | No | Yes | Yes |
| Divisions/categories | Yes | Yes | Yes | Yes |
| Live public results | Yes | Yes | Yes | Yes |
| Certificates | Yes | Yes | Yes | Yes |

The platform should render only the capabilities the active module exposes.

## 11. First proof module: Chess

Chess remains the recommended second activity module because it tests whether the architecture is actually modular.

It differs from Sport Stacking in important ways:

- rounds instead of preliminary timing attempts;
- pairings and opponent identity;
- points instead of best time;
- tie-break systems;
- no Doubles/Relay equivalent in the normal format;
- standings evolve round by round.

If the platform supports Sport Stacking and Chess without adding Chess rules to Shared Core or Sport Stacking rules to Chess, the boundary is healthy.

Chess implementation is not part of Foundation v1 Phase 1.

## 12. Migration strategy

### Phase 1 — Architecture inventory and boundaries

Current phase.

Deliverables:

- this boundary document;
- current-to-target ownership map;
- compatibility rules;
- target dependency rule;
- phased extraction plan.

Runtime impact: none.

### Phase 2 — Module contract and registry foundation

Introduce code-level contracts and a module registry with Sport Stacking as the only registered module.

Requirements:

- no SQL migration if avoidable;
- existing competitions resolve to Sport Stacking through a compatibility default;
- no route changes;
- no public payload change;
- characterization tests prove current behavior.

### Phase 3 — Sport Stacking adapter boundary

Wrap existing Sport Stacking-specific services and frontend engines behind the module contract.

Requirements:

- adapter first, rewrite later if ever;
- existing test suites remain green;
- `FinalsReportEngine.js` remains unchanged unless separately approved;
- no result behavior changes.

### Phase 4 — Shared participant/organization services

Create neutral participant and organization service contracts while retaining current SQL authority.

Potential schema changes must be planned as a separate migration workstream and must not use the DLL-only deployment workflow.

### Phase 5 — Chess proof module

Implement a minimal Chess competition module to validate the platform boundary.

The proof succeeds only if Chess can use Shared Core without changing Sport Stacking rules.

## 13. Database migration boundary

Future modularization may eventually need fields/tables such as:

- competition activity/module code;
- activity module version;
- neutral participant master;
- competition entry records;
- organization master;
- module-specific extension data.

These are intentionally not introduced now.

When required, schema migration must use a separate approved migration process with:

- explicit migration review;
- backup/rollback plan;
- staging validation;
- production authorization;
- post-migration verification.

The existing one-DLL GitHub production deployment workflow must remain migration-free.

## 14. Public API compatibility rule

Existing consumers must not be forced to migrate when the module foundation is introduced.

For Sport Stacking competitions:

- existing authenticated endpoints continue working;
- existing public result route remains stable;
- existing result field meanings remain stable;
- existing SignalR revision behavior remains stable;
- existing SQL authority remains stable.

A future generic API may be added beside existing endpoints before any deprecation is considered.

## 15. Offline compatibility rule

The current Competition Package/offline system is valuable shared infrastructure, but the payload is Sport Stacking-shaped today.

Future package structure should evolve toward:

```text
manifest
sharedCompetition
sharedParticipants
sharedOrganizations
module
  code
  version
  state
sharedResultRevision
```

Existing package format remains valid until a versioned migration path and backward-compatibility tests exist.

## 16. Extraction safety gates

Before any runtime modularization begins:

1. Existing master CI must be green.
2. Sport Stacking characterization tests must cover registration, divisions, teams, results, qualification, awards, reporting, and offline package behavior.
3. Existing public API representative-data tests must remain green.
4. Existing SQL authority and concurrency guards must remain green.
5. No extraction PR may mix architecture refactoring with new activity features.
6. No extraction PR may include a database migration unless that migration is the explicitly approved purpose of the PR.
7. Each extraction step must be independently revertible.
8. Production deployment remains a separate manual decision after merge.

## 17. Definition of Shared Core

A feature belongs in Shared Core only if it remains meaningful without knowing the activity rules.

Use this test:

> Could this service operate correctly for Sport Stacking, Chess, Swimming, and Athletics without branching on their scoring/event rules?

If yes, it is a Shared Core candidate.

If no, it belongs in an activity module or an activity-specific adapter.

Examples:

Shared Core:

- authenticate user;
- create competition;
- assign user to competition;
- upload competition logo;
- allocate result revision;
- publish a competition-changed event;
- translate UI shell text;
- render competition header;
- audit a mutation.

Activity module:

- calculate Sport Stacking division;
- validate Cycle attempts;
- rank Chess players by points/tie-break;
- seed a swimming heat;
- validate an athletics lane result.

## 18. Foundation v1 completion criteria

Foundation v1 Phase 1 is complete when:

- the current architecture is classified into Shared Core, Sport Stacking, transitional compatibility, and infrastructure;
- the dependency direction is documented;
- the conceptual activity module contract is documented;
- the Sport Stacking compatibility strategy is documented;
- the first proof-module strategy is documented;
- no runtime code or database schema is changed;
- full CI passes.

## 19. Next engineering step after this document

The next code phase should be **Modular Platform Foundation v1 Phase 2 — Module Contract & Registry**.

Recommended Phase 2 scope:

1. Add a small activity module abstraction with no database dependency.
2. Register one built-in module: `sport-stacking`.
3. Resolve all existing competitions to `sport-stacking` by compatibility default.
4. Add characterization/static tests proving no route, payload, result rule, or UI behavior changed.
5. Do not move scoring/report code yet.
6. Do not add Chess yet.
7. Do not deploy automatically.

This creates the seam first. Extraction comes later.
