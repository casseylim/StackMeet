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
  return { events, lifecycle, signalR: { HubConnectionBuilder: Builder, LogLevel: { Warning: 3 } } };
}

async function operatorNotifications() {
  const source = fs.readFileSync(path.join(root, "app.js"), "utf8");
  const trace = [], h = hubDouble(trace);
  let failState = true, editing = false, resultReads = 0;
  const ctx = {
    window: { signalR: h.signalR, StackMeetAuth: { readSession: () => ({ token: "synthetic" }) }, setTimeout: () => 1 },
    signalR: h.signalR, competitionStateConnection: null, competitionStateReconnectTimer: null,
    currentCompetitionKey: () => "TEST", currentCompetitionRevision: 4, currentResultsRevision: 10,
    resultSyncDiagnostics: {}, pendingResultsRefresh: false,
    competitionEditorActive: () => editing,
    loadLatestCompetition: async () => { trace.push("state-read"); if (failState) throw new Error("synthetic read failure"); return {}; },
    applyRemoteCompetitionState: async () => { trace.push("state-apply"); },
    refreshSqlResults: async () => { resultReads++; },
    console: { warn() {} }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "async function connectCompetitionStateUpdates()", "// Creates a stable comparison"), ctx);
  await ctx.connectCompetitionStateUpdates();
  assert.deepEqual(trace, ["start", "JoinOperatorCompetition"]);
  await h.events.get("ResultsChanged")({ competitionKey: "OTHER", revision: 100 });
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 10 });
  assert.equal(resultReads, 0, "Wrong competition and obsolete result revisions are ignored.");
  editing = true;
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 12 });
  assert.equal(ctx.pendingResultsRefresh, true);
  assert.equal(resultReads, 0, "Result notification does not interrupt an active editor.");
  editing = false;
  await h.events.get("ResultsChanged")({ competitionKey: "TEST", revision: 12 });
  assert.equal(resultReads, 1);
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 5 });
  assert.equal(ctx.currentCompetitionRevision, 5);
  failState = false;
  await h.events.get("CompetitionChanged")({ competitionKey: "TEST", revision: 5 });
  // Known gap, not a desired contract: notification revision advances before successful apply.
  assert.equal(trace.filter(x => x === "state-read").length, 1);
  assert.equal(trace.includes("state-apply"), false);
  console.log("OBSERVED ENG05A GAP-01 same state notification ignored after failed refresh");
  trace.length = 0;
  await h.lifecycle.reconnected();
  assert.deepEqual(trace, ["JoinOperatorCompetition", "state-read", "state-apply"]);
  // Reconnect currently has no explicit independent result GET in the handler.
  assert.equal(resultReads, 1);
  console.log("OBSERVED ENG05A GAP-02 operator reconnect handler has no explicit result refresh");
  console.log("PASS ENG05A operator subscription order and editor notification deferral");
}

async function publicNotifications() {
  const source = fs.readFileSync(path.join(root, "results/results.js"), "utf8");
  const trace = [], h = hubDouble(trace), pending = deferred();
  let reads = 0, next = pending.promise;
  const ctx = {
    window: { signalR: h.signalR }, signalR: h.signalR, competitionId: "TEST", endpoint: "/synthetic",
    refreshInFlight: false, liveSubscription: false, lastVersion: "", currentPayload: null,
    publicTranslations: {}, hasExplicitLanguage: true, activeLanguage: "en",
    show() {}, text() {}, safeTranslations: () => ({}), applyStaticTranslations() {},
    setConnection: (state) => trace.push("connection:" + state),
    render: payload => trace.push("render:" + payload.lastUpdatedAt),
    renderError: () => trace.push("render-error"),
    fetch: async () => { reads++; trace.push("GET"); return next; }
  };
  vm.createContext(ctx);
  vm.runInContext(extract(source, "async function refresh(showLoader)", "function publicBrandingPath")
    + "\n" + extract(source, "async function connectLiveUpdates()", "function renderError("), ctx);
  await ctx.connectLiveUpdates();
  assert.deepEqual(trace.slice(0, 2), ["start", "JoinCompetition"]);
  const first = ctx.refresh(false);
  h.events.get("ResultsChanged")();
  h.events.get("CompetitionChanged")();
  assert.equal(reads, 1, "Current in-flight guard coalesces by dropping notifications.");
  pending.resolve(response({ lastUpdatedAt: "v1", settings: {}, results: [result("A", 1)] }));
  await first;
  assert.equal(reads, 1, "Known gap: no trailing refresh after event during GET.");
  console.log("OBSERVED ENG05A GAP-03 public in-flight notifications have no trailing refresh");
  next = Promise.resolve(response({ lastUpdatedAt: "v2", settings: {}, results: [] }));
  await ctx.refresh(false); // Same authoritative GET as the existing polling fallback.
  assert.equal(ctx.currentPayload.results.length, 0);
  assert.equal(reads, 2);
  trace.length = 0;
  next = Promise.resolve(response({ lastUpdatedAt: "v3", settings: {}, results: [] }));
  await h.lifecycle.reconnected();
  // Handler intentionally launches refresh without awaiting it.
  await new Promise(resolve => setImmediate(resolve));
  assert.ok(trace.indexOf("JoinCompetition") >= 0 && trace.indexOf("GET") > trace.indexOf("JoinCompetition"));
  assert.equal(ctx.currentPayload.lastUpdatedAt, "v3", "Reconnect must apply the latest authoritative payload.");
  assert.equal(ctx.lastVersion, "v3||", "Version signature includes timestamp and both empty branding fields.");
  assert.equal(ctx.currentPayload.results.length, 0, "Reconnect must not restore a deleted result.");
  assert.ok(trace.includes("render:v3"), "Reconnect must render the refreshed projection.");
  console.log("PASS ENG05A public reconnect joins before authoritative refresh");
}

(async () => {
  await stateResponseLost();
  await resultResponseLost();
  await lateSnapshotAfterDelete();
  await confirmedCommitFailedRefresh();
  await operatorNotifications();
  await publicNotifications();
  console.log("ENG05A retry/reconnect characterization passed; observed gaps remain open.");
})().catch(error => { console.error(error); process.exitCode = 1; });
