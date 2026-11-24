"use strict";
const assert = require("node:assert/strict");
const Repository = require("../backend/StackMeet.Api/wwwroot/js/storage/Repository.js");

const state = {
    settings: { name: "Synthetic" }, stackers: [{ id: "1.1" }],
    results: [{ id: "r1", attempts: [6.123] }], doubles: [{ id: "2.1" }],
    relays: [{ id: "3.1" }], notifications: [{ id: "n1" }], auditLogs: [{ id: "a1" }],
    finalQualificationSnapshots: [{ id: "q1" }]
};
const original = structuredClone(state);
const outgoing = Repository.legacyStateForSave(state);
assert.deepEqual(outgoing.stackers, []);
assert.deepEqual(outgoing.results, []);
assert.deepEqual(outgoing.doubles, state.doubles);
assert.deepEqual(outgoing.relays, state.relays);
assert.deepEqual(outgoing.finalQualificationSnapshots, state.finalQualificationSnapshots);
outgoing.settings.name = "changed clone";
assert.deepEqual(state, original, "Projection must not mutate runtime state");
const incoming = Repository.withoutLegacyStackers(state);
assert.deepEqual(incoming.stackers, []);
assert.deepEqual(incoming.results, state.results, "Existing legacy read semantics remain unchanged");
incoming.results[0].attempts[0] = 8;
assert.deepEqual(state, original);

const remote = {
    settings: { name: "remote" }, doubles: [{ id: "2.1", name: "remote" }, { id: "2.2" }, { id: "2.3" }],
    relays: [{ id: "3.1" }, { id: "3.2" }], notifications: [{ id: "n1" }, { id: "n2" }],
    auditLogs: [{ id: "a1" }], remoteOnly: true
};
const local = {
    settings: { name: "local" }, doubles: [{ id: "2.1", name: "local" }, { id: "2.4" }],
    relays: [{ id: "3.3" }], notifications: [{ id: "n1", read: true }],
    auditLogs: [{ id: "a2" }], stackers: state.stackers, results: state.results
};
const beforeRemote = structuredClone(remote), beforeLocal = structuredClone(local);
const merged = Repository.mergeConcurrentState(remote, local,
    { doubles: new Set(["2.2"]), relays: new Set(["3.1"]) });
assert.deepEqual(merged.doubles, [{ id: "2.1", name: "local" }, { id: "2.3" }, { id: "2.4" }]);
assert.deepEqual(merged.relays.map(row => row.id), ["3.2", "3.3"]);
assert.deepEqual(merged.notifications, [{ id: "n1", read: true }, { id: "n2" }]);
assert.deepEqual(merged.auditLogs.map(row => row.id), ["a1", "a2"]);
assert.equal(merged.settings.name, "local");
assert.equal(merged.remoteOnly, true);
assert.deepEqual(merged.stackers, local.stackers);
assert.deepEqual(merged.results, local.results);
assert.deepEqual(remote, beforeRemote);
assert.deepEqual(local, beforeLocal);
assert.deepEqual(Repository.mergeConcurrentState({ doubles: [{ id: "2.1" }] }, { doubles: [] }).doubles, [{ id: "2.1" }]);
assert.deepEqual(Repository.mergeConcurrentState({ doubles: [{ id: "2.1" }] },
    { doubles: [{ id: "2.1", name: "re-added" }] }, { doubles: new Set(["2.1"]) }).doubles,
    [{ id: "2.1", name: "re-added" }], "Existing local re-add precedence must remain");
assert.deepEqual(Repository.mergeConcurrentState({ relays: [{ id: "3.1" }] }, { relays: null }).relays, null,
    "Non-array legacy merge behavior remains unchanged");
console.log("ENG-03B state projection and merge contracts passed.");
