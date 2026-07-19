# Services Design

## Constraints

This is a future design only. It does not change current behavior, rules, XML, localStorage, or DOM. Services should accept explicit inputs, return values or structured errors, and never read DOM elements or hidden globals.

## StorageRepository

**Responsibilities:** Load/save the complete versioned state; preserve the existing key and JSON shape; coordinate defaults, normalization, startup import, reset, and recovery; delegate unchanged XML conversion.

**Public functions:** `load()`, `save(state)`, `reset()`, `applyStartupImport(state, batch)`, `importXml(text)`, `exportXml(state)`.

**Dependencies:** State schema/defaults, StateNormalizer, XmlSerializer, browser storage adapter.

**Future API compatibility:** Replace the browser adapter with HTTP storage while callers retain the same repository boundary. Version/revision tokens require a separately approved decision.

## StateNormalizer

**Responsibilities:** Defaults, compatibility transformations, reference validation, and explicitly derived load-time fields.

**Public functions:** `normalizeState(input)`, `normalizeAwards(input)`, `normalizeDoubles(input)`, `normalizeRelays(input, stackers)`, `normalizeResults(input)`, `validateReferences(state)`.

**Dependencies:** Schema/defaults and narrow pure Division/Team/Result policies.

**Future API compatibility:** Validate server responses and support phased schema migrations.

## SettingsService

**Responsibilities:** Tournament/stage/event settings, availability queries, and advancement configuration.

**Public functions:** `normalizeSettings(settings)`, `isEventGroupEnabled(events, group)`, `availableEntryTypes(settings, events)`, `advanceLimit(settings, type, division)`.

**Dependencies:** Settings schema and event catalog.

**Future API compatibility:** Maps to competition, stage, and competition-event endpoints.

## DivisionService

**Responsibilities:** Age calculation, generated/sorted divisions, standard/special/custom assignment, imported-division preservation, and planned division sets.

**Public functions:** `ageOnDate(dob, start)`, `generateDivisions(settings)`, `assignStackerDivision(stacker, settings, start)`, `recalculateStackers(stackers, settings, start)`, `sortDivisions(divisions)`, `plannedDivisions(state, type)`.

**Dependencies:** Date utilities and division configuration only.

**Future API compatibility:** Run server-side authoritatively and client-side for previews; APIs return IDs while names remain presentation values.

## StackerService

**Responsibilities:** Stacker CRUD, IDs, search/sort, CSV import preview/apply, and cascading-impact plans.

**Public functions:** `createStacker(state, input)`, `updateStacker(state, id, input)`, `deleteStacker(state, id)`, `nextStackerId(stackers)`, `searchStackers(stackers, query)`, `sortStackers(stackers, sort)`, `parseImportCsv(text)`, `applyImport(state, preview)`.

**Dependencies:** DivisionService, Team/Result reference readers, CSV utilities.

**Future API compatibility:** Maps to competition-scoped CRUD and transactional import endpoints.

## TeamService

**Responsibilities:** Shared team identity/membership integrity, Doubles/Child-Parent/Relay validation, conflict plans, completion, location, and divisions.

**Public functions:** `nextTeamId(teams, prefix)`, `validateDoubles(input, state)`, `saveDoubles(state, input)`, `validateRelay(input, state)`, `saveRelay(state, input)`, `deleteTeam(state, id)`, `findMemberships(state, stackerId)`, `isRelayComplete(team)`, `teamDisplayName(team, state)`, `teamDivision(team, stackers, settings)`.

**Dependencies:** DivisionService, stacker read interface, immutable collection utilities.

**Future API compatibility:** Maps to `teams`/`team_members`; membership conflict resolution becomes one competition-scoped server transaction.

## ResultService

**Responsibilities:** Compact IDs, participant resolution, time parsing, blank/scratch semantics, prelim/general results, official time, ranking, and missing prelims.

**Public functions:** `normalizeParticipantId(raw)`, `resolveParticipant(state, id)`, `parseTime(raw)`, `savePrelim(state, command)`, `bestAttempt(result)`, `officialTime(result)`, `missingPrelims(state, settings)`, `rankResults(results, context)`.

**Dependencies:** SettingsService, Team/Stacker read interfaces, event catalog.

**Future API compatibility:** Commands map to result endpoints; calculations run server-side authoritatively and client-side for previews.

## FinalsService

**Responsibilities:** Final sheet grouping/identity, qualifier selection/order, attempt saving, placement, and tie breaks.

**Public functions:** `buildFinalSheets(state)`, `qualifiersFor(sheet, state)`, `advanceLimit(sheet, settings)`, `placeFinalists(results)`, `compareFinalResults(a, b)`, `saveFinalSheet(state, command)`.

**Dependencies:** ResultService, SettingsService, DivisionService, participant readers.

**Future API compatibility:** Sheets/placements become server resources; shared fixtures protect the one authoritative tie-break implementation.

## AwardService

**Responsibilities:** Normalize award settings; calculate planned awards from active structure; keep overall awards separate; produce export rows.

**Public functions:** `normalizePlan(input)`, `calculatePlan(state)`, `calculateTotals(rows)`, `eligibleOverallStackers(state, category)`, `toCsv(rows)`.

**Dependencies:** DivisionService and SettingsService.

**Future API compatibility:** Read-only planning endpoint plus separately stored award configuration.

## ReportService

**Responsibilities:** Competition/admin report data, shared filtering/grouping/ranking/sorting, and export models separated from HTML.

**Public functions:** `buildCompetitionReport(state, query)`, `buildAdminReport(state, query)`, `applyFilters(rows, filters)`, `groupRows(rows, key)`, `sortRows(rows, sort)`, `exportCsv(model)`, `exportExcelHtml(model)`.

**Dependencies:** Read-only Stackers, Teams, Results, Finals, and Divisions interfaces.

**Future API compatibility:** Queries map to endpoints; one report model can feed JSON, HTML, CSV, and Excel output.

## PrintDocumentService

**Responsibilities:** Convert domain data into print models/markup; keep browser print invocation in a UI adapter.

**Public functions:** `individualTimeSheet(stacker, competition)`, `finalTimeSheet(sheet, state)`, `packet(selection, state)`, `bracketModel(config, participants)`.

**Dependencies:** Stackers, Teams, Finals, Settings, and translation message provider.

**Future API compatibility:** Models can later render to server PDF/HTML without changing business services.

## TranslationService

**Responsibilities:** Stable key resolution, defaults/overrides, editing, and completeness checks.

**Public functions:** `normalizePacks(input)`, `translate(language, key, params)`, `updateTranslation(packs, language, key, value)`, `missingKeys(packs, language)`.

**Dependencies:** Translation defaults only; DOM translation remains a UI adapter concern.

**Future API compatibility:** Packs may load from files, API configuration, or user preferences.

## LeaderboardService

**Responsibilities:** Select/rank/limit results by configuration and return a display-neutral model.

**Public functions:** `buildLeaderboard(state, config)`, `normalizeConfig(config)`.

**Dependencies:** ResultService and participant readers.

**Future API compatibility:** Maps to a cacheable endpoint for display devices.

## Mandatory service rules

- Required state and configuration are explicit inputs.
- No service calls `document`, `window`, `localStorage`, `alert`, `confirm`, `window.print`, or download APIs.
- Services return new values or explicit mutation results instead of silently changing unrelated collections.
- Each business rule has one authoritative implementation.
- XML, browser, and API adapters translate only at system boundaries.

