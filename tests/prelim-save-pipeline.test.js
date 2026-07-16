"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

function element() {
  return { value: "", checked: false, hidden: false, innerHTML: "", textContent: "", dataset: {}, style: {}, classList: { toggle() {}, add() {}, remove() {} }, addEventListener() {}, focus() {}, select() {}, remove() {}, append() {}, appendChild() {}, querySelector() { return null; }, querySelectorAll() { return []; }, setAttribute() {}, click() {} };
}

function loadApp() {
  const elements = new Map();
  const document = { getElementById(id) { if (!elements.has(id)) elements.set(id, element()); return elements.get(id); }, querySelector() { return null; }, querySelectorAll() { return []; }, addEventListener() {}, createElement() { return element(); }, body: { appendChild() {} } };
  const context = { console, document, location: { hash: "", search: "" }, confirm: () => true, alert() {}, crypto: { randomUUID: () => `test-${Math.random()}` }, structuredClone, URL, URLSearchParams, Blob, localStorage: { getItem() { return null; }, setItem() {} }, window: { addEventListener() {}, print() {}, open() { return { document: { write() {}, close() {} }, focus() {}, print() {}, close() {} }; } } };
  context.globalThis = context;
  ["ApiProvider.js", "StackerApi.js", "Repository.js"].forEach(file => vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "storage", file), "utf8"), context, { filename: file }));
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "results", "BestResultEngine.js"), "utf8"), context, { filename: "BestResultEngine.js" });
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "js", "reports", "FinalsReportEngine.js"), "utf8"), context, { filename: "FinalsReportEngine.js" });
  let source = fs.readFileSync(path.join(__dirname, "..", "app.js"), "utf8").replace(/\nvoid initializeApplication\(\);\s*$/, "\n");
  source += `\nglobalThis.__hooks = { getState: () => state, setState: value => { state = value; }, setInput: (id, value) => { document.getElementById(id).value = value; }, getInput: id => document.getElementById(id).value, getHtml: id => document.getElementById(id).innerHTML, getMessage: () => document.getElementById("prelimEntryMessage").textContent, normalizePrelimEntryId, resolvePrelimParticipant, prelimParticipantIdentity, loadPrelimParticipant, savePrelimResults, setProvider: provider => { repository.provider = provider; pendingSave = Promise.resolve(); } };`;
  vm.runInNewContext(source, context, { filename: "app.js" });
  return context.__hooks;
}

function baseState(hooks, type) {
  const state = structuredClone(hooks.getState());
  state.results = [];
  state.stackers = [{ id: "1.1", name: "One", gender: "M", division: "10U" }, { id: "1.2", name: "Two", gender: "F", division: "10U" }, { id: "1.3", name: "Three", gender: "M", division: "10U" }, { id: "1.4", name: "Four", gender: "F", division: "10U" }];
  state.doubles = [{ id: "2.1", one: "1.1", two: "1.2", division: "10U" }];
  state.relays = [{ id: "3.1", name: "Relay", members: ["1.1", "1.2", "1.3", "1.4"], division: "10U" }];
  hooks.setState(state);
  hooks.setInput("timeSheetId", type === "Individual" ? "1.1" : type === "Doubles" ? "2.1" : "3.1");
  return state;
}

function providerFor(state, options = {}) {
  let saved = structuredClone(state), saves = 0;
  return { async save(next) { saves += 1; if (options.fail) throw new Error("offline"); if (options.delay) await new Promise(resolve => setTimeout(resolve, options.delay)); saved = structuredClone(next); }, async load() { return structuredClone(saved); }, get saves() { return saves; } };
}

async function run() {
  { const hooks = loadApp(); assert.strictEqual(hooks.normalizePrelimEntryId("31"), "3.1"); assert.strictEqual(hooks.normalizePrelimEntryId("37"), "3.7"); assert.strictEqual(hooks.normalizePrelimEntryId("3105"), "3.105"); }

  {
    const hooks = loadApp(), state = baseState(hooks, "Timed Relay");
    state.events = { Individuals: ["3-3-3", "3-6-3", "Cycle"], Doubles: ["Cycle"], "Timed Relay": ["3-6-3"] };
    hooks.setState(state); hooks.setInput("timeSheetId", "31");
    const participant = hooks.loadPrelimParticipant();
    assert.strictEqual(participant.id, "3.1"); assert.strictEqual(participant.type, "Timed Relay"); assert.strictEqual(participant.entryType, "Relay");
    assert.match(hooks.getHtml("prelimStackerSummary"), /Relay Team \/ Members/); assert.match(hooks.getHtml("prelimStackerSummary"), /Relay/); assert.match(hooks.getHtml("prelimStackerSummary"), /One/);
  }

  {
    const hooks = loadApp(), state = baseState(hooks, "Timed Relay");
    state.events = { Individuals: ["3-3-3", "3-6-3", "Cycle"], Doubles: ["Cycle"], "Timed Relay": ["Cycle"] };
    hooks.setState(state);
    assert.deepStrictEqual(hooks.resolvePrelimParticipant("31").events, ["Cycle"]);
  }

  for (const [type, fields] of [["Individual", ["prelim333", "prelim363", "prelimCycle"]], ["Doubles", ["prelimCycle"]], ["Timed Relay", ["prelim363"]]]) {
    const hooks = loadApp(), state = baseState(hooks, type), provider = providerFor(state); hooks.setProvider(provider);
    fields.forEach((field, index) => hooks.setInput(field, String(3 + index)));
    assert.strictEqual(await hooks.savePrelimResults(), true); assert.strictEqual(provider.saves, 1); assert.strictEqual(hooks.getState().results.length, fields.length);
  }

  { const hooks = loadApp(), state = baseState(hooks, "Individual"), provider = providerFor(state); hooks.setProvider(provider); hooks.setInput("prelim333", "3"); assert.strictEqual(await hooks.savePrelimResults({ blankAsScratch: true }), true); assert.strictEqual(hooks.getState().results.filter(result => result.penalty === 999).length, 2); }
  { const hooks = loadApp(), state = baseState(hooks, "Doubles"); state.results = [{ id: "old", stage: "Prelims", type: "Doubles", participant: "2.1", event: "Cycle", attempts: [9], penalty: 0 }]; hooks.setState(state); const provider = providerFor(state); hooks.setProvider(provider); hooks.setInput("prelimCycle", "7"); await hooks.savePrelimResults(); assert.strictEqual(hooks.getState().results[0].attempts[0], 0.007); }
  { const hooks = loadApp(), state = baseState(hooks, "Timed Relay"); state.results = [{ id: "old", stage: "Prelims", type: "Timed Relay", participant: "3.1", event: "3-6-3", attempts: [9], penalty: 0 }]; hooks.setState(state); const provider = providerFor(state); hooks.setProvider(provider); hooks.setInput("prelim363", "8"); await hooks.savePrelimResults(); assert.strictEqual(hooks.getState().results.length, 1); assert.strictEqual(hooks.getState().results[0].attempts[0], 0.008); }
  { const hooks = loadApp(), state = baseState(hooks, "Timed Relay"), provider = providerFor(state); hooks.setProvider(provider); assert.strictEqual(await hooks.savePrelimResults({ blankAsScratch: true }), true); const result = hooks.getState().results.find(item => item.type === "Timed Relay" && item.participant === "3.1"); assert.strictEqual(result.penalty, 999); }
  { const hooks = loadApp(), state = baseState(hooks, "Individual"), provider = providerFor(state, { fail: true }); hooks.setProvider(provider); hooks.setInput("prelim333", "3"); await hooks.savePrelimResults(); assert.strictEqual(hooks.getState().results.length, 0); assert.notStrictEqual(hooks.getInput("prelim333"), ""); assert.match(hooks.getMessage(), /Save failed/); }
  { const hooks = loadApp(), state = baseState(hooks, "Individual"), provider = providerFor(state, { delay: 10 }); hooks.setProvider(provider); hooks.setInput("prelim333", "3"); const first = hooks.savePrelimResults(), second = hooks.savePrelimResults(); assert.strictEqual(first, second); await Promise.all([first, second]); assert.strictEqual(provider.saves, 1); }
  { const hooks = loadApp(), state = baseState(hooks, "Timed Relay"), provider = providerFor(state, { delay: 10 }); hooks.setProvider(provider); hooks.setInput("prelim363", "3"); const first = hooks.savePrelimResults(), second = hooks.savePrelimResults(); assert.strictEqual(first, second); await Promise.all([first, second]); assert.strictEqual(provider.saves, 1); }
  console.log("Prelim save pipeline tests passed.");
}

run().catch(error => { console.error(error); process.exitCode = 1; });
