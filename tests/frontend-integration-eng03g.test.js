"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const read = file => fs.readFileSync(path.join(root, file), "utf8").replace(/\r\n/g, "\n");
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, "frontend-architecture-matrix.json"), "utf8"));
const actualScripts = [...read("index.html").matchAll(/<script\b[^>]*\bsrc="([^"]+)"/g)].map(match => match[1].split("?")[0]);
assert.deepEqual(actualScripts, manifest.shells["index.html"].map(item => item.src));

function element() {
  return { value: "", checked: false, hidden: false, innerHTML: "", textContent: "", dataset: {}, style: {},
    classList: { toggle() {}, add() {}, remove() {} },
    addEventListener() {}, focus() {}, select() {}, remove() {}, append() {}, appendChild() {},
    querySelector() { return null; }, querySelectorAll() { return []; }, setAttribute() {}, click() {} };
}
const elements = new Map();
const document = {
  getElementById(id) { if (!elements.has(id)) elements.set(id, element()); return elements.get(id); },
  querySelector() { return null; }, querySelectorAll() { return []; }, addEventListener() {},
  createElement: element, body: element(), documentElement: {}
};
function storage() {
  const entries = new Map();
  return { getItem: key => entries.get(key) ?? null, setItem: (key, value) => entries.set(key, String(value)), removeItem: key => entries.delete(key) };
}
const context = {
  console, document, location: { hash: "", pathname: "/", search: "", href: "http://fixture.invalid/" },
  localStorage: storage(), sessionStorage: storage(), structuredClone, URL, URLSearchParams, Blob,
  crypto: { randomUUID: () => "synthetic-id" }, addEventListener() {},
  fetch() { throw new Error("Integration fixture must not perform network I/O."); },
  confirm: () => true, alert() {}
};
context.window = context;
vm.createContext(context);
// All first-party scripts in actual shell order. SignalR transport and authenticated boot are
// explicitly excluded: this is a synthetic composition fixture, not browser/runtime proof.
for (const file of actualScripts) {
  if (file === "results/vendor/signalr.min.js") continue;
  let source = read(file);
  if (file === "app.js") {
    const boot = /\n(?:void\s+)?initializeApplication\(\)(?:\.catch\(showBootError\))?;\s*$/;
    assert.match(source, boot, "Review startup suppression if bootstrap changes.");
    source = source.replace(boot, "\n");
    source += [
      "globalThis.hooks = { getState: () => state, setInput: setValue, nextStackerCode,",
      "validateDoubleEntry, relayCanCompete, parseCompetitionTime, prelimResultInputValue,",
      "finalResultInputValues, allAroundRows, finalsEngine: FinalsReportEngine,",
      "captureResultBaseline, editorUpserts, legacyStateForSave,",
      "prelimTimeSheetHtml, finalTimeSheetHtml, csvLine, t, tf, esc,",
      "realProviders: () => [repository instanceof window.StackMeetStorage.Repository,",
      "stackerApi instanceof window.StackMeetStorage.StackerApi,",
      "resultApi instanceof window.StackMeetStorage.ResultApi] };"
    ].join("\n");
  }
  vm.runInContext(source, context, { filename: file });
}
const h = context.hooks;
assert.ok(h.realProviders().every(Boolean), "No fallback persistence provider permitted.");
const state = h.getState();
state.translations = {};
state.stackers = [{ id: "1.1", name: "Alpha & Beta", gender: "F", division: "U12", special: "No" }, { id: "1.2", name: "Incomplete", gender: "F", division: "U12", special: "No" }];
state.doubles = [];
state.relays = [];
const events = ["3-3-3", "3-6-3", "Cycle"];
const row = (participant, event, stage, value, revision) => ({ id: [participant, event, stage].join("-"), type: "Individual", participant, event, stage, attempts: [value], penalty: 0, revision });
state.results = [
  ...events.map((event, i) => row("1.1", event, "Prelims", 1 + i, 1)),
  ...events.map((event, i) => row("1.1", event, "Finals", 5 + i, 2)),
  ...events.map((event, i) => row("1.2", event, "Prelims", 1 + i, 1)),
  ...events.slice(0, 2).map((event, i) => row("1.2", event, "Finals", 4 + i, 2))
];
const before = JSON.stringify(state);
assert.equal(h.nextStackerCode(), "1.3");
assert.equal(h.validateDoubleEntry({ type: "normal", status: "complete", one: "1.1", two: "1.1" }), "A stacker cannot partner with themselves.");
assert.equal(h.relayCanCompete({ members: ["1", "2", "3", "4"] }), true);
assert.equal(h.parseCompetitionTime("6523").value, 6.523);
assert.equal(h.prelimResultInputValue(state.results[0]), "1.000");
assert.equal(h.finalResultInputValues({ attempts: [5, 999] }).join("|"), "5.000|999|");
h.setInput("competitionStage", "all");
const overall = h.allAroundRows("i");
assert.equal(overall.length, 1);
assert.equal(overall[0].participant, "1.1");
assert.equal(overall[0].time, 18, "Prelims cannot replace any Finals time.");
const engineRows = h.finalsEngine.allAroundRows(state);
assert.equal(engineRows.find(item => item.participant === "1.1").bestValidTime, 18);
assert.equal(engineRows.find(item => item.participant === "1.2").resultStatus, "ineligible");
h.setInput("competitionStage", "prelims");
assert.equal(h.allAroundRows("i").length, 2, "Explicit preliminary report stays available.");

const key = { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle" };
const baseline = h.captureResultBaseline([key], () => state.results[5], 10);
assert.ok(Object.isFrozen(baseline) && Object.isFrozen(baseline.rows));
const draft = { ...key, attempts: [h.parseCompetitionTime("8123").value], penalty: 0 };
assert.equal(h.editorUpserts({ baseline }, [draft])[0].expectedRevision, 2);
assert.throws(() => h.editorUpserts({ baseline: { rows: {} } }, [draft]), error => error.outcome === "review-required");
const legacy = h.legacyStateForSave(state);
assert.equal(legacy.stackers.length, 0);
assert.equal(legacy.results.length, 0);

for (const locale of ["en", "ms", "zh-Hans"]) {
  context.StackMeetLanguagePreference.setPreferredLanguage(locale);
  context.StackMeetI18n.setDocumentLanguage(locale, document);
  assert.equal(document.documentElement.lang, locale);
  const prelim = h.prelimTimeSheetHtml({ id: "1.1", type: "Individual", name: "Alpha & Beta", detail: "U12", location: "MY", events });
  assert.ok(prelim.includes("Alpha &amp; Beta"));
  assert.ok(prelim.includes(h.esc(h.tf("Attempt {attempt}", { attempt: 3 }))));
  assert.equal((prelim.match(/class="time-write-line"/g) || []).length, 9);
  const finals = h.finalTimeSheetHtml({ typeKey: "1", type: "Individual", entryType: "Individual", division: "U12", event: "Cycle", id: "1.1", finalists: [{ participant: "1.1", name: "Alpha & Beta", qualifierRank: 1, prelimTime: 3 }] });
  assert.ok(finals.includes(h.esc(h.t("Attempt 3"))));
  assert.ok(finals.includes("3.000")); // seeding reference only
  assert.equal(h.csvLine(["Alpha & Beta", overall[0].time]), '"Alpha & Beta","18"');
}
const trace = [];
const navigation = context.StackMeetNavigationActions;
assert.equal(navigation.navigate({ route: "reports" }, { leaveResultEditors: () => false, closeNavigationDrawer: () => trace.push("close"), setRoute: () => trace.push("route"), render: () => trace.push("render") }), false);
assert.equal(trace.length, 0, "Cancelled navigation must preserve edited state.");
assert.equal(navigation.dispatchPrint("print-current-view", {}, { print: () => trace.push("print") }), true);
assert.equal(trace.join("|"), "print");
assert.equal(JSON.stringify(state), before, "Reports, print templates and rejected navigation cannot mutate the fixture state.");

const publicHtml = read("results/index.html"), publicCss = read("results/results.css"), publicClient = read("results/results.js");
assert.match(publicCss, /\.brand-row\s*\{[^}]*flex-wrap:\s*wrap/);
assert.match(publicCss, /\.public-language-label select\s*\{[^}]*max-width:\s*100%/);
assert.match(publicCss, /\.section-status-card:focus-visible\s*\{\s*outline:\s*2px solid/);
assert.match(publicCss, /\.section-nav a\s*\{[^}]*color:\s*#46546a/);
assert.match(publicHtml, /<a class="brand" href="\/">\s*<span class="brand-mark" aria-hidden="true">S<\/span>\s*<span>NADITrack<\/span>/);
assert.doesNotMatch(publicClient, /link\.setAttribute\("aria-label", .*\bt\(item\.label\)/);
assert.match(publicClient, /arrow\.setAttribute\("aria-hidden", "true"\)/);
console.log("ENG-03G synthetic shell composition, three locales, result/print integration and static UI guards passed.");
