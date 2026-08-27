const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const context = { window: {}, console };
context.window.window = context.window;
vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "js", "results", "BestResultEngine.js"), "utf8"), context, { filename: "BestResultEngine.js" });
vm.runInNewContext(fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "js", "reports", "FinalsReportEngine.js"), "utf8"), context, { filename: "FinalsReportEngine.js" });
const reports = context.window.StackMeetFinalsReports;
const state = {
  stackers: [
    { id: "1.1", name: "Champion", division: "18U", org: "School A", gender: "M", special: "No" },
    { id: "1.2", name: "Qualifier", division: "18U", org: "School B", gender: "M", special: "No" },
    { id: "1.3", name: "Scratch", division: "18U", org: "School C", gender: "M", special: "No" }
  ], doubles: [], relays: [],
  results: [
    { stage: "Prelims", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [3.2, 3.4, 3.3] },
    { stage: "Prelims", type: "Individual", participant: "1.2", event: "3-3-3", attempts: [3.5, 3.6, 3.7] },
    { stage: "Prelims", type: "Individual", participant: "1.3", event: "3-3-3", attempts: [999, 999, 999] },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [2.1, 2.2, 2.3] },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-6-3", attempts: [3.1, 3.2, 3.3] },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [6.1, 6.2, 6.3] },
    { stage: "Finals", type: "Individual", participant: "1.2", event: "3-3-3", attempts: [2.0, 2.2, 2.3] },
    { stage: "Finals", type: "Individual", participant: "1.2", event: "3-6-3", attempts: [3.0, 3.2, 3.3] },
    { stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [5.0, 5.2, 5.3] }
  ]
};
const prelim = reports.stagePlacementRows(state, "Prelims", { participantType: "Individual", division: "all", event: "all", category: "mixed" });
assert.strictEqual(prelim.length, 3);
assert.strictEqual(prelim.find(row => row.participant === "1.1").rank, 1);
assert.strictEqual(prelim.find(row => row.participant === "1.2").rank, 2);
assert.strictEqual(prelim.find(row => row.participant === "1.3").resultStatus, "scratch");
const allAround = reports.rankFinalRows(reports.stageAllAroundRows(state, "Finals", { participantType: "all", division: "all", event: "all", category: "mixed" }).filter(row => row.resultStatus === "valid"));
assert.strictEqual(allAround[0].participant, "1.2");
assert.strictEqual(allAround[0].bestValidTime, 10);
assert.strictEqual(allAround[1].participant, "1.1");
assert.ok(Math.abs(allAround[1].bestValidTime - 11.3) < 0.000001);
console.log("Competition report stage tests passed.");
