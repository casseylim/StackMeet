"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const ResultApi = require(path.join(root, "js/storage/ResultApi.js"));

function element() {
  return {
    value: "", hidden: false, textContent: "", innerHTML: "", dataset: {}, style: {}, children: [],
    classList: { toggle() {}, add() {}, remove() {} }, focus() {}, select() {}, remove() {}, setAttribute() {}, closest() { return null; },
    addEventListener(event, handler) { this[event] = handler; },
    append(...children) { this.children.push(...children); }, appendChild(child) { this.children.push(child); },
    replaceChildren(...children) { this.children = children; },
    querySelector() { return null; }, querySelectorAll() { return []; }
  };
}

function fixture() {
  const elements = new Map();
  const get = id => {
    if (!elements.has(id)) elements.set(id, element());
    return elements.get(id);
  };
  const final = [element(), element(), element()];
  const labels = ["prelim333", "prelim363", "prelimCycle"].map((id, index) => ({ hidden: false, dataset: { prelimEvent: ["3-3-3", "3-6-3", "Cycle"][index] }, querySelector: () => get(id) }));
  const document = {
    getElementById: get, createElement: element, createTextNode: text => text,
    querySelector: selector => selector.includes("data-final-attempt") ? final[Number(selector.match(/data-final-attempt="(\d)"/)[1]) - 1] : null,
    querySelectorAll: selector => selector === ".final-time-input" ? final : selector === "[data-prelim-event]" ? labels : [],
    addEventListener() {}, body: element()
  };
  const context = vm.createContext({
    console, document, location: { hash: "", search: "", href: "http://fixture.invalid/" }, history: { replaceState() {} },
    sessionStorage: { setItem() {} }, localStorage: { getItem() { return null; }, setItem() {} },
    confirm: () => context.confirmAnswer, confirmAnswer: true, alert() {},
    URL, URLSearchParams, structuredClone, Blob, crypto: { randomUUID: () => "synthetic-id" }, window: { addEventListener() {} }
  });
  for (const name of ["js/i18n/I18n.js", "js/storage/ApiProvider.js", "js/storage/StackerApi.js", "js/storage/Repository.js", "js/results/BestResultEngine.js", "js/reports/FinalsReportEngine.js", "js/ParticipantTeams.js", "js/results/ScoringEntry.js"]) {
    vm.runInContext(read(name), context);
  }
  const source = read("app.js").replace(/\n(?:void\s+)?initializeApplication\(\)(?:\.catch\(showBootError\))?;\s*$/, "\n");
  vm.runInContext(source + '\n' + [
    'const fixtureSheet = { id: "1.9", type: "Individual", entryType: "Individual", division: "10U", event: "Cycle", finalists: [{participant:"1.1", name:"Synthetic", qualifierRank:1, prelimTime:3}], prelimRows: [] };',
    'finalSheets = () => [fixtureSheet]; drawResultRows = () => {}; drawMissingTimes = () => {}; populateFinalSheetSelect = () => {};',
    'globalThis.hooks = { loadPrelimParticipant, savePrelimResults, loadFinalSheet, saveFinalResults, refreshSqlResults,',
    'switchTo: id => setSelectedSqlCompetition({id}), setApi: api => { resultApi.list = api.list; resultApi.saveBatch = api.saveBatch; resultApi.review = api.review; resultApi.acceptReview = api.acceptReview; },',
    'init: () => { state.stackers = [{id:"1.1",name:"Synthetic",gender:"M",division:"10U"}]; }, current: () => state.results,',
    'baseline: kind => (kind === "prelim" ? prelimResultEditor : finalResultEditor).baseline };'
  ].join('\n'), context);
  context.hooks.init();
  context.hooks.switchTo(7);
  return { hooks: context.hooks, get, final, context };
}

const row = (stage, revision, attempts = [5]) => ({
  id: stage + "-id", stage, type: "Individual", participant: "1.1", event: "Cycle", attempts, penalty: 0, revision
});
const deferred = () => Promise.withResolvers();
const snapshot = (revision, results) => ({ revision, results });
const conflict = Object.assign(new Error("stale"), { status: 409, outcome: "rejected" });

async function editorScenarios() {
  for (const stage of ["Prelims", "Finals"]) {
    const { hooks, get, final, context } = fixture();
    let latest = snapshot(10, [row(stage, 8)]);
    const requests = [];
    let gate = deferred();
    hooks.setApi({
      list: async () => latest, review: async () => latest,
      acceptReview: (_, revision) => revision === latest.revision,
      saveBatch: async (id, body) => { requests.push({ id, body }); return gate.promise; }
    });
    await hooks.refreshSqlResults({ rerender: false });
    const prelim = stage === "Prelims";
    const save = prelim ? hooks.savePrelimResults : hooks.saveFinalResults;
    const input = prelim ? get("prelimCycle") : final[0];
    const message = get(prelim ? "prelimEntryMessage" : "finalEntryMessage");
    if (prelim) { get("timeSheetId").value = "1.1"; hooks.loadPrelimParticipant(); }
    else hooks.loadFinalSheet("1.9");
    const baseline = hooks.baseline(prelim ? "prelim" : "final");
    assert.equal(baseline.revision, 10);
    assert.equal(Object.values(baseline.rows).find(entry => entry.exists).id, stage + "-id");
    input.value = "6.123";
    latest = snapshot(11, [
      row(stage, 11, [7]),
      ...(prelim ? [{ ...row(stage, 11, [8.765]), id: "other-event", event: "3-3-3" }] : [])
    ]);
    await hooks.refreshSqlResults({ rerender: false });
    const first = save();
    assert.equal(first, save(), "repeated submission joins the pending write");
    assert.equal(requests.length, 1);
    assert.equal(requests[0].body.upserts[0].expectedRevision, 8);
    gate.reject(conflict);
    assert.equal(await first, false);
    assert.equal(input.value, "6.123");
    assert.equal(input.readOnly, false);
    await message.children[2].click();
    const comparison = message.children[2].children[0].textContent;
    assert.match(comparison, /Saved: 7.*Draft: 6\.123/);
    if (prelim) {
      const comparisons = message.children[2].children.map(item => item.textContent);
      assert.equal(comparisons.length, Object.keys(baseline.rows).length, "review must show every rebased row");
      assert.ok(comparisons.some(value => /3-3-3.*Saved: 8\.765.*Draft: No draft result/.test(value)), "a blank draft row with a remote result must be shown before accepting the new baseline");
    }
    context.confirmAnswer = false;
    message.children[3].click();
    assert.equal(hooks.baseline(prelim ? "prelim" : "final"), baseline);
    context.confirmAnswer = true;
    message.children[3].click();
    assert.equal(input.value, "6.123");
    assert.equal(hooks.baseline(prelim ? "prelim" : "final").revision, 11);
    gate = deferred();
    const afterReview = save();
    assert.equal(requests[1].body.upserts[0].expectedRevision, 11);
    const delayedGet = deferred();
    hooks.setApi({ list: () => delayedGet.promise, saveBatch: async () => { throw new Error("Unexpected write"); } });
    const oldRead = hooks.refreshSqlResults({ rerender: false });
    hooks.switchTo(8);
    delayedGet.resolve(snapshot(99, [row(stage, 99)]));
    gate.resolve(snapshot(12, [row(stage, 12)]));
    assert.equal(await oldRead, false);
    assert.equal(await afterReview, false);
    assert.equal(hooks.current().length, 0, "pending result responses cannot touch the new competition");
  }

  const { hooks, get } = fixture();
  let latest = snapshot(20, []);
  let write;
  hooks.setApi({
    list: async () => latest,
    saveBatch: async (_, body) => { write = body; throw conflict; }
  });
  await hooks.refreshSqlResults({ rerender: false });
  get("timeSheetId").value = "1.1";
  hooks.loadPrelimParticipant();
  get("prelimCycle").value = "6.123";
  latest = snapshot(21, []);
  await hooks.refreshSqlResults({ rerender: false });
  await hooks.savePrelimResults();
  assert.equal(write.upserts[0].expectedRevision, 0);
  assert.equal(write.expectedResultsRevision, 20, "a create keeps the revision observed when the editor opened");
}

async function transportScenarios() {
  global.window = { StackMeetAuth: { authHeaders: () => ({}) } };
  const pending = [];
  global.fetch = (url, options) => {
    const call = deferred();
    pending.push({ ...call, url, method: options.method });
    return call.promise;
  };
  const reply = (call, data, status = 200) => call.resolve({ ok: status < 400, status, json: async () => data, text: async () => JSON.stringify(data) });
  const api = new ResultApi();
  const write = { upserts: [{ ...row("Finals", 1), expectedRevision: 1 }], deletes: [] };
  const read1 = api.list(7);
  const read2 = api.list(7);
  reply(pending[1], snapshot(3, [row("Finals", 3)]));
  await read2;
  reply(pending[0], snapshot(2, [row("Finals", 2)]));
  assert.equal((await read1).revision, 3);

  const delayedWrite = api.saveBatch(7, write);
  const newRead = api.list(7);
  reply(pending[3], snapshot(5, [row("Finals", 5)]));
  await newRead;
  reply(pending[2], snapshot(4, [row("Finals", 4)]));
  assert.equal((await delayedWrite).revision, 5, "delayed POST cannot reapply an obsolete delta");

  const lateRead = api.list(7);
  const successful = api.saveBatch(7, write);
  reply(pending[5], snapshot(8, [row("Finals", 8)]));
  await new Promise(setImmediate);
  pending[6].reject(new Error("refresh unavailable"));
  await assert.rejects(successful, { outcome: "committed-refresh-failed" });
  reply(pending[4], snapshot(7, [row("Finals", 7)]));
  await assert.rejects(lateRead, /superseded/);
  await assert.rejects(api.saveBatch(7, write), { outcome: "review-required" });
  assert.equal(pending.length, 7);
  const review = api.review(7);
  reply(pending[7], snapshot(8, [row("Finals", 8)]));
  assert.equal(api.acceptReview(7, (await review).revision), true);

  const uncertain = api.saveBatch(7, write);
  pending[8].reject(new Error("disconnected after send"));
  await assert.rejects(uncertain, { outcome: "unknown" });
  await assert.rejects(api.saveBatch(7, write), { outcome: "review-required" });
  const elsewhere = api.saveBatch(8, write);
  reply(pending[9], { code: "result_precondition_required" }, 428);
  await assert.rejects(elsewhere, { outcome: "rejected", status: 428, code: "result_precondition_required" });
}

(async () => {
  await editorScenarios();
  await transportScenarios();
  const localeContext = vm.createContext({});
  const keys = ["Review latest saved results", "The save outcome is unknown. Review saved results before trying again.",
    "Results were saved. Review saved results before continuing.", "Use reviewed results as the new baseline", "No draft result"];
  for (const locale of ["en", "ms", "zh-Hans"]) {
    vm.runInContext(read("js/i18n/locales/" + locale + ".js"), localeContext);
    for (const key of keys) {
      const value = localeContext.StackMeetI18nLocales[locale][key];
      assert.ok(value);
      if (locale !== "en") assert.notEqual(value, key);
    }
  }
  console.log("ENG-01R1 editor, result transport and locale scenarios passed.");
})().catch(error => { console.error(error); process.exitCode = 1; });
