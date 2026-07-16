"use strict";

// Sprint 5 characterization harness. It runs the legacy browser script without
// changing it and asserts observable outcomes through a deliberately small hook.
const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

function element() {
  return {
    value: "", checked: false, hidden: false, innerHTML: "", textContent: "", dataset: {}, style: {},
    classList: { toggle() {}, add() {}, remove() {} },
    addEventListener() {}, focus() {}, select() {}, remove() {}, append() {}, appendChild() {},
    querySelector() { return null; }, querySelectorAll() { return []; }, setAttribute() {}, click() {}
  };
}

function loadLegacyApp() {
  const elements = new Map();
  const document = {
    getElementById(id) { if (!elements.has(id)) elements.set(id, element()); return elements.get(id); },
    querySelector() { return null; }, querySelectorAll() { return []; }, addEventListener() {},
    createElement() { return element(); }, body: { appendChild() {} }
  };
  const context = {
    console, document, location: { hash: "" }, confirm: () => true, alert() {},
    crypto: { randomUUID: () => "test-uuid" }, structuredClone, URL: { createObjectURL() { return "blob:test"; }, revokeObjectURL() {} }, Blob,
    window: { addEventListener() {}, open() { return { document: { write() {}, close() {} }, focus() {}, print() {}, close() {} }; } }
  };
  context.globalThis = context;
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "storage", "ApiProvider.js"), "utf8"), context, { filename: "ApiProvider.js" });
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "storage", "StackerApi.js"), "utf8"), context, { filename: "StackerApi.js" });
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "storage", "Repository.js"), "utf8"), context, { filename: "Repository.js" });
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "results", "BestResultEngine.js"), "utf8"), context, { filename: "BestResultEngine.js" });
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "reports", "FinalsReportEngine.js"), "utf8"), context, { filename: "FinalsReportEngine.js" });
  let source = fs.readFileSync(path.join(__dirname, "..", "app.js"), "utf8");
  source = source.replace(/\n(?:void\s+)?initializeApplication\(\)(?:\.catch\(showBootError\))?;\s*$/, "\n");
  source += `\nglobalThis.__stackMeetHooks = {
    getState: () => state, setState: value => { state = value; }, setInput: (id, value) => { document.getElementById(id).value = value; },
    setChecked: (id, value) => { document.getElementById(id).checked = value; }, getFlash: () => flashMessage,
    ageOnCompetitionDate, normalizedDateValue, nextStackerCode, nextTeamCode, divisionForStacker, findDivisionFor, divisionCountSummary, relayTeamSetupAvailable,
    sortStackers, sortStackerTable, validateDoubleEntry, removeConflictingDoubles, validateRelayEntry, removeConflictingRelays,
    relayIsComplete, relayCanCompete, relayTeamStatus, relayTimedDivision, relayHeadToHeadDivision, generatedRelayDivision, parseCompetitionTime, normalizePrelimEntryId, resolvePrelimParticipant, prelimResultInputValue,
    calculateBestResult, bestAttempt, official, finalTieBreakKey, compareFinalResults, finalPlacements, awardPlanRows,
    eventRows, allAroundRows, applyCompetitionFilters, limitGroupedRows, rankCompetitionRows, stateToXml, xmlToState, addStacker, deleteStacker,
    finalsEngine: FinalsReportEngine
  };`;
  vm.runInNewContext(source, context, { filename: "app.js" });
  return { hooks: context.__stackMeetHooks };
}

function freshState(hooks) {
  const state = hooks.getState();
  return structuredClone(state);
}

let passed = 0;
function same(actual, expected) { assert.deepStrictEqual(JSON.parse(JSON.stringify(actual)), expected); }
function scenario(id, given, when, then) {
  try { when(); passed += 1; console.log(`PASS ${id} — ${then}`); }
  catch (error) { error.message = `${id}: Given ${given}. ${error.message}`; throw error; }
}

const { hooks } = loadLegacyApp();

scenario("REG-001", "an empty competition", () => {
  const state = freshState(hooks); state.stackers = []; state.doubles = []; state.relays = []; hooks.setState(state);
  assert.strictEqual(hooks.nextStackerCode(), "1.1");
}, "the next individual ID is 1.1");

scenario("REG-002", "existing IDs across all groups", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.9" }]; state.doubles = [{ id: "2.4" }]; state.relays = [{ id: "3.7" }]; hooks.setState(state);
  assert.strictEqual(hooks.nextStackerCode(), "1.10"); assert.strictEqual(hooks.nextTeamCode("2"), "2.5"); assert.strictEqual(hooks.nextTeamCode("3"), "3.8");
}, "IDs advance independently by public prefix");

scenario("REG-003", "a valid date written in supported forms", () => {
  assert.strictEqual(hooks.normalizedDateValue("11/07/2026"), "2026-07-11");
  assert.strictEqual(hooks.normalizedDateValue("July 11, 2026"), "2026-07-11");
  assert.strictEqual(hooks.normalizedDateValue("2026-02-29"), "");
}, "DOB input is normalized and invalid calendar dates are rejected");

scenario("REG-004", "a birthday after the competition date", () => {
  assert.strictEqual(hooks.ageOnCompetitionDate("2014-07-12", "2026-07-11"), 11);
  assert.strictEqual(hooks.ageOnCompetitionDate("2014-07-11", "2026-07-11"), 12);
}, "age is calculated on the competition start date");

scenario("REG-005", "configured ordinary and Special cutoffs", () => {
  const state = freshState(hooks); state.divisionSettings = { combined: [10], male: [12], female: [12], special: [12], custom: [] }; hooks.setState(state);
  assert.strictEqual(hooks.findDivisionFor(10, "F", false, state.divisionSettings), "10 & Under Combined");
  assert.strictEqual(hooks.ageOnCompetitionDate("2014-07-12", "2026-07-11", "actual"), 11);
  assert.strictEqual(hooks.ageOnCompetitionDate("2014-07-12", "2026-07-11", "yearBorn"), 12);
  state.settings.separateSpecialDivisionsByGender = false; hooks.setState(state);
  assert.strictEqual(hooks.findDivisionFor(12, "M", true, state.divisionSettings, false), "SS 12 & Under L1");
  state.settings.separateSpecialDivisionsByGender = true; hooks.setState(state);
  assert.strictEqual(hooks.findDivisionFor(12, "M", true, state.divisionSettings, true), "SS 12 & Under L1 M");
  assert.strictEqual(hooks.findDivisionFor(12, "F", true, state.divisionSettings, true), "SS 12 & Under L1 F");
  state.events = { Individuals: [], Doubles: [], "Timed Relay": [], "Head To Head": ["Cycle"] }; hooks.setState(state);
  assert.strictEqual(hooks.relayTeamSetupAvailable(), true);
  state.events["Head To Head"] = []; hooks.setState(state);
  assert.strictEqual(hooks.relayTeamSetupAvailable(), false);
}, "division assignment follows age, gender and Special rules");

scenario("REG-006", "registered stackers", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.10", name: "Zed", age: 10 }, { id: "1.2", name: "Amy", age: 12 }]; hooks.setState(state);
  same(hooks.sortStackers(state.stackers).map(item => item.id), ["1.2", "1.10"]);
}, "numeric stacker sorting orders 1.2 before 1.10");

scenario("TEAM-001", "incomplete or invalid doubles entries", () => {
  assert.strictEqual(hooks.validateDoubleEntry({ type: "normal", status: "complete", one: "1.1", two: "", parentName: "" }), "Normal doubles needs two registered stackers.");
  assert.strictEqual(hooks.validateDoubleEntry({ type: "child_parent", status: "complete", one: "1.1", two: "", parentName: "Parent" }), "");
  assert.match(hooks.validateDoubleEntry({ type: "normal", status: "complete", one: "1.1", two: "1.1", parentName: "" }), /cannot partner/);
}, "normal, Child/Parent, and self-pairing rules are enforced");

scenario("TEAM-002", "a stacker reassigned to a new doubles team", () => {
  const state = freshState(hooks); state.doubles = [{ id: "2.1", one: "1.1", two: "1.2" }, { id: "2.2", one: "1.3", two: "1.4" }]; hooks.setState(state);
  same(hooks.removeConflictingDoubles(["1.1"]), ["2.1"]); same(hooks.getState().doubles.map(t => t.id), ["2.2"]);
}, "the conflicting former doubles team is removed");

scenario("TEAM-003", "relay entries", () => {
  const state = freshState(hooks); state.settings.start = "2099-01-01"; state.relays = [{ id: "3.1", name: "Fast Four", members: ["1.1", "1.2", "1.3", "1.4"] }]; hooks.setState(state);
  assert.match(hooks.validateRelayEntry([], ""), /name is required/);
  assert.strictEqual(hooks.validateRelayEntry([], "Draft Team"), "");
  assert.match(hooks.validateRelayEntry(["1.1"], "fast four"), /must be unique/);
  assert.match(hooks.validateRelayEntry(["1", "2", "3", "4", "5", "6", "7"], "Seven"), /up to 6/);
  assert.match(hooks.validateRelayEntry(["1.1", "1.1"], "New"), /only be selected once/);
  assert.strictEqual(hooks.relayIsComplete({ members: ["1", "2", "3", "4"] }), true);
  assert.strictEqual(hooks.relayIsComplete({ members: ["1", "2", "3"] }), false);
  assert.strictEqual(hooks.relayTeamStatus({ members: [] }, "2026-07-11"), "Draft");
  assert.strictEqual(hooks.relayTeamStatus({ members: ["1", "2", "3"] }, "2026-07-11"), "Incomplete");
  assert.strictEqual(hooks.relayTeamStatus({ members: ["1", "2", "3", "4"] }, "2026-07-11"), "Ready");
  assert.strictEqual(hooks.relayTeamStatus({ members: ["1", "2", "3", "4", "5"] }, "2026-07-11"), "Ready");
  assert.strictEqual(hooks.relayTeamStatus({ members: ["1", "2", "3", "4", "5", "6"] }, "2026-07-11"), "Ready");
  assert.strictEqual(hooks.relayTeamStatus({ members: ["1", "2", "3", "4"] }, "2099-01-01"), "Locked");
  state.settings.start = ""; state.divisionSettings.timedRelay = [10, 12, 14]; state.divisionSettings.headToHeadRelay = [11, 13]; state.stackers = [{ id: "1.1", age: 10 }, { id: "1.2", age: 10 }, { id: "1.3", age: 11 }, { id: "1.4", age: 12 }]; hooks.setState(state);
  assert.strictEqual(hooks.generatedRelayDivision(["1.1", "1.2", "1.3", "1.4"], "timedRelay"), "12U");
  assert.strictEqual(hooks.generatedRelayDivision(["1.1", "1.2", "1.3", "1.4"], "headToHeadRelay"), "13U");
  state.divisionSettings.timedRelay = [10, 11, 14]; hooks.setState(state);
  assert.strictEqual(hooks.relayTimedDivision({ members: ["1.1", "1.2", "1.3", "1.4"] }), "14U");
  assert.strictEqual(hooks.relayHeadToHeadDivision({ members: ["1.1", "1.2", "1.3", "1.4"] }), "13U");
}, "draft, incomplete, Ready and Locked states, capacity, independent relay divisions, and oldest-member recalculation are applied");

scenario("RES-001", "universal compact or dotted result IDs", () => {
  assert.strictEqual(hooks.normalizePrelimEntryId("12"), "1.2"); assert.strictEqual(hooks.normalizePrelimEntryId("115"), "1.15"); assert.strictEqual(hooks.normalizePrelimEntryId("1125"), "1.125");
  assert.strictEqual(hooks.normalizePrelimEntryId("21"), "2.1"); assert.strictEqual(hooks.normalizePrelimEntryId("218"), "2.18"); assert.strictEqual(hooks.normalizePrelimEntryId("37"), "3.7"); assert.strictEqual(hooks.normalizePrelimEntryId("3105"), "3.105");
  assert.strictEqual(hooks.normalizePrelimEntryId("3.04"), "3.4"); assert.strictEqual(hooks.normalizePrelimEntryId("44"), "");
}, "valid IDs resolve according to the universal entry convention");

scenario("RES-002", "time-entry values", () => {
  same(hooks.parseCompetitionTime(""), { kind: "blank", value: null });
  same(hooks.parseCompetitionTime("999"), { kind: "scratch", value: 999 });
  same(hooks.parseCompetitionTime("6523"), { kind: "time", value: 6.523 });
  same(hooks.parseCompetitionTime("1:02.5"), { kind: "time", value: 62.5 });
  assert.strictEqual(hooks.parseCompetitionTime("601000").kind, "invalid");
}, "blank/DNS, scratch, milliseconds and clock notation retain current meaning");

scenario("RES-003", "attempts and a scratch", () => {
  assert.strictEqual(hooks.bestAttempt({ attempts: [7.1, 6.9, 7.0], penalty: 0 }), 6.9);
  assert.strictEqual(hooks.official({ attempts: [6.9], penalty: 0 }), 6.9);
  assert.strictEqual(hooks.official({ attempts: [6.9], penalty: 2 }), 8.9);
  same(hooks.calculateBestResult({ attempts: [4.356, 999, 4.578], penalty: 0 }), { status: "valid", bestTime: 4.356, bestValidTime: 4.356, eligibleForRanking: true });
  same(hooks.calculateBestResult({ attempts: [999, 999, 4.578], penalty: 0 }), { status: "valid", bestTime: 4.578, bestValidTime: 4.578, eligibleForRanking: true });
  same(hooks.calculateBestResult({ attempts: [999, 5.123, 999], penalty: 0 }), { status: "valid", bestTime: 5.123, bestValidTime: 5.123, eligibleForRanking: true });
  same(hooks.calculateBestResult({ attempts: [999, 999, 999], penalty: 999 }), { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false });
  same(hooks.calculateBestResult({ attempts: [], penalty: 0 }), { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false });
}, "the shared helper ignores scratch when any valid attempt exists and blocks scratch-only or missing entries");

scenario("FIN-001", "equal best final attempts", () => {
  const a = { participant: "1.1", attempts: [6.1, 6.4, 6.6] }; const b = { participant: "1.2", attempts: [6.1, 6.3, 6.7] };
  same(hooks.finalTieBreakKey(a), [6.1, 6.4, 6.6]); assert.ok(hooks.compareFinalResults(a, b) > 0);
}, "second-best then third-best attempts break the tie");

scenario("FIN-002", "a final sheet with three results", () => {
  const sheet = { finalists: [{ participant: "1.1" }, { participant: "1.2" }, { participant: "1.3" }] };
  const placements = hooks.finalPlacements(sheet, [{ participant: "1.1", attempts: [7] }, { participant: "1.2", attempts: [6.5] }, { participant: "1.3", attempts: [] }]);
  assert.strictEqual(placements.get("1.2"), 1); assert.strictEqual(placements.get("1.1"), 2); assert.strictEqual(placements.has("1.3"), false);
}, "the fastest valid final result wins and no-attempt finalist is unplaced");

scenario("AWD-001", "a planned competition structure", () => {
  const state = freshState(hooks); state.events = { Individuals: ["3-3-3"], Doubles: ["Cycle"], "Timed Relay": ["3-6-3"] }; state.awards.individualPlaces = 2; state.awards.doublesPlaces = 1; state.awards.relayPlaces = 1; state.awards.relayUnits = 4; hooks.setState(state);
  assert.throws(() => hooks.awardPlanRows(), /generatedDivisions is not defined/);
}, "the current planner fails before calculating because generatedDivisions is absent (characterized defect)");

scenario("RPT-001", "preliminary results including a Special stacker", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "Normal", division: "12U", special: "No", gender: "M" }, { id: "1.2", name: "Special", division: "SS 12U", special: "Yes", gender: "F" }]; state.results = [{ type: "Individual", stage: "Prelims", participant: "1.1", event: "Cycle", attempts: [7], penalty: 0 }, { type: "Individual", stage: "Prelims", participant: "1.2", event: "Cycle", attempts: [6], penalty: 0 }]; hooks.setState(state);
  hooks.setInput("competitionSpecialMode", "special"); hooks.setInput("competitionDivision", "all"); hooks.setInput("competitionGender", ""); hooks.setInput("competitionCountry", ""); hooks.setInput("competitionRegion", ""); hooks.setInput("competitionOrg", "");
  same(hooks.applyCompetitionFilters(hooks.eventRows("i", "cycle")).map(row => row.participant), ["1.2"]);
}, "report filtering can isolate Special stacker rows");

scenario("FRP-001", "a scratch final result", () => {
  const result = hooks.finalsEngine.classifyResult({ attempts: [999], penalty: 0 });
  same(result, { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false });
  same(hooks.finalsEngine.classifyResult({ attempts: [4.356, 999, 4.578], penalty: 0 }), { status: "valid", bestTime: 4.356, bestValidTime: 4.356, eligibleForRanking: true });
}, "999 is scratch-only when no valid attempt exists and never outranks a valid performance");

scenario("FRP-002", "equal final performances", () => {
  const rows = hooks.finalsEngine.rankFinalRows([{ participant: "1.2", name: "Beta", result: { attempts: [6.1] } }, { participant: "1.1", name: "Alpha", result: { attempts: [6.1] } }]);
  same(rows.map(row => [row.participant, row.rank]), [["1.1", 1], ["1.2", 1]]);
}, "equal valid performances retain an equal rank while names only stabilize display");

scenario("FRP-003", "Normal, Special, and Mixed category filters", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "Normal", special: "No", gender: "M" }, { id: "1.2", name: "Special", special: "Yes", gender: "F" }]; state.results = [{ stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [7] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [6] }]; hooks.setState(state);
  const engine = hooks.finalsEngine;
  assert.strictEqual(engine.finalResultRows(state, { category: "normal" }).length, 1); assert.strictEqual(engine.finalResultRows(state, { category: "special" }).length, 1); assert.strictEqual(engine.finalResultRows(state, { category: "mixed" }).length, 2);
}, "the category dimension isolates Normal and Special and combines both for Mixed");

scenario("FRP-004", "Male, Female, and Combined filters", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "Male", special: "No", gender: "M" }, { id: "1.2", name: "Female", special: "No", gender: "F" }]; state.results = [{ stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [7] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [6] }]; hooks.setState(state);
  const engine = hooks.finalsEngine;
  assert.strictEqual(engine.finalResultRows(state, { gender: "M" }).length, 1); assert.strictEqual(engine.finalResultRows(state, { gender: "F" }).length, 1); assert.strictEqual(engine.finalResultRows(state, {}).length, 2);
}, "the gender dimension supports Male, Female, and Combined");

scenario("FRP-004A", "all nine Top Performance category and gender combinations", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "Normal Male", special: "No", gender: "M" }, { id: "1.2", name: "Normal Female", special: "No", gender: "F" }, { id: "1.3", name: "Special Male", special: "Yes", gender: "M" }, { id: "1.4", name: "Special Female", special: "Yes", gender: "F" }]; state.results = state.stackers.map((stacker, index) => ({ stage: "Finals", type: "Individual", participant: stacker.id, event: "Cycle", attempts: [6 + index] })); hooks.setState(state);
  const expected = { normal: { M: 1, F: 1, "": 2 }, special: { M: 1, F: 1, "": 2 }, mixed: { M: 2, F: 2, "": 4 } };
  ["normal", "special", "mixed"].forEach(category => ["M", "F", ""].forEach(gender => assert.strictEqual(hooks.finalsEngine.finalResultRows(state, { category, gender }).length, expected[category][gender])));
}, "all Normal/Special/Mixed × Male/Female/Combined combinations are repeatable");

scenario("FRP-005", "a finals-only All-Around", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "Eligible", special: "No", gender: "M" }, { id: "1.2", name: "Scratch", special: "No", gender: "M" }]; state.results = [{ stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [3] }, { stage: "Finals", type: "Individual", participant: "1.1", event: "3-6-3", attempts: [4] }, { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [5] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "3-3-3", attempts: [3] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "3-6-3", attempts: [4] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [999] }]; hooks.setState(state);
  const rows = hooks.finalsEngine.allAroundRows(state, {}); assert.strictEqual(rows.find(row => row.participant === "1.1").bestValidTime, 12); assert.strictEqual(rows.find(row => row.participant === "1.2").resultStatus, "ineligible");
}, "only three valid final events make an Individual eligible");

scenario("FRP-006", "organization placement credits for teams", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "A", org: "Org A" }, { id: "1.2", name: "B", org: "Org A" }, { id: "1.3", name: "C", org: "Org B" }]; state.doubles = [{ id: "2.1", one: "1.1", two: "1.2" }, { id: "2.2", one: "1.1", two: "1.3" }]; state.results = [{ stage: "Finals", type: "Doubles", participant: "2.1", event: "Cycle", attempts: [6] }, { stage: "Finals", type: "Doubles", participant: "2.2", event: "Cycle", attempts: [7] }]; hooks.setState(state);
  const credits = hooks.finalsEngine.organizationCredits(state, {}); assert.strictEqual(credits.find(row => row.organization === "Org A").counts[1], 1); assert.strictEqual(credits.find(row => row.organization === "Org A").counts[2], 1); assert.strictEqual(credits.find(row => row.organization === "Org B").counts[2], 1);
}, "one team placement credits a represented organization once, including mixed-organization teams");

scenario("FRP-007", "organization lexicographic placement ranking", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "A1", org: "Org A" }, { id: "1.2", name: "A2", org: "Org A" }, { id: "1.3", name: "B1", org: "Org B" }, { id: "1.4", name: "B2", org: "Org B" }, { id: "1.5", name: "C1", org: "Org C" }]; state.results = [{ stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [5] }, { stage: "Finals", type: "Individual", participant: "1.3", event: "3-3-3", attempts: [6] }, { stage: "Finals", type: "Individual", participant: "1.2", event: "3-6-3", attempts: [6] }, { stage: "Finals", type: "Individual", participant: "1.4", event: "3-6-3", attempts: [5] }, { stage: "Finals", type: "Individual", participant: "1.5", event: "Cycle", attempts: [4] }, { stage: "Finals", type: "Individual", participant: "1.3", event: "Cycle", attempts: [5] }]; hooks.setState(state);
  const rows = hooks.finalsEngine.organizationCredits(state, {}); assert.strictEqual(rows.find(row => row.organization === "Org B").rank, 1); assert.strictEqual(rows.find(row => row.organization === "Org A").rank, 2);
}, "equal champions are resolved by second places before any later placement");

scenario("FRP-008", "qualification snapshot XML export", () => {
  const state = freshState(hooks); state.finalQualificationSnapshots = [{ id: "q1", status: "Approved", participantType: "Individual", division: "12U", event: "Cycle", selectedQualifiers: [] }]; hooks.setState(state);
  assert.match(hooks.stateToXml(state), /<finalQualificationSnapshots>/); assert.match(hooks.stateToXml(state), /q1/);
}, "snapshot records round-trip through the existing XML payload format");

scenario("STO-001", "the current state", () => {
  const state = freshState(hooks); state.stackers = [{ id: "1.1", name: "A & B", attempts: [] }]; hooks.setState(state);
  const xml = hooks.stateToXml(state); assert.match(xml, /<stackmeet version="1">/); assert.match(xml, /A &amp; B/); assert.match(xml, /<stackers>/);
}, "XML export uses the current StackMeet root and the current JSON state remains serializable");

console.log(`Characterization suite passed (${passed} scenarios).`);
