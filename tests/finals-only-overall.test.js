"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
// Git's Windows checkout may use CRLF; normalize before locating function boundaries.
const app = fs.readFileSync(path.join(__dirname, "../backend/StackMeet.Api/wwwroot/app.js"), "utf8").replace(/\r\n/g, "\n");
const extract = name => {
  const start = app.indexOf("function " + name + "(");
  const end = app.indexOf("\n}\n", start);
  assert.ok(start >= 0 && end > start, name);
  return app.slice(start, end + 2);
};
const selected = { finalsReportKind: "overall", finalsReportStage: "Prelims", competitionStage: "all" };
const context = {
  val: id => selected[id], setValue: (id, value) => { selected[id] = value; },
  finalsReportFilters: () => ({}), stageReportDefinition: (stage, kind) => ({ stage, kind })
};
vm.runInNewContext(extract("finalsReportDefinition") + "\n" + extract("competitionStageMatches") +
  "\nglobalThis.hooks = { finalsReportDefinition, competitionStageMatches };", context);
assert.equal(context.hooks.finalsReportDefinition().stage, "Finals");
assert.equal(selected.finalsReportStage, "Finals");
selected.finalsReportKind = "division"; selected.finalsReportStage = "Prelims";
assert.equal(context.hooks.finalsReportDefinition().stage, "Prelims");
assert.equal(context.hooks.competitionStageMatches("Finals"), true);
for (const stage of ["Prelims", "SOC", "All"]) assert.equal(context.hooks.competitionStageMatches(stage), false);
selected.competitionStage = "prelims";
assert.equal(context.hooks.competitionStageMatches("Prelims"), true);
assert.equal(context.hooks.competitionStageMatches("Finals"), false);
console.log("Owner finals-only overall report contracts passed.");
