# StackTrack2.0 — Sprint 0 Architecture Review

## Scope and baseline

This is an analysis-only review of the current static application: `index.html`, `app.js`, `styles.css`, `data/*`, `database/*`, and the image assets. No runtime code or existing asset was changed.

The running application is a browser-only single-page application (SPA) built with plain HTML, CSS, and JavaScript. `index.html` contains the shell and page templates, `app.js` contains configuration, state, business rules, rendering, event handling, persistence, import/export, reporting, and printing, and `styles.css` contains both screen and print styling. The SQL files describe a future hosted architecture but are not used at runtime.

---

## 1. Application startup flow

### Load sequence

1. The browser parses `index.html`.
2. The permanent application shell is created: sidebar, navigation container, top bar, dashboard hero, toolbar, and `#view` outlet.
3. Twelve `<template>` elements are parsed but remain inert: dashboard, settings, language, stackers, doubles, relay, paperwork, awards, competition, reports, leaderboard, and users.
4. `styles.css` loads and supplies the shared layout, controls, tables, reports, paperwork, responsive rules, and print rules.
5. `data/stacktrack-4257-stackers.js` runs first and assigns an import object to `window.stackmeetImport4257`.
6. `app.js` runs as a classic, non-module script. All top-level declarations and functions share the page script scope.
7. Static configuration objects and dictionaries are created, followed by the `demo` state.
8. `state` is initialized synchronously by `loadState()`.
9. `loadState()` reads the single localStorage record, parses it, passes it through `applyStartupImport()`, and then through `normalizeState()`. On any exception it repeats that process from a clone of `demo`.
10. `route` is initialized from `location.hash`, defaulting to `dashboard`. Remaining UI session globals are initialized.
11. Permanent document-level click/change handlers, hash-change handling, XML import/export handlers, and reset handling are registered.
12. The final top-level call to `render()` paints the active route.

### Startup import behavior

`applyStartupImport(data)` inspects `window.stackmeetImport4257`. It imports the bundled stackers only when the current data still looks like the demo state. Importing replaces `stackers`, clears `doubles`, `relays`, and `results`, records `importBatch`, and immediately writes the result to localStorage. This is startup-time state mutation, not a passive seed lookup.

### Render startup behavior

`render()` performs the common render pipeline:

1. Reject an unavailable Doubles/Relay route and fall back to the dashboard.
2. Build navigation with `renderNav()`.
3. Select the matching template from `routes`/the route name.
4. Clone the template into `#view`.
5. Update title/hero visibility and translate the cloned content.
6. Apply event-driven menu/control visibility.
7. Dispatch to the route-specific renderer.
8. Translate dynamic content again.
9. Persist the entire state with `saveState()`.

Important consequence: ordinary navigation and rendering perform a localStorage write even when the user has not changed business data.

---

## 2. Navigation flow

Navigation is hash-based and implemented without a router library.

- `routes` is an array of `[routeKey, label, badge]` tuples.
- `renderNav()` converts available route tuples into anchors such as `href="#stackers"` and adds `data-route`.
- `routeIsAvailable()` removes Doubles and Relay routes when their event groups are disabled.
- The document click delegate intercepts `[data-route]`, assigns `route`, and calls `render()` immediately.
- The browser also updates the URL hash; the `hashchange` handler rereads it and calls `render()` again.
- Direct URL/hash loading works because startup initializes `route` from `location.hash`.
- Route templates follow the convention `${route}View`.
- Route-specific rendering is selected by a long conditional chain inside `render()`.

### Navigation coupling

Navigation depends directly on:

- `state.events` through `routeIsAvailable()` and `eventGroupEnabled()`.
- Translation state through `t()`, `languageLabel()`, and `navBadgeText()`.
- Template IDs embedded in `index.html`.
- Route-specific global render functions.
- Three cached DOM elements: `view`, `pageTitle`, and `hero`.

There is no route lifecycle abstraction. Route setup, DOM population, listener registration, translation, visibility rules, and persistence are coordinated centrally by `render()`.

---

## 3. State lifecycle

### State creation

`demo` is the complete default state and acts simultaneously as:

- Demonstration data.
- Initial state schema.
- Default-value source.
- XML import fallback/template.
- Reset target.

It contains `settings`, `translations`, `leaderboard`, `awards`, `events`, `divisionSettings`, `divisions`, `stackers`, `doubles`, `relays`, `results`, `notifications`, and `users`.

### Loading

`loadState()`:

```text
localStorage.getItem(storageKey)
  -> JSON.parse or structuredClone(demo)
  -> applyStartupImport
  -> normalizeState
  -> state
```

The entire operation is wrapped in one broad `try/catch`. Invalid JSON, normalization errors, missing browser APIs, or startup-import failures all fall back to the demo path without preserving or reporting the corrupt value.

### Normalization

`normalizeState(data)` mutates the supplied aggregate and establishes runtime invariants:

- Merges settings defaults.
- Rebuilds translation packs from defaults plus saved overrides.
- Reduces prelim rounds to `"0"` or `"1"`.
- Merges division settings and ensures `custom`/`special` arrays.
- Normalizes awards and allowed limits/items.
- Regenerates division names from cutoff settings.
- Adds default stacker fields and recalculates ages/divisions.
- Normalizes legacy Doubles and Relay shapes.
- Normalizes Relay result type names.
- Re-adds standard divisions found in imported stackers.

Normalization is both schema repair and business recalculation. Loading saved data can therefore change derived divisions and normalize legacy values before the first render.

### Saving

`saveState()` serializes the entire global `state` into one localStorage value:

- Key: `stackmeet-stacktrack-style-v1`
- Value: JSON representation of the full aggregate

Most mutations are performed directly against `state`. The central click handler calls `render()` for many actions, and `render()` saves. For actions that intentionally avoid rerendering, the handler calls `saveState()` directly. This convention is implicit rather than enforced.

### Local UI state outside persisted state

Editing IDs, selected tabs, report sort, active entry sheets, flash messages, and print orientation are top-level variables and are lost on refresh. They are not part of the persisted tournament state.

### XML export

The Export XML toolbar handler calls `stateToXml(state)`, creates a Blob, temporarily creates a download link, and downloads `stackmeet-data.xml`.

`stateToXml()` manually serializes every major state collection. It owns knowledge of all XML tag names and model shapes. Data is escaped by `xmlEsc()`/`xmlAttr()`.

### XML import

The file input reads text and calls `xmlToState()`:

1. Parse with `DOMParser`.
2. Validate the root/parser error.
3. Clone `demo` as the import base.
4. Map XML nodes into settings and collections.
5. Convert selected numeric/boolean values.
6. Pass the imported aggregate through `normalizeState()`.
7. Replace global `state` and rerender/save.

The XML reader and writer are manually paired rather than schema-driven. XML import is replacement, not merge. Access passwords are intentionally absent. The future SQL mapping retains XML as a migration/export format.

### Future SQL state model

`database/schema.sql` defines 20 relational tables plus indexes for competitions, access, stages, events, divisions, stackers, organizations, teams, sheets, results/attempts, leaderboards, sessions, notifications, and paperwork jobs. `MULTI_TOURNAMENT_MODEL.md` requires all tournament queries to scope by the internal `competition_id`, while preserving a human-facing `public_code`. None of this database layer is connected to the current application.

---

## 4. Function dependency map

The map below groups direct and important transitive dependencies. Utility-only calls such as `esc()` are omitted where repeating them would obscure the domain relationships.

### Startup, state, and shell

```text
loadState
  -> localStorage
  -> applyStartupImport
       -> window.stackmeetImport4257
       -> defaultDivisionSettings, demo, storageKey
  -> normalizeState
       -> demo/default packs/default settings
       -> normalizeAwards
       -> generateDivisionNames
       -> recalculateStackerDivisions
       -> normalizeDoubles / normalizeRelays / normalizeResults
       -> appendStandardImportedDivisions

render
  -> routeIsAvailable -> eventGroupEnabled -> state.events
  -> renderNav -> routes, route, navBadgeText -> translations
  -> template lookup in index.html
  -> applyTranslations / applyEventMenuVisibility
  -> route-specific render function
  -> saveState -> localStorage
```

`render()` is the primary orchestration hotspot because it knows every route, renderer, translation pass, visibility rule, and persistence timing.

### Translation

```text
currentLanguage -> state.settings.language
languageLabel -> static language mapping
t -> currentLanguage + state.translations
translateChrome -> t + fixed shell DOM
applyTranslations -> t + DOM text traversal
renderLanguage -> drawLanguageRows
drawLanguageRows -> translation dictionaries + language search DOM
saveLanguage -> language form DOM + state.translations
```

Translation is tightly coupled to rendered English text and DOM traversal rather than stable message keys.

### Settings, events, and divisions

```text
renderSettings
  -> state.settings/events/divisionSettings
  -> renderDivisionCutoffs
       -> divisionAges, countBadges, refreshDivisionCountBadges

saveSettings
  -> settings form DOM
  -> normalizePrelimRounds
  -> recalculateStackerDivisions

saveEvents
  -> event checkbox DOM -> state.events

saveDivisions
  -> readDivisionSettingsFromForm
  -> generateDivisionNames
  -> recalculateStackerDivisions

divisionForStacker/findDivisionFor
  -> ageOnCompetitionDate
  -> findRangeName/divisionPath
  -> division settings and competition start date

generateDivisionNames
  -> divisionRanges
  -> standardCombinedDivisionNames
  -> dedupeDivisions/sortedDivisions
  -> divisionSortInfo
```

Division behavior is highly interconnected with stacker registration, imported data, team divisions, reports, finals grouping, and awards planning.

### Stackers

```text
renderStackers
  -> populateCustomDivisionOptions
  -> drawStackerRows
       -> sortStackers/compareStackers/stackerIdNumber
       -> doublesForStacker/relayForStacker

addStacker
  -> form values
  -> ageOnCompetitionDate/stackerDivisionFromForm
  -> nextStackerCode
  -> state.stackers mutation

loadStackerForEdit/clearStackerForm/syncStackerEditState
  -> stacker form DOM + editingStackerId

deleteStacker
  -> state.stackers
  -> removes/updates related doubles, relays, results

saveStackerDoubleAssignment
  -> selected stacker
  -> validateDoubleEntry
  -> removeConflictingDoubles
  -> generatedDoublesDivision
  -> state.doubles

importStackersCsvFile
  -> parseCsv -> mapStackTrackCsvRow
  -> appendStandardImportedDivisions
  -> clears teams/results
```

Stacker deletion and CSV import are high-coupling operations because they modify multiple state collections.

### Doubles and Relay teams

```text
renderDoubles
  -> syncDoubleEditState
  -> populateDoubleSelects/fillDoubleSelect
  -> filteredDoublesForTab/completedDoubles
  -> doubleTeamName/doubleDivision/teamCountry

addDouble
  -> detectedDoubleType
  -> validateDoubleEntry
  -> removeConflictingDoubles
  -> generatedDoublesDivision
  -> nextTeamCode("2")

renderRelay
  -> buildRelayMemberControls
  -> syncRelayEditState
  -> populateRelaySelects/fillRelaySelect
  -> selectedRelayMemberIds/warnings
  -> filteredRelaysForTab/completedRelays

addRelay
  -> selectedRelayMemberIds
  -> validateRelayEntry
  -> removeConflictingRelays
  -> generatedRelayDivision
  -> relayCountryForMembers/relayRegionForMembers
  -> nextTeamCode("3")

team helpers
  -> stacker lookups in global state
  -> age/division helpers
```

The Doubles and Relay paths implement parallel selection, validation, conflict removal, form synchronization, editing, deletion, completion, and location logic, but do not share a team service abstraction.

### Prelims, finals, and rankings

```text
renderCompetition
  -> populateEntryTypeOptions/populateParticipants
  -> populateFinalSheetSelect
  -> attach entry listeners
  -> updateCompetitionEntryMode
  -> drawResultRows/drawMissingTimes

loadPrelimParticipant
  -> normalizePrelimEntryId
  -> resolvePrelimParticipant
  -> typeEventGroup + eventGroupEnabled
  -> existing state.results

savePrelimResults
  -> active participant globals
  -> visiblePrelimTimeInputs/parseCompetitionTime
  -> state.results replacement/insertion
  -> drawResultRows/drawMissingTimes

finalSheets
  -> completedDoubles/completedRelays
  -> enabled events/divisions/results
  -> finalSheetQualifiers
       -> finalAdvanceLimit/finalEntryType
       -> result ranking

loadFinalSheet
  -> finalSheets
  -> drawFinalSheetRows
       -> finalResultFor/finalResultInputValues
       -> finalParticipantSubline

saveFinalResults
  -> finalDraftResults
  -> finalPlacements -> compareFinalResults -> finalTieBreakKey
  -> state.results replacement/insertion

bestAttempt -> official -> bestResults
```

This is the most business-critical dependency cluster. It depends on event configuration, division calculation, participant/team completeness, advancement settings, ID normalization, scratch semantics, ranking rules, and global UI session state.

### Awards

```text
renderAwards
  -> fillAwardLimitSelect/award controls
  -> drawAwardSummary

saveAwards -> form DOM -> normalizeAwards -> drawAwardSummary

awardPlanRows
  -> individualAwardRows/doublesAwardRows/relayAwardRows/overallAwardRows
  -> planned*AwardDivisions
  -> plannedEventsForGroup
  -> awardRowsForPlaces/groupItemsByValue

planned divisions/events
  -> state.divisions/divisionSettings/events
```

Awards intentionally depend on planned configuration rather than current registrations. Changes to division-name generation or event availability can alter award quantities.

### Reports

```text
renderReports
  -> renderReportTabs
  -> populateCompetitionReportBuilder/runCompetitionReport
  -> populateReportBuilder/runReport

competitionReportRows
  -> eventRows/allAroundRows
  -> competitionRowFromResult
       -> competitionParticipantMeta
  -> applyCompetitionFilters
  -> limitGroupedRows/rankCompetitionRows
  -> competitionReportTable

buildAdminReportData
  -> reportRowsForType
  -> filterReportRows
  -> selectedReportColumns/reportValue
  -> sortAdminReport

export paths
  -> current report data
  -> csvLine/downloadText or Excel HTML
```

Competition and admin reporting are separate pipelines with overlapping participant lookup, filtering, sorting, grouping, table rendering, and exporting responsibilities.

### Printing, leaderboard, and utility layer

```text
renderPaperwork -> print range options + event visibility
buildPaperwork
  -> selectedStackersForPrintRange/individualTimeSheetHtml
  -> buildFinalPaperwork -> finalSheets/finalTimeSheetHtml
print* -> printTimeSheetTarget -> DOM state + window.print
buildBracket -> bracket form DOM + generated placeholder HTML

renderLeaderboard
  -> bestResults -> official/bestAttempt
  -> participantName

participantName/team naming/location helpers
  -> state.stackers/doubles/relays

stateToXml/xmlToState
  -> every persisted domain shape
  -> XML helpers
```

`stateToXml()`, `xmlToState()`, `participantName()`, and generic team/participant helpers are cross-domain coupling points.

---

## 5. Global variables

All explicit top-level variables in `app.js` are listed below. Top-level function declarations are also globally scoped within the classic script, but are covered by the dependency and module sections.

### Configuration and lookup constants

| Global | Purpose |
|---|---|
| `storageKey` | Single localStorage key for the full application state. |
| `eventGroups` | Canonical event names grouped by Individuals, Doubles, Timed Relay, and Head-to-Head. |
| `prelimEntryConfig` | Maps compact ID prefixes to participant type and eligible prelim events. |
| `prelimEventFieldIds` | Maps event names to prelim input element IDs. |
| `countries` | Country options used by forms/report filters. |
| `reportPresets` | Common administrative-report configurations. |
| `competitionReportPresets` | Common competition-result/SOC/all-around configurations. |
| `reportColumns` | Administrative report column metadata and type availability. |
| `defaultMalayTranslations` | Default Bahasa Malaysia message dictionary. |
| `defaultChineseTranslations` | Default Simplified Chinese message dictionary. |
| `defaultTranslationPacks` | Language-code mapping to default dictionaries. |
| `divisionAges` | Generated selectable ages from 4 through 105. |
| `monthNames` | Month-name lookup used in date normalization. |
| `defaultDivisionSettings` | Default combined/male/female/special/custom cutoff configuration. |
| `defaultAwards` | Default places, items, Relay units, and overall awards. |
| `awardOverallGroups` | Metadata for overall award categories. |
| `demo` | Complete default/demo state and import/reset template. |
| `routes` | Route key, display label, and badge metadata. |

### Mutable application and UI-session globals

| Global | Purpose |
|---|---|
| `state` | Authoritative in-memory tournament aggregate loaded from localStorage/demo. |
| `route` | Current hash route key. |
| `flashMessage` | General transient stacker/UI message. |
| `editingStackerId` | Stacker currently being edited. |
| `pendingDeleteStackerId` | Stacker awaiting custom delete confirmation. |
| `stackerSort` | Current stacker table sort key/direction. |
| `reportTab` | Active Competition/Admin report tab. |
| `adminReportSort` | Current admin report column/direction. |
| `adminPrintOrientation` | Current admin print orientation. |
| `activePrelimParticipantId` | Participant loaded in compact prelim entry. |
| `activePrelimParticipantType` | Type of the loaded prelim participant. |
| `activeFinalSheetId` | Finals sheet currently loaded for entry/printing. |
| `doublesTab` | Completed or incomplete Doubles tab. |
| `doubleFlashMessage` | Transient Doubles validation/status message. |
| `editingDoubleId` | Doubles team currently being edited. |
| `stackerDoubleEditorOpen` | Whether the embedded stacker-to-Doubles editor is expanded. |
| `relayTab` | Completed or incomplete Relay tab. |
| `relayFlashMessage` | Transient Relay validation/status message. |
| `editingRelayId` | Relay team currently being edited. |

### Cached DOM globals

| Global | Purpose |
|---|---|
| `view` | Main template outlet (`#view`). |
| `pageTitle` | Top-bar page title element. |
| `hero` | Dashboard hero element whose visibility changes by route. |

### External global

`window.stackmeetImport4257` is defined by `data/stacktrack-4257-stackers.js` and consumed by `applyStartupImport()`. Browser globals used directly include `window`, `document`, `location`, `localStorage`, `crypto`, `DOMParser`, `Blob`, `URL`, `alert`, and `confirm`.

---

## 6. Module candidates

These are recommended boundaries only. No extraction is performed in Sprint 0.

### Application Shell / Router

**Functions:** `render`, `renderNav`, `navBadgeText`, `routeIsAvailable`, `applyEventMenuVisibility`, `pruneSelectOptions`.

**Responsibilities:** Route state, template mounting, common render lifecycle, navigation availability, shell labels, route dispatch.

**Dependencies:** Event settings, Translation, DOM templates, every route module, Storage save timing.

### Storage and Serialization

**Functions:** `loadState`, `applyStartupImport`, `normalizeState`, `saveState`, `stateToXml`, `xmlToState`, `xmlEsc`, `xmlAttr`.

**Responsibilities:** Default state creation, persistence, import seed handling, schema normalization, XML backup/restore.

**Dependencies:** Defaults, Settings, Divisions, Teams, Results, Awards, Translation, browser storage/file APIs.

### Settings and Events

**Functions:** `renderSettings`, `saveSettings`, `saveEvents`, `eventGroupEnabled`, `normalizePrelimRounds`.

**Responsibilities:** Tournament metadata, stage/event settings, advancement counts, paperless/time-sheet preferences.

**Dependencies:** State, Stackers division recalculation, Navigation availability, Results/finals, Awards, Print Center.

### Divisions

**Functions:** `renderDivisionCutoffs`, `countBadges`, `refreshDivisionCountBadges`, `divisionCountSummary`, `divisionTargetForAge`, `cutoffTarget`, `saveDivisions`, `readDivisionSettingsFromForm`, `generateDivisionNames`, `sortedDivisions`, `compareDivisionNames`, `divisionSortInfo`, `divisionGroupSort`, `masterStartAge`, `masterEndAge`, `divisionRanges`, `dedupeDivisions`, `officialNameForCombinedRange`, `isOfficialAdultDivision`, `standardCombinedDivisionName`, `standardCombinedDivisionNames`, `masterLevelForAge`, `addDivision`, `removeDivision`, `divisionForStacker`, `findDivisionFor`, `findRangeName`, `divisionPath`.

**Responsibilities:** Cutoff configuration, generated/custom division names, sorting, assignment, preview/counts.

**Dependencies:** Settings dates, Stackers, Teams, Awards, Results grouping, Reports.

### Stackers

**Functions:** `renderStackers`, `populateCustomDivisionOptions`, `drawStackerRows`, `sortStackers`, `compareStackers`, `stackerIdNumber`, `updateSortHeaders`, `sortStackerTable`, `updateStackerDivisionPreview`, `stackerDivisionFromForm`, `isSpecialStacker`, `recalculateStackerDivisions`, `syncStackerEditState`, `importStackersCsvFile`, `appendStandardImportedDivisions`, `mapStackTrackCsvRow`, CSV classification/cleaning helpers, `parseCsv`, `addStacker`, `loadStackerForEdit`, `clearStackerForm`, delete-confirmation functions, `deleteStacker`, `nextStackerCode`.

**Responsibilities:** Registration, editing, import, deletion, display/sort/search, division assignment.

**Dependencies:** Storage state, Settings date, Divisions, Teams, Results, Reports, Print Center.

### Teams

**Functions:** all `renderDoubles`/Doubles form, selection, warning, validation, conflict, CRUD and naming/division helpers; all `renderRelay`/Relay member, warning, validation, conflict, CRUD and location/division helpers; `nextTeamCode`, `teamCountry`, `teamRegion`, `relayForStacker`, `doublesForStacker`, `registeredDoubleMemberIds`, `relayMemberIds`.

**Responsibilities:** Doubles and Relay lifecycle, membership integrity, completion, team identities, generated divisions/locations.

**Dependencies:** Stackers, Divisions, Settings, Results, Reports, Print Center.

### Results / Prelims

**Functions:** `renderCompetition` prelim path, `populateEntryTypeOptions`, `availableEntryTypes`, `updateCompetitionEntryMode`, `loadPrelimParticipant`, compact-ID resolution helpers, prelim field/input helpers, time parsing/normalization, `savePrelimResults`, `drawResultRows`, `drawMissingTimes`, `missingPrelimGroups`, `loadMissingPrelim`, legacy `saveResult`, `bestAttempt`, `official`, `bestResults`.

**Responsibilities:** Participant lookup, prelim time entry, scratch handling, result storage, missing-time monitoring, ranking primitives.

**Dependencies:** Settings/events, Stackers, Teams, shared state, Finals, Reports, Leaderboard.

### Finals

**Functions:** `populateFinalSheetSelect`, `finalSheets`, final group sorting/qualifier/advance helpers, missing summary, sheet loading/drawing, final result input helpers, final computation/draft/placement/tiebreak functions, `saveFinalResults`, clearing/messages, final printing HTML.

**Responsibilities:** Generate finalist sheets, order qualifiers, accept attempts, calculate placements/ties, persist finals.

**Dependencies:** Results, Settings advancement counts, Events, Stackers, Teams, Divisions, Print Center, Reports.

### Awards

**Functions:** `renderAwards`, award control/normalization functions, `saveAwards`, `drawAwardSummary`, all award row/planning/grouping functions, `eligibleOverallStackers`, `exportAwardsCsv`, `ordinal`.

**Responsibilities:** Plan award quantities from active competition structure, configure items/places, summarize/export.

**Dependencies:** Settings/events, Divisions, Stackers for separate overall eligibility, Utilities/export.

### Reports

**Functions:** all `renderReports`, report-tab, competition-report, admin-report, preset, filter, ranking, grouping, table, sort, and export functions from `renderReports` through `handleReportTypeChange`.

**Responsibilities:** Result reporting, operational reports, filters/presets, ranking, grouping, presentation, print/export.

**Dependencies:** Every core data domain, Translation labels, Utilities/download, DOM controls.

### Print Center

**Functions:** `renderPaperwork`, `buildPaperwork`, `buildFinalPaperwork`, `selectedStackersForPrintRange`, `individualTimeSheetHtml`, `printSingleStackerSheet`, `printPaperPreview`, `printTimeSheetTarget`, `buildBracket`, final sheet HTML/print functions.

**Responsibilities:** On-screen document previews, individual/final sheets, bracket placeholder, browser printing and print cleanup.

**Dependencies:** Settings, Events, Stackers, Teams, Finals, Translation, CSS print selectors.

### Translation

**Functions:** `currentLanguage`, `languageLabel`, `t`, `translateChrome`, `applyTranslations`, `renderLanguage`, `drawLanguageRows`, `saveLanguage` plus translation constants.

**Responsibilities:** Language selection, dictionaries, editing translations, shell/template/dynamic text replacement.

**Dependencies:** State/Storage, DOM text structure, all modules that produce user-facing English.

### Leaderboard

**Functions:** `renderLeaderboard`, `saveLeaderboard`, `bestResults`, `participantName`, `fmt`.

**Responsibilities:** Configure and render a result display board.

**Dependencies:** Results/ranking, Stackers/Teams, Settings, DOM/CSS.

### Utilities

**Functions:** `countBy`, `groupCounts`, `participantName`, stacker/team label/search helpers, date helpers, ID generators, `fmt`, `setOptions`, `setValue`, `val`, `csvLine`, `downloadText`, `selectedOptionText`, `slugify`, `splitName`, `esc`, `cssEscape`.

**Responsibilities:** Escaping, DOM values/options, formatting, dates, IDs, labels, grouping, downloads.

**Dependencies:** Some are pure; participant/team and ID helpers currently depend directly on global state and should be classified as domain services rather than generic utilities when eventually extracted.

---

## 7. Technical debt, ranked

### Critical

1. **No automated tests for competition rules.** Finals qualification, tie-breaking, age/division assignment, special divisions, team conflicts, awards counts, XML round trips, and scratch handling can regress silently. These rules determine official competition outcomes.
2. **Single browser-local persistence record with no recovery/version migration.** One malformed or incompatible record causes silent fallback to demo/import behavior. There is no transaction boundary, revision history, conflict detection, or durable server copy.
3. **Startup import can destructively replace operational collections.** `applyStartupImport()` may replace stackers and clear teams/results based on a heuristic that data “looks like demo.” This occurs automatically during initialization and immediately saves.

### High

1. **`app.js` is a 4,636-line monolith.** All domains share global state and DOM knowledge; changes have a wide blast radius.
2. **`render()` mixes navigation, mounting, translation, domain rendering, visibility, and persistence.** A render failure can affect routing and saving; render-only navigation writes state.
3. **Business rules are coupled to DOM and global mutable state.** Important calculations are difficult to test independently and can depend on current form/route state.
4. **Finals depend on many implicit invariants.** Participant completeness, event enablement, division naming, prelim result shape, advancement settings, scratch representation, and tie rules converge in one workflow.
5. **Manual XML reader/writer pair owns the whole schema.** Adding or renaming a state field requires coordinated edits in defaults, normalization, serializer, parser, reports, and possibly SQL mapping.
6. **Destructive operations span collections without transactions.** Stacker deletion, team conflict removal, CSV import, and reset can update several collections; partial runtime failures have no rollback.
7. **Future SQL model and live state model can drift.** The database schema is comprehensive, but no executable mapper, API contract, or migration tests enforce alignment with the JavaScript/XML model.

### Medium

1. **Central action dispatcher is oversized.** Roughly 60 action conditions share one listener, making ownership and control flow harder to trace.
2. **Parallel report implementations duplicate concepts.** Competition and admin reports independently implement filtering, metadata lookup, sorting, grouping, HTML, and export behavior.
3. **Doubles and Relay management repeat team patterns.** Selection, conflicts, completion, CRUD, and form state can diverge.
4. **Route renderers repeatedly bind DOM listeners.** Correctness depends on template replacement removing old elements; there is no explicit mount/unmount lifecycle.
5. **Translation relies on English text matching and DOM traversal.** Small wording/markup changes can break translation; dynamic HTML remains partially untranslated.
6. **`demo` has too many responsibilities.** It is sample content, schema default, recovery fallback, import base, and reset state.
7. **Normalization mutates and recalculates during load.** Persistence migration, validation, and derived business logic are combined, making data changes less observable.
8. **Legacy and modern result-entry paths coexist.** `saveResult()` and the compact prelim/final workflows use different validation and entry assumptions.
9. **Print generation is partly generic and tightly coupled to CSS selectors.** A class/DOM change can break targeted printing or preview cleanup.
10. **Browser print cleanup uses both `afterprint` and a one-second timer.** Slow/blocked print dialogs can create timing inconsistencies.
11. **`normalizeDoubles()` contains duplicate property assignments and initializes `division` through a Relay helper.** Later spread/order often masks the issue, but intent and fallback behavior are fragile.
12. **No explicit domain schema validation.** Arrays, IDs, references, dates, and enum-like values are repaired opportunistically rather than validated with actionable errors.

### Low

1. **Classic script/global namespace.** Naming collisions and hidden dependencies become more likely as the application grows.
2. **Large embedded translation dictionaries and HTML template strings reduce scanability.** They obscure control flow but do not directly break behavior.
3. **Hard-coded DOM IDs and string conventions are widespread.** Renaming UI elements requires manual cross-file coordination.
4. **Mixed naming conventions exist for Relay (`Relay` versus `Timed Relay`) and division labels.** Normalization compensates, but conceptual overhead remains.
5. **Utility functions mix pure helpers and stateful domain lookups.** Module placement is unclear.
6. **No build/lint/format pipeline.** Syntax can be checked, but consistency and dead-code analysis are manual.
7. **CSS is a single 1,631-line stylesheet.** Screen components, reports, documents, responsive behavior, and print rules share one cascade.
8. **Static assets have no documented sizing/usage contract.** This is low risk now but matters for generated paperwork and branding.

---

## 8. Refactoring roadmap

The extraction order should reduce risk by establishing safety and seams before touching rule-heavy workflows.

1. **Characterization tests and state fixtures first.** Capture current behavior for state normalization, XML round trips, compact IDs, divisions, teams, ranking, finals, and awards. This is preparation, not feature work.
2. **Pure Utilities.** Extract escaping, formatting, CSV, date parsing, basic grouping, and non-stateful ID parsing. This has the lowest domain risk and clarifies what is genuinely reusable.
3. **State schema, defaults, and Storage.** Separate demo content from defaults; define a version; isolate localStorage and XML behind explicit load/save/import/export boundaries.
4. **Translation.** Move dictionaries and key lookup out of the application orchestration while retaining current output behavior.
5. **Settings/Events and Divisions.** Extract configuration and pure division-generation/assignment rules. These are upstream dependencies for most competition modules.
6. **Stackers.** Separate registration/import CRUD and division calculation from rendering.
7. **Teams.** Establish a shared membership service, then isolate Doubles and Relay policies without forcing them into one identical model.
8. **Results primitives and Prelims.** Extract time parsing, scratch semantics, result identity, official-time calculation, and prelim completeness.
9. **Finals.** Move qualifier generation, sheet identity, ordering, tie-breaking, and placements only after upstream behavior is tested and stable.
10. **Awards.** Extract planned-structure calculations and exports.
11. **Reports.** Build a shared data/query pipeline, then retain separate competition/admin presentation adapters.
12. **Print Center.** Extract document models and renderers from UI routing; preserve current browser print behavior initially.
13. **Leaderboard.** Consume the extracted result query/ranking interfaces.
14. **Application Shell/Router last.** Once route modules expose clear mount/render operations, simplify `render()` and action dispatch without changing URLs or templates.
15. **Database/API adapter after the client boundaries stabilize.** Implement the existing SQL model through the Storage/repository boundary rather than wiring SQL concepts directly into views.

---

## 9. Risk assessment

### Very high risk

- **Finalist generation and placement:** official results depend on limits, grouping, scratches, tie-break attempts, participant identity, and stage/type naming.
- **Division calculation:** changes affect registration, team divisions, awards, finals sheets, reports, and imported stackers.
- **State normalization/import:** runs before the UI appears and can rewrite derived data or trigger fallback behavior.
- **XML import/export:** model-wide and easy to make asymmetric, causing silent field loss.
- **Stacker deletion/import replacement:** affects stackers, team memberships, results, and division lists.
- **Startup bundled import:** heuristic and destructive to teams/results when triggered.

### High risk

- **Team conflict handling:** editing one team can remove or change other memberships.
- **Awards planning:** quantities depend on planned divisions and enabled event categories, not simply registration counts.
- **Competition reports:** combine multiple stages/types and recalculate ranks/grouping under filters.
- **Navigation/render lifecycle:** a central change touches every screen and persistence timing.

### Medium risk

- **Print CSS and generated markup:** screen previews and browser print output depend on matching selectors and temporary body state.
- **Translation:** text-node replacement can be affected by markup or wording changes.
- **Admin reports:** broad model dependency but less impact on official result storage.
- **CSV import:** external header variations and replacement behavior require careful regression coverage.

### Lower risk

- **Dashboard metrics and notifications display.** Mostly derived/read-only presentation.
- **Users display.** Currently static/read-only.
- **Static visual styling outside print rules.** Usually localized impact, though the shared cascade should still be checked.
- **Image assets and database documentation.** Not connected to current runtime behavior.

---

## 10. Engineering score

Scores describe the current prototype architecture, not the value or completeness of its competition features.

| Category | Score | Rationale |
|---|---:|---|
| Architecture | 4/10 | Clear domain concepts and a workable SPA shell, but nearly all runtime concerns reside in one global script. |
| Readability | 6/10 | Function names and domain terminology are generally clear; size, embedded HTML/dictionaries, and implicit dependencies slow comprehension. |
| Maintainability | 4/10 | Direct global mutations and cross-domain coupling make changes risky despite many focused helper functions. |
| Scalability | 3/10 | Single-browser storage and full-state rendering cannot support multi-user or hosted competition operations; future SQL design is promising but disconnected. |
| Performance | 7/10 | Current dataset size is manageable and browser operations are generally simple; whole-state serialization and repeated full rendering will become costly at larger scale. |
| Testing | 1/10 | No automated test suite or testable module boundary was found. |
| UI Separation | 3/10 | HTML templates exist, but render logic, DOM listeners, business rules, and persistence remain mixed in `app.js`. |
| Business Logic | 7/10 | Substantial competition logic is implemented with explicit helpers, but it lacks isolation, formal invariants, and regression tests. |
| State Management | 4/10 | Central state and normalization are understandable, but mutation/save conventions, recovery, versioning, and destructive imports are weak. |
| Extensibility | 4/10 | New functions can be added quickly, but every additional module increases monolith size and cross-domain risk. |

**Overall baseline: 4.3/10.** The application has meaningful operational depth and a credible future database design, but architectural safety has not caught up with the feature set.

---

## Recommended Sprint 1 plan

Sprint 1 should remain behavior-preserving and focus on establishing engineering safety before feature development.

1. Record representative JSON/XML fixtures for empty, demo, imported 4257, special-stacker, Doubles, Relay, prelim, final, scratch, and tie scenarios.
2. Add a lightweight automated test harness using the existing bundled Node runtime.
3. Write characterization tests for age/division rules, compact ID normalization, team conflicts, official-time calculation, finalist selection/order, final tie-breaking, award quantities, and XML round trips.
4. Define and document a versioned state schema, including persisted versus session-only fields and reference integrity rules.
5. Specify storage interfaces (`load`, `save`, `reset`, `import`, `export`) and error/recovery behavior without changing the current localStorage implementation yet.
6. Document the dependency direction to enforce during later extraction: Utilities/Schema -> Storage and domain rules -> UI modules -> Application Shell.
7. Establish a repeatable validation checklist: `node --check app.js`, automated tests, fixture round trips, and manual smoke checks for every route and print preview.

### Sprint 1 exit criteria

- Existing user-facing behavior remains unchanged.
- No new competition features are introduced.
- High-risk business rules have executable regression coverage.
- State format and recovery behavior are documented.
- Later module extraction can begin with measurable confidence rather than relying on manual inspection alone.
