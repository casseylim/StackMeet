"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const scoring = require(path.join(root, "js/results/ScoringEntry.js"));
const app = fs.readFileSync(path.join(root, "app.js"), "utf8").replace(/\r\n/g, "\n");
for (const [raw, kind, value] of [
  ["", "blank", null], ["  ", "blank", null], [null, "blank", null],
  ["999", "scratch", 999], [" 999 ", "scratch", 999],
  ["6523", "time", 6.523], ["6.523", "time", 6.523], ["7", "time", 0.007],
  ["0999", "time", 0.999], ["1:02.5", "time", 62.5], ["1:02.005", "time", 62.005],
  ["10:00", "time", 600], ["600000", "time", 600],
  ["601000", "invalid", null], ["10:00.001", "invalid", null],
  ["0", "invalid", null], ["-3", "invalid", null], ["6.1234", "invalid", null],
  ["1:60", "invalid", null], ["1e3", "invalid", null], ["NaN", "invalid", null]
]) assert.deepEqual(scoring.parseCompetitionTime(raw), { kind, value }, String(raw));
for (const [raw, expected] of [["12", "1.2"], ["115", "1.15"], ["2.03", "2.3"], ["3105", "3.105"], [" 3.04 ", "3.4"], ["44", ""], ["", ""]]) {
  assert.equal(scoring.normalizePrelimEntryId(raw), expected);
}
assert.equal(scoring.normalizePrelimRounds(0), "0");
assert.equal(scoring.normalizePrelimRounds(3), "1");
const rows = [
  { id: "r1", type: "Individual", participant: "1.1", stage: "Prelims", attempts: [6.123], revision: 5 },
  { id: "r2", type: "Individual", participant: "2.1", stage: "Finals", attempts: [8, 999], revision: 8 },
  { id: "r3", type: "Individual", participant: "3.1", stage: "Finals", attempts: [9], revision: 10 },
  { id: "r4", type: "Relay", participant: "3.2", stage: "Finals", attempts: [9], revision: 11 }
];
const original = structuredClone(rows);
assert.deepEqual(scoring.normalizeResults(rows), [
  rows[0], { ...rows[1], type: "Doubles" }, { ...rows[2], type: "Timed Relay" }, { ...rows[3], type: "Timed Relay" }
]);
assert.deepEqual(rows, original, "Normalization must not modify input rows, stage or revisions.");
assert.deepEqual(scoring.finalResultInputValues(null), ["", "", ""]);
assert.deepEqual(scoring.finalResultInputValues({ attempts: [6.123, 999, null] }), ["6.123", "999", ""]);
assert.deepEqual(scoring.finalResultInputValues({ attempts: [0, 1000, "7"] }), ["0.000", "999", ""]);
assert.deepEqual(scoring.finalResultAttempts({ attempts: [0, 6.123, 999, null, "7", Infinity] }), [0, 6.123, 999]);
const classify = require(path.join(root, "js/results/BestResultEngine.js")).calculateBestResult;
assert.equal(scoring.prelimResultInputValue(null, classify), "");
assert.equal(scoring.prelimResultInputValue({ attempts: [6.123, 7] }, classify), "6.123");
assert.equal(scoring.prelimResultInputValue({ attempts: [999], penalty: 999 }, classify), "999");
assert.equal(scoring.prelimResultInputValue({ attempts: [] }, classify), "999");
const context = { window: {} };
vm.runInNewContext(fs.readFileSync(path.join(root, "js/results/ScoringEntry.js"), "utf8"), context);
for (const name of Object.keys(scoring)) {
  assert.equal(typeof context.window.StackMeetScoringEntry[name], "function");
  const start = app.indexOf("function " + name + "(");
  const end = app.indexOf("\n}\n", start);
  assert.ok(start >= 0 && end > start, "Missing wrapper " + name);
  assert.ok(app.slice(start, end).includes("return window.StackMeetScoringEntry." + name + "("), "Unwired wrapper " + name);
}
console.log("ENG-03E scoring entry contracts passed.");
