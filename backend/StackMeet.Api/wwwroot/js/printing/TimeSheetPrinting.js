"use strict";
function buildPaperwork(type) {
    const out = document.getElementById("paperOutput");
    const title = type.replaceAll("-", " ").replace(/\b\w/g, c => c.toUpperCase());
    if (type.startsWith("finals")) {
        buildFinalPaperwork(type);
        return;
    }
    if (type === "individual-prelim") {
        const stackers = selectedStackersForPrintRange();
        const range = stackers.length ? `${stackers[0].id} to ${stackers[stackers.length - 1].id}` : "No stackers";
        out.innerHTML = `<div class="panel-head no-print"><h2>Individual Time Sheets (${esc(range)})</h2><button class="ghost" data-action="print-paper-preview" type="button">Print Range</button></div>
      <div class="time-sheet-list">${stackers.map(individualTimeSheetHtml).join("") || `<p class="muted">No stackers found in this range.</p>`}</div>`;
        return;
    }
    if (type === "doubles-prelim") {
        const teams = printableDoublesTeams();
        out.innerHTML = `<div class="panel-head no-print"><h2>Doubles Time Sheets</h2><button class="ghost" data-action="print-paper-preview" type="button"${teams.length ? "" : " disabled"}>Print</button></div>
      <div class="time-sheet-list">${teams.map(doublesTimeSheetHtml).join("") || `<p class="muted">No completed doubles teams are available for preliminary time sheets.</p>`}</div>`;
        return;
    }
    if (type === "relay-prelim") {
        const teams = completedRelays();
        out.innerHTML = `<div class="panel-head no-print"><h2>Relay Time Sheets</h2><button class="ghost" data-action="print-paper-preview" type="button"${teams.length ? "" : " disabled"}>Print</button></div>
      <div class="time-sheet-list">${teams.map(relayTimeSheetHtml).join("") || `<p class="muted">No ready relay teams are available for preliminary time sheets.</p>`}</div>`;
        return;
    }
    const sample = state.stackers.slice(0, 6);
    out.innerHTML = `<div class="panel-head no-print"><h2>${esc(title)}</h2><button class="ghost" data-action="print-paper-preview" type="button">Print</button></div>
    ${sample.map(s => `<article class="sheet-preview"><strong>${esc(s.id)} ${esc(s.name)}</strong><p>${esc(s.division)} // ${esc(s.org)} // ${esc(s.country)}</p><p>3-3-3: _____  3-6-3: _____  Cycle: _____</p></article>`).join("")}`;
}

function buildFinalPaperwork(type) {
    const out = document.getElementById("paperOutput");
    const typeMap = {
        "finals-individual": { typeKey: "1", title: "Individual Final Time Sheets" },
        "finals-doubles": { typeKey: "2", title: "Doubles Final Time Sheets" },
        "finals-relay": { typeKey: "3", title: "Relay Final Time Sheets" }
    };
    const config = typeMap[type] || { typeKey: "", title: "All Final Time Sheets" };
    const sheets = finalSheets().filter(sheet => !config.typeKey || sheet.typeKey === config.typeKey);
    const summary = sheets.length
        ? `${sheets.length} ${t(sheets.length === 1 ? "sheet ready for judges" : "sheets ready for judges")}`
        : t("No final sheets yet. Enter prelim results first.");
    out.innerHTML = `<div class="panel-head no-print"><div><h2>${esc(t(config.title))}</h2><p class="muted">${esc(summary)}</p></div><button class="ghost" data-action="print-paper-preview" type="button"${sheets.length ? "" : " disabled"}>${esc(t("Print Finals"))}</button></div>
    <div class="final-sheet-list">${sheets.map(finalTimeSheetHtml).join("") || `<p class="muted">${esc(t("No finalists matched this selection."))}</p>`}</div>`;
}

function selectedStackersForPrintRange() {
    const stackers = [...state.stackers].sort((a, b) => stackerIdNumber(a.id) - stackerIdNumber(b.id));
    if (!stackers.length) return [];
    const fromValue = stackerIdNumber(val("printRangeFrom") || stackers[0].id);
    const toValue = stackerIdNumber(val("printRangeTo") || stackers[stackers.length - 1].id);
    const low = Math.min(fromValue, toValue);
    const high = Math.max(fromValue, toValue);
    return stackers.filter(stacker => {
        const id = stackerIdNumber(stacker.id);
        return id >= low && id <= high;
    });
}

function individualTimeSheetHtml(stacker) {
    return prelimTimeSheetHtml({
        id: stacker.id,
        type: "Individual",
        name: stacker.name,
        detail: `Division: ${stacker.division || "Open"}`,
        location: `${stacker.country || "--"} · Organization: ${stacker.org || "Independent"}`,
        events: ["3-3-3", "3-6-3", "Cycle"]
    });
}

function doublesTimeSheetHtml(team) {
    return prelimTimeSheetHtml({
        id: team.id,
        type: "Doubles",
        name: participantName("Doubles", team.id),
        detail: `Division: ${doubleDivision(team)}`,
        location: teamCountry(team),
        events: timeSheetEvents("Doubles", ["Cycle"])
    });
}

function relayTimeSheetHtml(team) {
    return prelimTimeSheetHtml({
        id: team.id,
        type: "Relay",
        name: participantName("Timed Relay", team.id),
        detail: `Stackers: ${relayMemberIds(team).map(stackerName).join(", ") || "--"} · Division: ${relayTimedDivision(team)}`,
        location: relayLocation(team),
        events: timeSheetEvents("Timed Relay", ["3-6-3"])
    });
}

function timeSheetEvents(group, fallback) {
    // Uses configured event setup; falls back so Individual/Doubles/Relay sheets still print.
    const events = state.events?.[group];
    return Array.isArray(events) && events.length ? events : fallback;
}

function prelimTimeSheetHtml({ id, type, name, detail, location, events }) {
    // Shared printable time sheet for preliminary Individual, Doubles, and Timed Relay.
    // The generated headers are Event / Attempt 1 / Attempt 2 / Attempt 3.
    const attempts = [1, 2, 3];
    return `<article class="individual-time-sheet">
    <div class="time-sheet-brand">${esc(brandText("reportHeader"))}</div>
    <header class="time-sheet-header">
      <div class="time-sheet-identity"><span>${esc(type)}</span><h2>${esc(name)}</h2></div>
      <div class="time-sheet-stacker-id"><span>ID</span><strong>${esc(id)}</strong></div>
    </header>
    <div class="time-sheet-subline"><strong>${esc(detail)}</strong><span>Location: ${esc(location)}</span></div>
    <table class="attempt-table">
      <colgroup><col class="event-col" /><col class="attempt-col" /><col class="attempt-col" /><col class="attempt-col" /></colgroup>
      <thead><tr><th>Event</th>${attempts.map(attempt => `<th>Attempt ${attempt}</th>`).join("")}</tr></thead>
      <tbody>${events.map(event => `<tr><th>${esc(event)}</th>${attempts.map(() => `<td><span class="time-write-line"></span><span class="best-mark"><i></i> Best</span></td>`).join("")}</tr>`).join("")}</tbody>
    </table>
    <div class="time-sheet-notes">
      <p>Record Attempt 1, Attempt 2 and Attempt 3.</p>
      <p>Tick the fastest valid attempt.</p>
      <p>Use 999 for a scratch.</p>
      <p>Leave blank if the competitor or team did not compete.</p>
    </div>
    <footer class="time-sheet-signoff"><span>Judge: ______________________________</span><span>Table: __________</span></footer>
  </article>`;
}

function printSingleStackerSheet(id) {
    const stacker = state.stackers.find(item => item.id === id);
    if (!stacker) return;
    document.getElementById("singleTimeSheetPrintJob")?.remove();
    const printJob = document.createElement("section");
    printJob.id = "singleTimeSheetPrintJob";
    printJob.innerHTML = individualTimeSheetHtml(stacker);
    document.body.appendChild(printJob);
    printTimeSheetTarget("single-time-sheet", printJob);
}

function printPaperPreview() {
    if (!document.querySelector("#paperOutput .individual-time-sheet, #paperOutput .final-time-sheet, #paperOutput .sheet-preview, #paperOutput .bracket")) return;
    printTimeSheetTarget("print-center");
}

function printTimeSheetTarget(target, removableElement = null) {
    // Applies a temporary print target and page orientation, then cleans it after printing.
    const pageStyle = document.createElement("style");
    const isFinalTimeSheetPrint = target === "single-time-sheet"
        ? removableElement?.querySelector(".final-time-sheet")
        : document.querySelector("#paperOutput .final-time-sheet");
    pageStyle.textContent = isFinalTimeSheetPrint
        ? "@media print { @page { size: A4 landscape; margin: 8mm; } }"
        : "@media print { @page { size: A4 portrait; margin: 10mm; } }";
    document.head.appendChild(pageStyle);
    const originalTitle = document.title;
    document.body.dataset.printTarget = target;
    if (isFinalTimeSheetPrint) document.body.dataset.printMode = "final-time-sheet";
    document.title = "";
    const cleanup = () => {
        document.title = originalTitle;
        delete document.body.dataset.printTarget;
        delete document.body.dataset.printMode;
        pageStyle.remove();
        removableElement?.remove();
    };
    window.addEventListener("afterprint", cleanup, { once: true });
    window.print();
    setTimeout(cleanup, 1000);
}

function buildBracket() {
    const out = document.getElementById("paperOutput");
    const size = Number((val("bracketType").match(/\d+/) || [8])[0]);
    out.innerHTML = `<div class="panel-head"><h2>${esc(val("bracketType"))} ${esc(val("resultEvent") || val("bracketEvent"))} Bracket</h2><button class="ghost" onclick="window.print()" type="button">Print</button></div>
    ${Array.from({ length: size }, (_, i) => `<div class="bracket">Seed ${i + 1}: ____________________________</div>`).join("")}`;
}


function printCurrentFinalSheet() {
    const sheet = finalSheets().find(item => item.id === activeFinalSheetId);
    if (!sheet) {
        showFinalMessage("Find a final sheet before printing.", true);
        return;
    }
    buildFinalSheetPrint(sheet);
}

function buildFinalSheetPrint(sheet) {
    document.getElementById("singleTimeSheetPrintJob")?.remove();
    const printJob = document.createElement("section");
    printJob.id = "singleTimeSheetPrintJob";
    printJob.innerHTML = finalTimeSheetHtml(sheet);
    document.body.appendChild(printJob);
    printTimeSheetTarget("single-time-sheet", printJob);
}

function finalTimeSheetHtml(sheet) {
    return `<article class="final-time-sheet">
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
