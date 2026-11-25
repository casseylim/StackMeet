"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.join(__dirname, "../backend/StackMeet.Api/wwwroot");
const presentation = require(path.join(root, "js/reports/ReportPresentation.js"));
const app = fs.readFileSync(path.join(root, "app.js"), "utf8").replace(/\r\n/g, "\n");
const start = app.indexOf("function esc("), end = app.indexOf("\n}\n", start);
assert.ok(start >= 0 && end > start);
const context = { window: {} };
vm.runInNewContext(app.slice(start, end + 2) + "\nglobalThis.escapeHtml = esc;", context);
const esc = context.escapeHtml;
const ports = { esc, t: value => value, tf: (key, values) => key.replace(/\{(\w+)\}/g, (_, name) => values[name]), brandText: () => "Synthetic & Co", finalParticipantSubline: () => "ID: <1.1>" };
assert.equal(presentation.csvLine(["A,B", 'A"B', null, 0, "line\nbreak"]), '"A,B","A""B","","0","line\nbreak"');
assert.equal(presentation.slugify(" Finals / U12! "), "finals-u12");
assert.equal(presentation.slugify("决赛"), "report");
assert.ok(presentation.compareReportCells(" 2 ", "10") < 0);
assert.ok(presentation.compareReportCells("A2", "A10") < 0);
assert.equal(presentation.compareReportCells("alpha", "ALPHA"), 0);
const report = { title: "Finals <test>", headers: ["Name", "Time"], rows: [["A&B", "8.123"]], groups: [] };
const original = structuredClone(report);
assert.deepEqual(presentation.reportExportRows(report), report.rows);
const grouped = { ...report, groups: [{ name: "U12 <group>", rows: report.rows }] };
assert.deepEqual(presentation.reportExportRows(grouped), [["U12 <group>"], ["A&B", "8.123"]]);
assert.equal(presentation.reportRowHtml(["<script>", null, '"'], esc), "<tr><td>&lt;script&gt;</td><td></td><td>&quot;</td></tr>");
const excel = presentation.excelReportHtml(grouped, { esc, reportRowHtml: row => presentation.reportRowHtml(row, esc), header: "NADI & Track", competitionName: "Cup <1>" });
for (const expected of ["NADI &amp; Track", "Cup &lt;1&gt;", "Finals &lt;test&gt;", "U12 &lt;group&gt; (1)", "A&amp;B", "8.123", 'colspan="2"']) assert.ok(excel.includes(expected), expected);
assert.deepEqual(report, original);
for (const type of ["Individual", "Doubles", "Timed Relay"]) {
  const prelim = presentation.prelimTimeSheetHtml({ id: "1.1", type, name: "<Name>", detail: "U12", location: "MY & SG", events: ["Cycle"] }, ports);
  for (const expected of ["individual-time-sheet", "Synthetic &amp; Co", "&lt;Name&gt;", "MY &amp; SG", "Attempt 1", "Attempt 2", "Attempt 3", "Use 999 for a scratch.", "Cycle"]) assert.ok(prelim.includes(expected), expected);
  assert.equal((prelim.match(/class="time-write-line"/g) || []).length, 3);
}
for (const [typeKey, type] of [["1", "Individual"], ["2", "Doubles"], ["3", "Timed Relay"]]) {
  const finals = presentation.finalTimeSheetHtml({ typeKey, type, entryType: type, division: "U12", event: "Cycle", id: "1.1", finalists: [{ qualifierRank: 1, participant: "1.1", name: "A&B", prelimTime: 8.123 }] }, ports);
  for (const expected of ["final-print-table", "A&amp;B", "ID: &lt;1.1&gt;", "8.123", "Prelims", "Attempt 3", "SCRATCH write 999."]) assert.ok(finals.includes(expected), expected);
  assert.equal(finals.includes("final-time-sheet-individual"), typeKey === "1");
}
vm.runInNewContext(fs.readFileSync(path.join(root, "js/reports/ReportPresentation.js"), "utf8"), context);
for (const name of Object.keys(presentation)) {
  assert.equal(typeof context.window.StackMeetReportPresentation[name], "function");
  const i = app.indexOf("function " + name + "("), j = app.indexOf("\n}\n", i);
  assert.ok(i >= 0 && j > i && app.slice(i, j).includes("return window.StackMeetReportPresentation." + name + "("), name);
}
console.log("ENG-03F report presentation contracts passed.");
