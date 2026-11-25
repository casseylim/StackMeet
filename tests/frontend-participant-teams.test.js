"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const teams = require(path.join(root, "js/ParticipantTeams.js"));
const app = fs.readFileSync(path.join(root, "app.js"), "utf8");
const roster = { stackers: [{ id: "1.2" }, { id: "1.10" }], doubles: [{ id: "2.4" }], relays: [{ id: "3.7", name: "Fast Four" }] };
const original = structuredClone(roster);
assert.equal(teams.nextTeamCode("1", roster), "1.11");
assert.equal(teams.nextTeamCode("2", roster), "2.5");
assert.equal(teams.nextTeamCode("3", roster), "3.8");
assert.equal(teams.nextTeamCode("1", { stackers: [], doubles: [] }), "1.1");
assert.ok(teams.compareStackers({ id: "1.2" }, { id: "1.10" }, "id") < 0);
assert.equal(teams.stackerIdNumber("not-an-id"), Number.MAX_SAFE_INTEGER);
assert.deepEqual(teams.registeredDoubleMemberIds({ one: "1.1", two: "", childStackerId: "1.2", parentStackerId: "1.3" }), ["1.1", "1.2", "1.3"]);
assert.deepEqual(teams.relayMemberIds({ members: ["1.1", "", "1.2"], one: "legacy" }), ["1.1", "1.2"]);
assert.deepEqual(teams.relayMemberIds({ one: "1.1", four: "1.4" }), ["1.1", "1.4"]);
assert.deepEqual(teams.relayMemberIds(null), []);
for (let count = 0; count <= 6; count++) {
  const team = { members: Array.from({ length: count }, (_, i) => String(i + 1)) };
  assert.equal(teams.relayCanCompete(team), count >= 4);
  assert.equal(teams.relayTeamStatus(team), count === 0 ? "Draft" : count < 4 ? "Incomplete" : "Ready");
}
const d = { type: "normal", status: "complete", one: "1.1", two: "", parentName: "" };
assert.equal(teams.validateDoubleEntry({ ...d, one: "" }), "Please choose the first stacker.");
assert.equal(teams.validateDoubleEntry(d), "Normal doubles needs two registered stackers.");
assert.equal(teams.validateDoubleEntry({ ...d, status: "pending" }), "");
assert.equal(teams.validateDoubleEntry({ ...d, two: "1.1" }), "A stacker cannot partner with themselves.");
assert.equal(teams.validateDoubleEntry({ ...d, type: "child_parent" }), "Child/Parent needs a registered parent or external parent name.");
assert.equal(teams.validateDoubleEntry({ ...d, type: "child_parent", parentName: "Synthetic parent" }), "");
assert.equal(teams.validateDoubleEntry({ ...d, two: "1.2" }), "");
assert.equal(teams.validateRelayEntry([], "", roster.relays, ""), "Relay team name is required.");
assert.equal(teams.validateRelayEntry([], "fast four", roster.relays, ""), "Relay team name must be unique.");
assert.equal(teams.validateRelayEntry([], "fast four", roster.relays, "3.7"), "");
assert.equal(teams.validateRelayEntry(["1", "2", "3", "4", "5", "6", "7"], "New", roster.relays, ""), "Relay team can only keep up to 6 registered stackers.");
assert.equal(teams.validateRelayEntry(["1", "1"], "New", roster.relays, ""), "A stacker can only be selected once in this relay team.");
assert.equal(teams.validateRelayEntry([], "New", roster.relays, ""), "");
assert.deepEqual(roster, original, "Pure operations must not mutate roster or teams");

// Browser export and all nine application compatibility wrappers must remain connected.
const context = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(root, "js/ParticipantTeams.js"), "utf8"), context);
for (const name of Object.keys(teams)) {
  assert.equal(typeof context.window.StackMeetParticipantTeams[name], "function");
  const start = app.indexOf("function " + name + "(");
  assert.ok(start >= 0, "missing wrapper " + name);
  const end = app.indexOf("\n}\n", start);
  assert.ok(app.slice(start, end).includes("return window.StackMeetParticipantTeams." + name + "("), "unwired wrapper " + name);
}
console.log("ENG-03D participant and team contracts passed.");
