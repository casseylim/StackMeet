"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const CompetitionRepository = require("../backend/StackMeet.Api/wwwroot/js/storage/Repository.js");

const source = fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "app.js"), "utf8");

function extractFunction(name) {
  const functionStart = source.indexOf(`function ${name}(`);
  assert.notStrictEqual(functionStart, -1, `Unable to find ${name}`);
  const start = source.slice(functionStart - 6, functionStart) === "async " ? functionStart - 6 : functionStart;
  const braceStart = source.indexOf("{", start);
  let depth = 0;
  let quote = null;
  let escaped = false;
  for (let index = braceStart; index < source.length; index += 1) {
    const char = source[index];
    if (quote) {
      if (escaped) escaped = false;
      else if (char === "\\") escaped = true;
      else if (char === quote) quote = null;
      continue;
    }
    if (["\"", "'", "`"].includes(char)) { quote = char; continue; }
    if (char === "{") depth += 1;
    if (char === "}" && --depth === 0) return source.slice(start, index + 1);
  }
  throw new Error(`Unable to extract ${name}`);
}

function createHarness(repository, initialState) {
  const context = {
    structuredClone, Map, Set, String, Array, CompetitionRepository,
    repository, state: initialState, pendingSave: Promise.resolve(), queuedSaveCount: 0, stateSaveGeneration: 0, stateSaveFailure: null,
    deletionGeneration: 0, pendingDeletedDoubles: new Map(), pendingDeletedRelays: new Map(),
    setSaveStatus() {}, applyPendingCompetitionUpdate() {}
  };
  context.globalThis = context;
  const functions = ["mergeConcurrentState", "legacyStateForSave", "recordDeletedStateRecord", "snapshotDeletedStateRecords", "acknowledgeDeletedStateRecords", "saveState"];
  vm.runInNewContext(functions.map(extractFunction).join("\n") + `\nglobalThis.hooks = { ${functions.join(", ")} };`, context);
  return context;
}

const deferred = () => {
  let resolve;
  const promise = new Promise(value => { resolve = value; });
  return { promise, resolve };
};

async function scenarioDeletionWhileLoading() {
  let server = { doubles: [{ id: "2.1" }, { id: "2.2" }], relays: [], notifications: [], auditLogs: [] };
  const firstLoad = deferred();
  let loads = 0;
  const repository = {
    load: () => ++loads === 1 ? firstLoad.promise : Promise.resolve(structuredClone(server)),
    save: value => { server = structuredClone(value); return Promise.resolve(); }
  };
  const harness = createHarness(repository, structuredClone(server));
  const saveA = harness.hooks.saveState();
  harness.state.doubles = [{ id: "2.1" }];
  harness.hooks.recordDeletedStateRecord("doubles", "2.2");
  const saveB = harness.hooks.saveState();
  firstLoad.resolve(structuredClone(server));
  await Promise.all([saveA, saveB]);
  assert.deepStrictEqual(server.doubles.map(item => item.id), ["2.1"], "A deletion during an earlier load was resurrected.");
}

async function scenarioFailedSaveRetainsTombstone() {
  let server = { doubles: [{ id: "2.1" }, { id: "2.2" }], relays: [], notifications: [], auditLogs: [] };
  let fail = true;
  const repository = {
    load: () => Promise.resolve(structuredClone(server)),
    save: value => fail ? Promise.reject(Object.assign(new Error("simulated save failure"), { status: 400 })) : (server = structuredClone(value), Promise.resolve())
  };
  const harness = createHarness(repository, { ...structuredClone(server), doubles: [{ id: "2.1" }] });
  harness.hooks.recordDeletedStateRecord("doubles", "2.2");
  await assert.rejects(harness.hooks.saveState(), /simulated save failure/);
  assert.strictEqual(harness.pendingDeletedDoubles.has("2.2"), true, "Failed save acknowledged its deletion tombstone.");
  fail = false;
  await harness.hooks.saveState();
  assert.deepStrictEqual(server.doubles.map(item => item.id), ["2.1"], "Retry did not preserve the pending deletion.");
  assert.strictEqual(harness.pendingDeletedDoubles.has("2.2"), false, "Successful retry did not acknowledge the exact tombstone.");
}

async function scenarioLaterGenerationSurvivesOlderSave() {
  let server = { doubles: [{ id: "2.1" }, { id: "2.2" }], relays: [], notifications: [], auditLogs: [] };
  const loadGate = deferred();
  const saveGate = deferred();
  const saveStarted = deferred();
  const repository = {
    load: () => loadGate.promise.then(() => structuredClone(server)),
    save: value => { saveStarted.resolve(); return saveGate.promise.then(() => { server = structuredClone(value); }); }
  };
  const harness = createHarness(repository, { ...structuredClone(server), doubles: [{ id: "2.1" }] });
  harness.hooks.recordDeletedStateRecord("doubles", "2.2");
  const save = harness.hooks.saveState();
  await Promise.resolve();
  loadGate.resolve();
  await saveStarted.promise;
  harness.hooks.recordDeletedStateRecord("doubles", "2.2");
  saveGate.resolve();
  await save;
  assert.strictEqual(harness.pendingDeletedDoubles.has("2.2"), true, "A newer deletion generation was acknowledged by an older save.");
}

async function scenarioUnknownOutcomeStopsQueuedWrites() {
  const server = { doubles: [{ id: "2.1" }, { id: "2.2" }], relays: [], notifications: [], auditLogs: [] };
  const gate = deferred();
  let posts = 0;
  const repository = {
    load: () => Promise.resolve(structuredClone(server)),
    save: () => { posts++; return gate.promise; }
  };
  const harness = createHarness(repository, { ...structuredClone(server), doubles: [{ id: "2.1" }] });
  harness.hooks.recordDeletedStateRecord("doubles", "2.2");
  const first = harness.hooks.saveState();
  const queued = harness.hooks.saveState();
  const outcomes = Promise.allSettled([first, queued]);
  gate.resolve(Promise.reject(new Error("unknown response")));
  const rejected = await outcomes;
  assert.strictEqual(rejected[0].status, "rejected");
  assert.strictEqual(rejected[1].reason.outcome, "state-save-cancelled");
  assert.strictEqual(posts, 1, "A queued save may not replay after an unknown outcome.");
  assert.strictEqual(harness.pendingDeletedDoubles.has("2.2"), true);
  const blocked = harness.hooks.saveState();
  assert.equal(typeof blocked?.then, "function", "The extracted async function must return a promise.");
  await assert.rejects(blocked, e => e.outcome === "state-review-required");
  assert.strictEqual(posts, 1);
  assert.deepStrictEqual(harness.state.doubles.map(item => item.id), ["2.1"]);
}

(async () => {
  await scenarioUnknownOutcomeStopsQueuedWrites();
  console.log("PASS ENG05B unknown outcome cancels queued saves and retains draft");
  await scenarioDeletionWhileLoading();
  console.log("PASS ASYNC-TEAM-DELETION-RACE");
  await scenarioFailedSaveRetainsTombstone();
  console.log("PASS ASYNC-FAILED-SAVE-RETENTION");
  await scenarioLaterGenerationSurvivesOlderSave();
  console.log("PASS ASYNC-LATER-GENERATION");
  console.log("Async save-state regression tests passed.");
})().catch(error => { console.error(error); process.exitCode = 1; });
