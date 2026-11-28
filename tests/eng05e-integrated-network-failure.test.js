"use strict";
// Shared synthetic authority, actual client modules and extracted production
// coordination functions. No production data, credentials, or network I/O.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const app = read("app.js"), publicApp = read("results/results.js");
const clone = structuredClone;
function extract(source, from, to) {
  const i = source.indexOf(from), j = source.indexOf(to, i + from.length);
  assert.ok(i >= 0 && j > i, "Missing source anchor " + from);
  return source.slice(i, j);
}
function response(body, status = 200, etag = null) {
  return { ok: status >= 200 && status < 300, status,
    headers: { get: name => name.toLowerCase() === "etag" ? etag : null },
    json: async () => clone(body), text: async () => JSON.stringify(body) };
}
function row(participant, revision, time) {
  return { id: participant, stage: "Finals", type: "Individual", participant, event: "Cycle",
    attempts: [time], penalty: 0, revision };
}
function gate() {
  let resolve; const promise = new Promise(done => { resolve = done; }); return { promise, resolve };
}
function authority() {
  const server = {
    stateRevision: 4, resultRevision: 10,
    state: { settings: { name: "Synthetic" }, stackers: [], results: [], doubles: [], relays: [], notifications: [], auditLogs: [] },
    rows: [row("A", 10, 5), row("B", 9, 7)],
    token: { one: "one-1", two: "two-1" }, requests: [], stateCommits: 0, resultCommits: 0,
    dropState: false, dropResult: false, publicStatus: 200, delayedPublic: null, failedReads: new Set()
  };
  server.fetch = actor => async (url, options = {}) => {
    const method = options.method || "GET";
    server.requests.push({ actor, url, method, token: options.headers?.Authorization });
    if (actor !== "public" && options.headers?.Authorization !== "Bearer " + server.token[actor])
      return response({ error: "Unauthorized" }, 401);
    if (method === "GET" && server.failedReads.has(actor)) throw new Error("synthetic disconnected read");
    if (url.startsWith("/api/state/")) {
      if (method === "GET") return response(server.state, 200, '"' + server.stateRevision + '"');
      if (options.headers["If-Match"] !== '"' + server.stateRevision + '"')
        return response({ error: "State conflict", currentRevision: server.stateRevision }, 409, '"' + server.stateRevision + '"');
      server.state = JSON.parse(options.body); server.stateRevision++; server.stateCommits++;
      if (server.dropState) { server.dropState = false; throw new Error("state response lost"); }
      return response(null, 204, '"' + server.stateRevision + '"');
    }
    if (url.startsWith("/api/public/")) {
      if (server.publicStatus !== 200) return response({}, server.publicStatus);
      const snapshot = response({ competition: { name: server.state.settings.name }, settings: {}, lastUpdatedAt: "constant",
        results: clone(server.rows), stackers: [], doubles: [], relays: [], divisions: [] });
      if (server.delayedPublic) { const hold = server.delayedPublic; server.delayedPublic = null; await hold.promise; }
      return snapshot;
    }
    assert.match(url, /^\/api\/competitions\/7\/results(?:\/batch)?$/);
    if (method === "GET") return response({ revision: server.resultRevision, results: server.rows });
    const body = JSON.parse(options.body), changes = [...body.upserts, ...body.deletes];
    for (const change of changes) {
      const existing = server.rows.find(item => item.participant === change.participant);
      if ((existing?.revision ?? 0) !== change.expectedRevision) return response({ error: "Result conflict" }, 409);
    }
    server.resultRevision++; server.resultCommits++;
    server.rows = server.rows.filter(item => !changes.some(change => change.participant === item.participant));
    const changed = body.upserts.map(item => row(item.participant, server.resultRevision, item.attempts[0]));
    server.rows.push(...changed);
    if (server.dropResult) { server.dropResult = false; throw new Error("result response lost"); }
    return response({ revision: server.resultRevision, results: changed });
  };
  return server;
}
function operator(server, actor) {
  let token = server.token[actor];
  const ctx = {
    structuredClone, fetch: server.fetch(actor), console: { warn() {} },
    state: clone(server.state), pendingSave: Promise.resolve(), queuedSaveCount: 0, stateSaveGeneration: 0, stateSaveFailure: null,
    deletionGeneration: 0, pendingDeletedDoubles: new Map(), pendingDeletedRelays: new Map(),
    pendingRemoteCompetitionState: null, pendingResultsRefresh: false, pendingStateRefresh: false,
    pendingStateRevision: 0, pendingResultRevision: 0, operatorSyncInFlight: false, operatorSyncRequestedAgain: false,
    operatorStatePresentationPending: false, operatorResultPresentationPending: false,
    currentCompetitionRevision: 4, currentResultsRevision: 10, resultCompetitionEpoch: 1, resultsLoadedForEpoch: -1,
    selectedSqlCompetitionId: 7, ageCalculationMode: "actual", route: "reports",
    resultSyncDiagnostics: {}, editing: false, confirmed: false, prelimResultEditor: null, finalResultEditor: null,
    inputs: [{ value: "5", readOnly: false }], document: { activeElement: null, getElementById() { return null; } },
    currentCompetitionKey: () => "TEST", competitionEditorActive: () => ctx.editing,
    competitionEntryInputActive: () => false, normalizeState: clone, normalizeResults: clone,
    refreshSqlStackers: async () => true, renderReports() {}, refreshResultsDisplayAfterSync() {},
    visiblePrelimTimeInputs: () => ctx.inputs, visibleFinalTimeInputs: () => ctx.inputs,
    t: value => value, setSaveStatus() {}, flashMessage: null
  };
  ctx.window = { StackMeetAuth: { authHeaders: () => ({ Authorization: "Bearer " + token }) }, confirm: () => ctx.confirmed, alert() {} };
  vm.createContext(ctx);
  for (const file of ["js/storage/ApiProvider.js", "js/storage/Repository.js", "js/storage/ResultApi.js"]) vm.runInContext(read(file), ctx);
  ctx.repository = vm.runInContext('new window.StackMeetStorage.Repository("TEST")', ctx);
  ctx.resultApi = vm.runInContext("new window.StackMeetStorage.ResultApi()", ctx);
  ctx.CompetitionRepository = ctx.window.StackMeetStorage.Repository;
  vm.runInContext(
    extract(app, "function legacyStateForSave(", "function normalizeState(")
    + "\n" + extract(app, "function mergeConcurrentState(", "function setSaveStatus(")
    + "\n" + extract(app, "async function refreshSqlResults(", "async function saveSqlResults(")
    + "\n" + extract(app, "function resultEditorInputs(", "function leaveResultEditors(")
    + "\n" + extract(app, "function loadLatestCompetition()", "function competitionEditorActive()")
    + "\n" + extract(app, "function applyPendingCompetitionUpdate()", "let competitionStateConnection")
    + "\n" + extract(app, "function operatorSyncEditing()", "async function connectCompetitionStateUpdates()"), ctx);
  ctx.withoutLegacyStackers = value => ctx.CompetitionRepository.withoutLegacyStackers(value);
  ctx.renew = () => { token = server.token[actor]; };
  return ctx;
}
function viewer(server) {
  const visible = new Map(), nodes = new Map();
  const el = id => {
    if (!nodes.has(id)) nodes.set(id, { replaceChildren() {}, removeAttribute() {}, dataset: {}, querySelector() { return null; } });
    return nodes.get(id);
  };
  const ctx = {
    fetch: server.fetch("public"), endpoint: "/api/public/competitions/TEST/results", AbortController,
    window: { setTimeout, clearTimeout }, refreshInFlight: false, refreshPending: false, pendingLoader: false,
    lastPayloadSignature: "", lastVersion: "", lastErrorMessage: "", currentPayload: null,
    liveSubscription: false, liveJoinGeneration: 0, verifiedJoinGeneration: -1, refreshHealthy: false,
    publicTranslations: {}, hasExplicitLanguage: true, activeLanguage: "en", safeTranslations: () => ({}),
    applyStaticTranslations() {}, el, document: {}, t: value => value, text() {}, setConnection() {},
    show(id, value) { visible.set(id, value); }, render(payload) { visible.set("medals", true); ctx.rendered = clone(payload.results); }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(publicApp, "async function refresh(showLoader)", "function publicBrandingPath")
    + "\n" + extract(publicApp, "function renderError(", "function setConnection("), ctx);
  return ctx;
}
const edit = (participant, expectedRevision, time) => ({ upserts: [{ ...row(participant, expectedRevision, time), expectedRevision }], deletes: [] });
const canonical = rows => JSON.stringify([...rows].sort((a,b) => a.participant.localeCompare(b.participant)));
function closeAttempt(ctx, expected) {
  let warned = false;
  const event = { preventDefault() { warned = true; } };
  assert.equal(ctx.protectUnsavedCompetitionWork(event), expected);
  assert.equal(warned, expected);
}
(async () => {
  const server = authority(), a = operator(server, "one"), b = operator(server, "two"), p = viewer(server);
  for (const operator of [a,b]) {
    operator.state = await operator.repository.load();
    await operator.refreshSqlResults({ background: true });
  }
  await p.refresh(false);
  assert.equal(canonical(a.state.results), canonical(b.state.results));
  assert.equal(canonical(a.state.results), canonical(p.currentPayload.results));

  // One lost state response, queued duplicate, and expired token while reviewing.
  a.state.settings.name = "Saved name"; server.dropState = true;
  const first = a.saveState(), queued = a.saveState();
  const failures = await Promise.allSettled([first, queued]);
  assert.equal(failures[0].status, "rejected");
  assert.equal(failures[1].reason.outcome, "state-save-cancelled");
  assert.equal(server.stateCommits, 1);
  assert.equal(a.repository.provider.stateEtag, '"4"');
  closeAttempt(a, true);
  a.state.settings.name = "Remaining local draft";
  server.token.one = "one-2";
  assert.equal(await a.reviewCompetitionSave(), false);
  assert.equal(a.state.settings.name, "Remaining local draft");
  assert.equal(a.stateSaveFailure.requiresReview, true);
  a.renew(); // Simulate an in-place replacement token, not the login UI.
  assert.equal(await a.reviewCompetitionSave(), false, "Cancel must keep a differing draft.");
  assert.equal(a.state.settings.name, "Remaining local draft");
  assert.equal(server.stateCommits, 1);
  await b.requestOperatorRefresh({ state: true });
  assert.equal(b.state.settings.name, "Saved name");
  a.confirmed = true;
  assert.equal(await a.reviewCompetitionSave(), true);
  assert.equal(a.state.settings.name, b.state.settings.name);
  assert.equal(a.repository.provider.stateEtag, '"5"');
  assert.equal(server.stateCommits, 1);
  console.log("PASS ENG05E state lost response, queued cancellation, token replacement and explicit draft disposition");

  // An active scoring editor protects its unsaved input; another operator
  // commits, the response is lost, and neither client can silently overwrite.
  a.inputs[0].value = "8"; a.prelimResultEditor = { kind: "prelim", initialInput: '["5"]', saving: false, reviewRequired: false };
  a.editing = true; closeAttempt(a, true);
  server.dropResult = true;
  await assert.rejects(b.resultApi.saveBatch(7, edit("A", 10, 6)), error => error.outcome === "unknown");
  await assert.rejects(b.resultApi.saveBatch(7, edit("A", 10, 6)), error => error.outcome === "review-required");
  await a.requestOperatorRefresh({ results: true, resultsRevision: 11 });
  assert.equal(a.pendingResultsRefresh, true);
  assert.equal(a.inputs[0].value, "8");
  await assert.rejects(a.resultApi.saveBatch(7, edit("A", 10, 8)), error => error.status === 409);
  assert.equal(server.resultCommits, 1);
  server.failedReads.add("two");
  await assert.rejects(b.resultApi.review(7), /disconnected/);
  await assert.rejects(b.resultApi.saveBatch(7, edit("A", 10, 6)), error => error.outcome === "review-required");
  server.failedReads.delete("two");
  const reviewed = await b.resultApi.review(7);
  assert.equal(b.resultApi.acceptReview(7, reviewed.revision), true);
  assert.equal(reviewed.results.find(item => item.participant === "A").attempts[0], 6);
  await p.refresh(false);
  assert.equal(p.currentPayload.results.find(item => item.participant === "A").attempts[0], 6);

  // Public GET started before deletion; event during GET demands a trailing
  // authoritative read, even when the timestamp never changes.
  const hold = gate(); server.delayedPublic = hold;
  const oldPublic = p.refresh(false);
  await b.resultApi.saveBatch(7, { upserts: [], deletes: [{ ...row("B", 9, 7), expectedRevision: 9 }] });
  void p.refresh(false);
  hold.resolve(); await oldPublic;
  assert.equal(server.resultCommits, 2);
  assert.equal(p.currentPayload.results.some(item => item.participant === "B"), false);
  assert.equal(a.inputs[0].value, "8");
  a.editing = false;
  // Operator explicitly reviews/discards the unsaved score in this fixture.
  a.prelimResultEditor.reviewRequired = true;
  closeAttempt(a, true);
  a.prelimResultEditor = null;
  await a.requestOperatorRefresh({ state: true, results: true });
  await b.requestOperatorRefresh({ state: true, results: true });
  assert.equal(canonical(a.state.results), canonical(server.rows));
  assert.equal(canonical(b.state.results), canonical(server.rows));
  assert.equal(canonical(p.currentPayload.results), canonical(server.rows));
  closeAttempt(a, false);
  server.publicStatus = 404; await p.refresh(false);
  assert.equal(p.currentPayload, null);
  server.publicStatus = 200; await p.refresh(false);
  assert.equal(canonical(p.currentPayload.results), canonical(server.rows));
  assert.equal(server.requests.filter(item => item.method === "POST").length, 4, "No automatic duplicate POST.");
  console.log("PASS ENG05E two operators and public converge after lost response, stale rejection, deletion and withdrawal");
  console.log("PASS ENG05E close guard warns for failed state and dirty/review-required scoring draft");
  console.log("ENG05E synthetic shared-authority integration passed; real browser/authentication and transport remain open.");
})().catch(error => { console.error(error); process.exitCode = 1; });
