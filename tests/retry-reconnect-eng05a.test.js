"use strict";

// ENG-05A characterizes current transport and notification behavior. All servers,
// network failures and hub callbacks are deterministic in-memory test doubles.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
global.window = { StackMeetStorage: {}, StackMeetAuth: { authHeaders: () => ({ Authorization: "Bearer synthetic" }) } };
const ApiProvider = require(path.join(root, "js/storage/ApiProvider.js"));
const ResultApi = require(path.join(root, "js/storage/ResultApi.js"));

const clone = value => structuredClone(value);
function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
function response(body, status = 200, etag = null) {
  return {
    ok: status >= 200 && status < 300, status,
    headers: { get: name => name.toLowerCase() === "etag" ? etag : null },
    json: async () => clone(body),
    text: async () => JSON.stringify(body)
  };
}
function result(participant, revision, time = 5) {
  return { id: "synthetic-" + participant, stage: "Finals", type: "Individual",
    participant, event: "Cycle", attempts: [time], penalty: 0, revision };
}
const edit = (participant, expectedRevision, time) => ({
  upserts: [{ stage: "Finals", type: "Individual", participant, event: "Cycle",
    expectedRevision, attempts: [time], penalty: 0 }], deletes: []
});

async function stateResponseLost() {
  let revision = 4, stored = { settings: { name: "Before" } }, drop = true;
  const calls = [];
  global.fetch = async (url, options = {}) => {
    const method = options.method || "GET";
    calls.push({ method, match: options.headers["If-Match"] });
    if (method === "GET") return response(stored, 200, '"' + revision + '"');
    if (options.headers["If-Match"] !== '"' + revision + '"')
      return response({ error: "State conflict", currentRevision: revision }, 409, '"' + revision + '"');
    stored = JSON.parse(options.body);
    revision++;
    if (drop) { drop = false; throw new Error("synthetic response lost after commit"); }
    return response(null, 204, '"' + revision + '"');
  };
  const provider = new ApiProvider("SYNTHETIC");
  await provider.load();
  const draft = { settings: { name: "Changed" } };
  await assert.rejects(provider.save(draft), /response lost/);
  assert.equal(revision, 5);
  assert.equal(provider.stateEtag, '"4"', "A lost response does not acknowledge a revision.");
  assert.equal((await provider.load()).settings.name, "Changed");
  assert.equal(provider.stateEtag, '"4"', "Ordinary load does not silently rebase a draft.");
  await assert.rejects(provider.save(draft), e => e.status === 409 && e.currentRevision === 5);
  assert.equal(revision, 5, "Retry with stale ETag must not commit twice.");
  assert.deepEqual(await provider.reloadLatest(), draft);
  assert.equal(provider.stateEtag, '"5"');
  assert.equal(calls.filter(x => x.method === "POST").length, 2);
  console.log("PASS ENG05A state lost-response and explicit revision acceptance");
}

async function resultResponseLost() {
  let revision = 10, rows = [result("A", 10), result("B", 9, 7)];
  let lost = false, failRead = false, posts = 0;
  global.fetch = async (url, options = {}) => {
    if ((options.method || "GET") === "GET") {
      if (failRead) throw new Error("synthetic offline GET");
      return response({ revision, results: rows });
    }
    posts++;
    const body = JSON.parse(options.body);
    const item = body.upserts[0];
    const old = rows.find(row => row.participant === item.participant);
    if (!old || old.revision !== item.expectedRevision)
      return response({ error: "Conflict" }, 409);
    revision++;
    const changed = result(item.participant, revision, item.attempts[0]);
    rows = rows.map(row => row.participant === item.participant ? changed : row);
    if (lost) { lost = false; throw new Error("synthetic post-commit disconnect"); }
    return response({ revision, results: [changed] });
  };
  const a = new ResultApi(), b = new ResultApi();
  await Promise.all([a.list(7), b.list(7)]);
  lost = true;
  const draft = edit("A", 10, 6);
  await assert.rejects(a.saveBatch(7, draft), e => e.outcome === "unknown");
  assert.equal(revision, 11);
  assert.deepEqual(draft.upserts[0].attempts, [6], "Caller retains input after transport failure.");
  await assert.rejects(a.saveBatch(7, draft), e => e.outcome === "review-required");
  assert.equal(posts, 1, "Unknown outcome cannot automatically replay.");
  await assert.rejects(b.saveBatch(7, edit("A", 10, 8)), e => e.status === 409 && e.outcome === "rejected");
  assert.equal(revision, 11, "Stale second client cannot overwrite the committed result.");
  failRead = true;
  await assert.rejects(a.review(7), /offline GET/);
  await assert.rejects(a.saveBatch(7, draft), e => e.outcome === "review-required");
  failRead = false;
  const reviewed = await a.review(7);
  assert.equal(reviewed.revision, 11);
  assert.equal(reviewed.results.find(row => row.participant === "A").attempts[0], 6);
  assert.equal(a.acceptReview(7, 10), false);
  assert.equal(a.acceptReview(7, 11), true);
  const repaired = await a.saveBatch(7, edit("A", 11, 6.5));
  assert.equal(repaired.revision, 12);
  assert.equal((await b.list(7)).revision, 12);
  assert.equal(posts, 3);
  console.log("PASS ENG05A result unknown-outcome barrier and two-client convergence");
}

async function lateSnapshotAfterDelete() {
  const late = deferred();
  let calls = 0;
  global.fetch = async (url, options = {}) => {
    calls++;
    if (calls === 1) return response({ revision: 20, results: [result("A", 20), result("B", 19)] });
    if (calls === 2) return late.promise;
    if (calls === 3) {
      assert.equal(options.method, "POST");
      return response({ revision: 21, results: [] });
    }
    throw new Error("Unexpected fetch");
  };
  const api = new ResultApi();
  await api.list(7);
  const oldRequest = api.list(7);
  const deleted = await api.saveBatch(7, { upserts: [], deletes: [{
    stage: "Finals", type: "Individual", participant: "A", event: "Cycle", expectedRevision: 20
  }] });
  assert.equal(deleted.revision, 21);
  late.resolve(response({ revision: 20, results: [result("A", 20), result("B", 19)] }));
  const snapshot = await oldRequest;
  assert.equal(snapshot.revision, 21);
  assert.deepEqual(snapshot.results.map(x => x.participant), ["B"], "Late GET cannot resurrect a deleted row.");
  console.log("PASS ENG05A delayed snapshot after delete");
}

async function confirmedCommitFailedRefresh() {
  const calls = [];
  let fail = true;
  global.fetch = async (url, options = {}) => {
    const method = options.method || "GET";
    calls.push(method);
    if (method === "POST") return response({ revision: 31, results: [result("A", 31, 6)] });
    if (fail) throw new Error("synthetic refresh unavailable");
    return response({ revision: 31, results: [result("A", 31, 6)] });
  };
  const api = new ResultApi(), draft = edit("A", 30, 6);
  // No cached full list: acknowledged delta requires authoritative GET.
  await assert.rejects(api.saveBatch(7, draft), e => e.outcome === "committed-refresh-failed");
  await assert.rejects(api.saveBatch(7, draft), e => e.outcome === "review-required");
  assert.deepEqual(calls, ["POST", "GET"]);
  fail = false;
  const review = await api.review(7);
  assert.equal(review.revision, 31);
  assert.equal(api.acceptReview(7, 31), true);
  console.log("PASS ENG05A confirmed commit with failed refresh");
}

function extract(source, start, end) {
  const from = source.indexOf(start);
  assert.notEqual(from, -1, "Missing source start: " + start);
  const to = source.indexOf(end, from + start.length);
  assert.ok(to > from, "Missing source end: " + end);
  return source.slice(from, to);
}
function hubDouble(trace) {
  const events = new Map(), lifecycle = {};
  const hub = {
    state: "Connected", connectionId: "synthetic",
    on(name, handler) { events.set(name, handler); },
    onreconnecting(handler) { lifecycle.reconnecting = handler; },
    onreconnected(handler) { lifecycle.reconnected = handler; },
    onclose(handler) { lifecycle.close = handler; },
    async start() { trace.push("start"); },
    async invoke(name) { trace.push(name); },
    async stop() { trace.push("stop"); }
  };
  class Builder {
    withUrl() { return this; } withAutomaticReconnect() { return this; }
    configureLogging() { return this; } build() { return hub; }
  }
  return { events, lifecycle, hub, signalR: { HubConnectionBuilder: Builder, LogLevel: { Warning: 3 } } };
}

async function operatorNotifications() {
  const source = fs.readFileSync(path.join(root, "app.js"), "utf8");
  const trace = [], h = hubDouble(trace);
  let failState = false, failResults = false, editing = false, resultReads = 0, stateReads = 0;
  let serverStateRevision = 4, serverResultsRevision = 10;
  let stateGate = null, resultGate = null;
  const ctx = {
    window: { signalR: h.signalR, StackMeetAuth: { readSession: () => ({ token: "synthetic" }) }, setTimeout: () => 1 },
    signalR: h.signalR, competitionStateConnection: null, competitionStateReconnectTimer: null,
    currentCompetitionKey: () => "TEST", currentCompetitionRevision: 4, currentResultsRevision: 10,
    resultSyncDiagnostics: {}, pendingResultsRefresh: false, pendingStateRefresh: false,
    pendingStateRevision: 0, pendingResultRevision: 0, operatorSyncInFlight: false, operatorSyncRequestedAgain: false,
    resultCompetitionEpoch: 1, queuedSaveCount: 0, stateSaveFailure: null,
    competitionEditorActive: () => editing, competitionEntryInputActive: () => false,
    document: { activeElement: null },
    loadLatestCompetition: async () => {
      stateReads++; trace.push("state-read");
      if (failState) throw new Error("synthetic read failure");
      if (stateGate) await stateGate.promise;
      return { etag: '"' + serverStateRevision + '"', state: {} };
    },
    applyRemoteCompetitionState: async () => { trace.push("state-apply"); return !editing; },
    refreshSqlResults: async () => {
      resultReads++; trace.push("result-read");
      if (failResults) throw new Error("synthetic result refresh failure");
      if (resultGate) await resultGate.promise;
      if (editing) return false;
      ctx.currentResultsRevision = serverResultsRevision;
      return true;
    },
    console: { warn() {} }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "function operatorSyncEditing()", "// Creates a stable comparison"), ctx);
  const readKey = ctx.currentCompetitionKey;
  ctx.currentCompetitionKey = () => { throw new Error("Empty refresh must not read competition context"); };
  assert.equal(await ctx.requestOperatorRefresh(), true);
  assert.equal(stateReads, 0);
  assert.equal(resultReads, 0);
  assert.equal(ctx.operatorSyncInFlight, false);
  ctx.currentCompetitionKey = readKey;
  console.log("PASS ENG05C empty refresh does not touch competition context");
  await ctx.connectCompetitionStateUpdates();
  assert.deepEqual(trace, ["start", "JoinOperatorCompetition", "state-read", "state-apply", "result-read"]);
  await h.events.get("ResultsChanged")({ competitionKey: "OTHER", revision: 100 });
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 10 });
  assert.equal(resultReads, 1, "Wrong competition and obsolete result revisions are ignored.");
  editing = true;
  serverResultsRevision = 12;
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 12 });
  assert.equal(ctx.pendingResultsRefresh, true);
  assert.equal(resultReads, 1, "Result notification does not interrupt an active editor.");
  editing = false;
  await ctx.requestOperatorRefresh();
  assert.equal(resultReads, 2);
  assert.equal(ctx.currentResultsRevision, 12);
  assert.equal(ctx.pendingResultsRefresh, false);

  // Replace the ENG-05A GAP-01 characterization with a positive recovery assertion.
  serverStateRevision = 5; failState = true;
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 5 });
  assert.equal(ctx.currentCompetitionRevision, 4, "A failed read must not advance the applied watermark.");
  assert.equal(ctx.pendingStateRefresh, true);
  failState = false;
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 5 });
  assert.equal(ctx.currentCompetitionRevision, 5);
  assert.equal(ctx.pendingStateRefresh, false);
  const readsAfterApply = stateReads;
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 5 });
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 4 });
  assert.equal(stateReads, readsAfterApply, "Applied duplicate and delayed state events must be ignored.");
  console.log("PASS ENG05C GAP-01 failed state refresh retains watermark and retries same revision");

  // Reconnect still repairs SQL results when JSON state fails or has not changed.
  trace.length = 0;
  failState = true; serverResultsRevision = 13;
  await h.lifecycle.reconnected();
  assert.deepEqual(trace, ["JoinOperatorCompetition", "state-read", "result-read"]);
  assert.equal(ctx.currentResultsRevision, 13);
  assert.equal(ctx.pendingResultsRefresh, false);
  assert.equal(ctx.pendingStateRefresh, true);
  failState = false;
  await ctx.requestOperatorRefresh({ state: true, results: true });
  assert.equal(ctx.pendingStateRefresh, false);
  console.log("PASS ENG05C GAP-02 reconnect joins before independent state and result refresh");

  // Failed result read stays pending; successful retry cannot be acknowledged
  // while the server snapshot is behind the notified revision.
  failResults = true;
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 15 });
  assert.equal(ctx.pendingResultsRefresh, true);
  assert.equal(ctx.currentResultsRevision, 13);
  failResults = false; serverResultsRevision = 14;
  await ctx.requestOperatorRefresh();
  assert.equal(ctx.pendingResultsRefresh, true);
  serverResultsRevision = 15;
  await ctx.requestOperatorRefresh();
  assert.equal(ctx.pendingResultsRefresh, false);
  assert.equal(ctx.currentResultsRevision, 15);

  // Burst of notifications during one GET triggers at most a bounded trailing pass.
  stateGate = deferred(); serverStateRevision = 6;
  const startReads = stateReads;
  const inFlight = h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 6 });
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 7 });
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 7 });
  serverStateRevision = 7; stateGate.resolve(); stateGate = null;
  await inFlight;
  assert.equal(ctx.currentCompetitionRevision, 7);
  assert.ok(stateReads - startReads <= 2);
  assert.equal(ctx.pendingStateRefresh, false);

  // Draft protection persists even during independent fallback result refresh.
  ctx.stateSaveFailure = { requiresReview: true };
  serverStateRevision = 8; serverResultsRevision = 16;
  const beforeDraft = stateReads;
  await ctx.requestOperatorRefresh({ state: true, results: true });
  assert.equal(stateReads, beforeDraft);
  assert.equal(ctx.currentResultsRevision, 16);
  assert.equal(ctx.pendingStateRefresh, true);
  ctx.stateSaveFailure = null;
  await ctx.requestOperatorRefresh();
  assert.equal(ctx.currentCompetitionRevision, 8);
  console.log("PASS ENG05C bounded refresh, failed result recovery and draft preservation");
}

async function operatorResultSnapshotRace() {
  const source = fs.readFileSync(path.join(root, "app.js"), "utf8");
  let gate = deferred(), editing = false, renderCount = 0;
  const ctx = {
    selectedSqlCompetitionId: 7, resultCompetitionEpoch: 1, resultsLoadedForEpoch: 1,
    currentResultsRevision: 10, state: { results: [result("A", 10)] },
    resultSyncDiagnostics: {}, pendingResultsRefresh: false, operatorResultPresentationPending: false,
    operatorSyncEditing: () => editing, normalizeResults: clone,
    resultApi: { list: () => gate.promise },
    refreshResultsDisplayAfterSync: () => { renderCount++; }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "async function refreshSqlResults(", "async function saveSqlResults("), ctx);
  const old = ctx.refreshSqlResults({ background: true });
  ctx.resultCompetitionEpoch = 2; ctx.selectedSqlCompetitionId = 8;
  ctx.currentResultsRevision = 0; ctx.state.results = [];
  gate.resolve({ revision: 11, results: [result("OLD", 11)] });
  assert.equal(await old, false);
  assert.equal(ctx.state.results.length, 0, "Old competition response must not repopulate results.");
  gate = deferred();
  const interrupted = ctx.refreshSqlResults({ background: true });
  editing = true;
  gate.resolve({ revision: 2, results: [result("NEW", 2)] });
  assert.equal(await interrupted, false);
  assert.equal(ctx.state.results.length, 0, "Editor opened during GET must remain untouched.");
  assert.equal(renderCount, 0);
  editing = false;
  assert.equal(await ctx.refreshSqlResults({ background: true }), true);
  assert.equal(ctx.state.results[0].participant, "NEW");
  assert.equal(renderCount, 1);
  assert.equal(await ctx.refreshSqlResults({ background: true }), true);
  assert.equal(renderCount, 1, "Unchanged polling snapshot must not rebuild the interface.");
  console.log("PASS ENG05C real result refresh guards epoch, editor race and unchanged presentation");
}

async function publicNotifications() {
  const source = fs.readFileSync(path.join(root, "results/results.js"), "utf8");
  const trace = [], h = hubDouble(trace);
  const payload = (time, results, official = false) => ({
    lastUpdatedAt: time, competition: { name: "Synthetic", isOfficial: official },
    settings: {}, results, stackers: [], doubles: [], relays: [], divisions: []
  });
  const nodes = new Map();
  const node = id => {
    if (!nodes.has(id)) nodes.set(id, { hidden: false, children: ["old"], textContent: "", dataset: {},
      replaceChildren(...children) { this.children = children; },
      removeAttribute(name) { delete this[name]; },
      querySelector() { return null; }
    });
    return nodes.get(id);
  };
  let reads = 0, active = 0, maxActive = 0;
  const responses = [];
  let timeoutCallback = null;
  const ctx = {
    window: { signalR: h.signalR, setTimeout(fn, delay) { assert.equal(delay, 10000); timeoutCallback = fn; return 1; }, clearTimeout() { timeoutCallback = null; } }, signalR: h.signalR, competitionId: "TEST", endpoint: "/synthetic",
    AbortController, refreshInFlight: false, refreshPending: false, pendingLoader: false,
    liveSubscription: false, liveJoinGeneration: 0, verifiedJoinGeneration: -1, refreshHealthy: false,
    lastVersion: "", lastPayloadSignature: "", lastErrorMessage: "", currentPayload: null,
    publicTranslations: {}, hasExplicitLanguage: true, activeLanguage: "en",
    el: node, document: { title: "" }, t: value => value,
    show(id, visible) { node(id).hidden = !visible; },
    text(id, value) { node(id).textContent = value; },
    safeTranslations: () => ({}), applyStaticTranslations() {}, updatePublicNavigation() {},
    i18n: { normalizeLanguageCode: value => value },
    setConnection: (state, label) => trace.push("connection:" + state + ":" + label),
    render: data => {
      trace.push("render:" + data.lastUpdatedAt);
      node("medals").hidden = false;
      node("medalRows").children = data.results.map(row => row.participant);
      node("errorState").hidden = true;
    },
    fetch: async (url, options) => {
      reads++; active++; maxActive = Math.max(maxActive, active); trace.push("GET");
      assert.equal(options.cache, "no-store");
      assert.ok(options.signal);
      try {
        assert.ok(responses.length, "unexpected GET");
        const next = responses.shift();
        return await (typeof next === "function" ? next(options.signal) : next);
      } finally { active--; }
    }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "async function refresh(showLoader)", "function publicBrandingPath")
    + "\n" + extract(source, "async function connectLiveUpdates()", "function setConnection(")
    + "\n" + extract(source, "function updatePublicLanguage(", 'el("publicLanguage")?.addEventListener'), ctx);
  responses.push(response(payload("v1", [result("A", 1)])));
  await ctx.connectLiveUpdates();
  assert.deepEqual(trace.slice(0, 2), ["start", "JoinCompetition"]);
  assert.equal(ctx.refreshHealthy, true);
  assert.equal(trace.at(-1), "connection:live:Connected");

  // Multiple hints during an in-flight GET require exactly one trailing GET.
  const pending = deferred();
  responses.push(pending.promise, response(payload("v1", [])));
  const first = ctx.refresh(false);
  h.events.get("ResultsChanged")();
  h.events.get("CompetitionChanged")();
  assert.equal(reads, 2);
  pending.resolve(response(payload("v1", [result("A", 1)])));
  await first;
  assert.equal(reads, 3);
  assert.equal(maxActive, 1);
  assert.equal(ctx.currentPayload.results.length, 0, "Trailing GET must apply deletion even with unchanged timestamp.");
  assert.deepEqual(node("medalRows").children, []);
  assert.equal(ctx.refreshPending, false);
  console.log("PASS ENG05D GAP-03 public notifications coalesce and same-timestamp deletion renders");

  // Metadata/status change with same timestamp must render; unchanged data must not.
  const renderCount = () => trace.filter(item => item.startsWith("render:")).length;
  const before = renderCount();
  responses.push(response(payload("v1", [], true)), response(payload("v1", [], true)));
  await ctx.refresh(false); await ctx.refresh(false);
  assert.equal(renderCount(), before + 1);
  assert.equal(ctx.currentPayload.competition.isOfficial, true);

  // Reconnect must rejoin, then retrieve the authoritative projection.
  trace.length = 0;
  h.lifecycle.reconnecting();
  responses.push(response(payload("v2", [result("B", 2)])));
  await h.lifecycle.reconnected();
  assert.ok(trace.indexOf("JoinCompetition") >= 0 && trace.indexOf("GET") > trace.indexOf("JoinCompetition"));
  assert.equal(ctx.lastVersion, "v2||");
  assert.equal(trace.at(-1), "connection:live:Connected");

  // A denied/withdrawn public endpoint cannot leave previously rendered medals
  // or cached results available for a subsequent language-triggered render.
  responses.push(response({}, 404));
  await ctx.refresh(false);
  assert.equal(ctx.currentPayload, null);
  assert.equal(ctx.lastVersion, "");
  assert.equal(ctx.lastPayloadSignature, "");
  assert.equal(node("medals").hidden, true);
  assert.equal(node("dashboard").hidden, true);
  assert.equal(node("disclaimer").hidden, true);
  assert.deepEqual(node("medalRows").children, []);
  assert.equal(node("competitionName").textContent, "Competition Results");
  assert.equal(node("errorState").hidden, false);
  assert.match(node("errorMessage").textContent, /does not exist/);
  assert.equal(trace.at(-1), "connection:offline:Reconnecting");
  const renderedBeforeLanguage = renderCount();
  ctx.updatePublicLanguage("ms", false);
  assert.equal(renderCount(), renderedBeforeLanguage, "Language switch cannot resurrect a withdrawn payload.");
  assert.equal(node("medals").hidden, true);
  responses.push(response(payload("v2", [result("B", 2)])));
  await ctx.refresh(false);
  assert.equal(node("medals").hidden, false);
  assert.deepEqual(node("medalRows").children, ["B"]);
  assert.equal(ctx.lastErrorMessage, "");
  responses.push(response({}, 503));
  await ctx.refresh(false);
  assert.equal(ctx.currentPayload, null, "Transient failure also hides stale standings.");
  assert.equal(node("medals").hidden, true);
  responses.push(response({ results: [] }));
  await ctx.refresh(false);
  assert.equal(ctx.currentPayload, null, "Malformed success cannot be published.");
  responses.push(response(payload("v2", [])));
  await ctx.refresh(false);
  assert.equal(ctx.currentPayload.results.length, 0);
  // A stalled request must time out and free the single-flight coordinator.
  responses.push(signal => new Promise((resolve, reject) => signal.addEventListener("abort", () => reject(new Error("aborted")), { once: true })));
  const stalled = ctx.refresh(false);
  assert.equal(typeof timeoutCallback, "function");
  timeoutCallback();
  await stalled;
  assert.equal(ctx.refreshInFlight, false);
  assert.equal(ctx.currentPayload, null);
  responses.push(response(payload("v2", [])));
  await ctx.refresh(false);
  assert.equal(ctx.refreshHealthy, true);
  h.lifecycle.close();
  assert.equal(trace.at(-1), "connection:offline:Auto refresh");
  // An unlisted competition may be denied push but still have an allowed HTTP direct link.
  const readsBeforeDeniedJoin = reads;
  h.hub.invoke = async () => { trace.push("denied-join"); throw new Error("subscription unavailable"); };
  await ctx.connectLiveUpdates();
  assert.equal(reads, readsBeforeDeniedJoin, "Denied push must not create a resubscription loop.");
  assert.equal(ctx.liveSubscription, false);
  responses.push(response(payload("v3", [])));
  await ctx.refresh(false);
  assert.equal(ctx.lastVersion, "v3||");
  assert.equal(trace.at(-1), "connection:offline:Auto refresh");
  assert.equal(responses.length, 0);
  console.log("PASS ENG05D timeout, language switch and unlisted HTTP fallback");
  console.log("PASS ENG05D GAP-04 failed and withdrawn projections clear cache and all standings; polling recovers");
}

async function stateDraftReview() {
  const source = fs.readFileSync(path.join(root, "app.js"), "utf8");
  const original = { settings: { name: "Draft" }, stackers: [{ id: "S" }], results: [{ id: "R" }] };
  const observation = { baseline: '"4"', etag: '"5"', state: { settings: { name: "Remote" }, stackers: [], results: [] } };
  let accepts = 0, confirmed = false, editing = true;
  const ctx = {
    state: clone(original), queuedSaveCount: 0, stateSaveFailure: null, stateSaveGeneration: 0, ageCalculationMode: "actual",
    pendingRemoteCompetitionState: null, pendingResultsRefresh: false,
    pendingDeletedDoubles: new Map([["D", 1]]), pendingDeletedRelays: new Map(),
    competitionEditorActive: () => editing, competitionEntryInputActive: () => false,
    repository: { observeLatestCompetition: async () => observation, acceptObservedCompetition: () => { accepts++; return true; } },
    legacyStateForSave: value => ({ ...clone(value), stackers: [], results: [] }),
    normalizeState: value => clone(value), withoutLegacyStackers: value => clone(value),
    selectedSqlCompetitionId: null, route: "reports", renderReports() {}, resultCompetitionEpoch: 1, operatorSyncEditing: () => editing, operatorStatePresentationPending: false,
    document: { activeElement: null }, window: { confirm: () => confirmed, alert() {} },
    t: value => value, setSaveStatus() {}, console: { warn() {} }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "async function applyRemoteCompetitionState(", "function competitionEditorActive()")
    + "\n" + extract(source, "async function reviewCompetitionSave()", "let competitionStateConnection"), ctx);
  assert.equal(await ctx.applyRemoteCompetitionState(observation), false);
  assert.equal(accepts, 0);
  assert.equal(ctx.state.settings.name, "Draft");
  editing = false;
  ctx.queuedSaveCount = 1;
  assert.equal(await ctx.applyRemoteCompetitionState(observation), false);
  assert.equal(accepts, 0);
  ctx.queuedSaveCount = 0;
  ctx.stateSaveFailure = { requiresReview: true };
  assert.equal(await ctx.applyRemoteCompetitionState(observation), false);
  assert.equal(await ctx.reviewCompetitionSave(), false, "Cancelled review retains the draft.");
  assert.equal(ctx.state.settings.name, "Draft");
  assert.equal(accepts, 0);
  assert.equal(ctx.pendingDeletedDoubles.has("D"), true);
  confirmed = true;
  assert.equal(await ctx.reviewCompetitionSave(), true);
  assert.equal(accepts, 1);
  assert.equal(ctx.state.settings.name, "Remote");
  assert.equal(ctx.state.results[0].id, "R");
  assert.equal(ctx.state.stackers[0].id, "S");
  assert.equal(ctx.stateSaveFailure, null);
  assert.equal(ctx.pendingDeletedDoubles.size, 0);
  console.log("PASS ENG05B active/queued/failed draft deferral and explicit reviewed replacement");
}

(async () => {
  await stateDraftReview();
  await stateResponseLost();
  await resultResponseLost();
  await lateSnapshotAfterDelete();
  await confirmedCommitFailedRefresh();
  await operatorNotifications();
  await operatorResultSnapshotRace();
  await publicNotifications();
  console.log("ENG05A/B/C/D retry/reconnect checks passed; integrated runtime acceptance remains open.");
})().catch(error => { console.error(error); process.exitCode = 1; });
