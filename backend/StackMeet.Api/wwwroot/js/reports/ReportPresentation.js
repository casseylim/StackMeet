(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetReportPresentation = api;
})(typeof window !== "undefined" ? window : null, function () {
  "use strict";

  // Presentation only: no state writes, qualification, result selection or DOM printing.
  function csvLine(row) {
    return row.map(value => `"${String(value ?? "").replaceAll('"', '""')}"`).join(",");
  }

  function slugify(value) {
    return String(value || "report").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "report";
  }

  function reportExportRows(report) {
    if (!report.groups.length) return report.rows;
    return report.groups.flatMap(group => [[group.name], ...group.rows]);
  }

  function compareReportCells(a, b) {
    const aText = String(a ?? "").trim();
    const bText = String(b ?? "").trim();
    const numericCell = /^[-+]?(?:\d+(?:\.\d+)?|\.\d+)$/;
    if (numericCell.test(aText) && numericCell.test(bText)) return Number(aText) - Number(bText);
    return aText.localeCompare(bText, undefined, { numeric: true, sensitivity: "base" });
  }

  function reportRowHtml(row, esc) {
    return `<tr>${row.map(cell => `<td>${esc(cell)}</td>`).join("")}</tr>`;
  }

  function excelReportHtml(report, { esc, reportRowHtml, header, competitionName }) {
    const bodyRows = report.groups.length
      ? report.groups.map(group => `<tr><td colspan="${report.headers.length}"><strong>${esc(group.name)} (${group.rows.length})</strong></td></tr>${group.rows.map(row => reportRowHtml(row)).join("")}`).join("")
      : report.rows.map(row => reportRowHtml(row)).join("");
    return `<!doctype html><html><head><meta charset="utf-8"><style>
      body{font-family:Arial,sans-serif} table{border-collapse:collapse;width:100%} th,td{border:1px solid #999;padding:6px;text-align:left} th{background:#e9eef2}
    </style></head><body>
      <h1>${esc(header)}</h1><h2>${esc(competitionName)}</h2><h3>${esc(report.title)}</h3>
      <table><thead><tr>${report.headers.map(header => `<th>${esc(header)}</th>`).join("")}</tr></thead><tbody>${bodyRows}</tbody></table>
    </body></html>`;
  }

  function prelimTimeSheetHtml({ id, type, name, detail, location, events }, { esc, t, tf, brandText }) {
    // Shared printable time sheet for preliminary Individual, Doubles, and Timed Relay.
    // The generated headers are Event / Attempt 1 / Attempt 2 / Attempt 3.
    const attempts = [1, 2, 3];
    return `<article class="individual-time-sheet">
      <div class="time-sheet-brand">${esc(brandText("reportHeader"))}</div>
      <header class="time-sheet-header">
        <div class="time-sheet-identity"><span>${esc(t(type))}</span><h2>${esc(name)}</h2></div>
        <div class="time-sheet-stacker-id"><span>${esc(t("ID"))}</span><strong>${esc(id)}</strong></div>
      </header>
      <div class="time-sheet-subline"><strong>${esc(detail)}</strong><span>${esc(tf("Location: {location}", { location }))}</span></div>
      <table class="attempt-table">
        <colgroup><col class="event-col" /><col class="attempt-col" /><col class="attempt-col" /><col class="attempt-col" /></colgroup>
        <thead><tr><th>${esc(t("Event"))}</th>${attempts.map(attempt => `<th>${esc(tf("Attempt {attempt}", { attempt }))}</th>`).join("")}</tr></thead>
        <tbody>${events.map(event => `<tr><th>${esc(event)}</th>${attempts.map(() => `<td><span class="time-write-line"></span><span class="best-mark"><i></i> ${esc(t("Best"))}</span></td>`).join("")}</tr>`).join("")}</tbody>
      </table>
      <div class="time-sheet-notes">
        <p>${esc(t("Record Attempt 1, Attempt 2 and Attempt 3."))}</p>
        <p>${esc(t("Tick the fastest valid attempt."))}</p>
        <p>${esc(t("Use 999 for a scratch."))}</p>
        <p>${esc(t("Leave blank if the competitor or team did not compete."))}</p>
      </div>
      <footer class="time-sheet-signoff"><span>${esc(t("Judge:"))} ______________________________</span><span>${esc(t("Table:"))} __________</span></footer>
    </article>`;
  }

  function finalTimeSheetHtml(sheet, { esc, t, brandText, finalParticipantSubline }) {
    const printClass = sheet.typeKey === "1" ? " final-time-sheet-individual" : "";
    return `<article class="final-time-sheet${printClass}">
      <div class="time-sheet-brand">${esc(brandText("reportHeader"))}</div>
      <header class="final-sheet-header">
        <div class="qr-box">QR</div>
        <div><h2>${esc(t("Finals:"))}</h2><p>${esc(t(sheet.entryType))} // ${esc(sheet.division)} // ${esc(sheet.event)}</p></div>
        <strong>ID: ${esc(sheet.id)}</strong>
      </header>
      <table class="final-print-table">
        <thead><tr><th></th><th>${esc(t("Stacker"))}</th><th>${esc(t("Prelims"))}</th><th>${esc(t("Attempt 1"))}</th><th>${esc(t("Attempt 2"))}</th><th>${esc(t("Attempt 3"))}</th><th>${esc(t("Best Time"))}</th><th>${esc(t("Place"))}</th></tr></thead>
        <tbody>${sheet.finalists.map(finalist => `<tr>
          <td class="final-rank">${finalist.qualifierRank}</td>
          <td><strong>${esc(finalist.name)}</strong><br><small>${esc(finalParticipantSubline(sheet.type, finalist.participant))}</small></td>
          <td>${esc(finalist.prelimTime.toFixed(3))}</td>
          <td></td><td></td><td></td><td></td><td></td>
        </tr>`).join("")}</tbody>
      </table>
      <ul class="final-sheet-tips">
        <li>${esc(t("Start at the top of the page, allow 2 warm-ups prior to Attempt 1 for each stacker."))}</li>
        <li>${esc(t("After warm-ups, the next 3 stacks must be used as Attempt 1, 2 and 3."))}</li>
        <li>${esc(t("Indicate time using all numbers as displayed on the timer. Example: 6.523."))}</li>
        <li>${esc(t("SCRATCH write 999."))}</li>
        <li>${esc(t("Leave blank = did not compete."))}</li>
      </ul>
    </article>`;
  }

  return Object.freeze({ csvLine, slugify, reportExportRows, compareReportCells, reportRowHtml, excelReportHtml, prelimTimeSheetHtml, finalTimeSheetHtml });
});
