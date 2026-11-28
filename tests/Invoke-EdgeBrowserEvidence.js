"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const { execFileSync } = require("node:child_process");
const { fixture } = require("./edge-browser-fixture.js");
const { launch, pause } = require("./edge-cdp.js");
const output = path.resolve(process.argv[2] || "edge-evidence");
const sha = process.argv[3];
const report = { schemaVersion: 1, sourceSha: sha, fixture: "ENG06D loopback synthetic competition", cases: [], findings: [], status: "failed" };
const { server, info, name } = fixture();
const select = (id, value) => "(() => { const node = document.getElementById(" + JSON.stringify(id) + "); node.value = " + JSON.stringify(value) + "; node.dispatchEvent(new Event('change', { bubbles:true })); })()";
async function measure(cdp, label, width, locale, control) {
  const metric = await cdp.eval("({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth, lang: document.documentElement.lang, title: document.title })");
  assert.equal(metric.width, width);
  assert.equal(metric.lang, locale);
  const doc = await cdp.send("DOM.getDocument");
  const node = await cdp.send("DOM.querySelector", { nodeId: doc.root.nodeId, selector: "#" + control });
  assert.ok(node.nodeId, "Missing control " + control);
  const ax = await cdp.send("Accessibility.getPartialAXTree", { nodeId: node.nodeId, fetchRelatives: false });
  const actual = ax.nodes.find(n => !n.ignored);
  assert.ok(actual?.name?.value?.trim(), "Missing accessible name: " + control);
  if (metric.scrollWidth > width + 2) report.findings.push({ case: label, type: "horizontal-overflow", ...metric });
  await cdp.shot(path.join(output, label + ".png"));
  report.cases.push({ name: label, ...metric, accessible: { role: actual.role?.value, name: actual.name.value }, screenshot: label + ".png" });
}
async function printSheet(cdp, type, cssClass, landscape) {
  const selector = '[data-action="paperwork"][data-type="' + type + '"]';
  await cdp.eval("document.querySelector(" + JSON.stringify(selector) + ").click()");
  await cdp.wait("!!document.querySelector('#paperOutput ." + cssClass + "')", type + " populated preview");
  const metrics = await cdp.eval("({ count: document.querySelectorAll('#paperOutput ." + cssClass + "').length, text: document.getElementById('paperOutput').innerText })");
  assert.match(metrics.text, /Alpha Fixture/);
  // Observe the real print action's style and target without a platform modal.
  const config = await cdp.eval("(() => { let capture; const real = window.print; window.print = () => { capture = { target: document.body.dataset.printTarget, mode: document.body.dataset.printMode || '', style: [...document.head.querySelectorAll('style')].at(-1).textContent }; }; try { document.querySelector('#paperOutput [data-action=\"print-paper-preview\"]').click(); return capture; } finally { window.print = real; window.dispatchEvent(new Event('afterprint')); } })()");
  assert.equal(config.target, "print-center");
  assert.match(config.style, landscape ? /A4 landscape/ : /A4 portrait/);
  assert.equal(await cdp.eval("!document.body.dataset.printTarget && !!document.title"), true, "Print cleanup failed");
  await pause(1100);
  // Replay captured style/target for actual PDF layout via browser DevTools.
  await cdp.eval("(() => { const s = document.createElement('style'); s.id='eng06d-print'; s.textContent=" + JSON.stringify(config.style) + "; document.head.append(s); document.body.dataset.printTarget='print-center'; if(" + JSON.stringify(config.mode) + ") document.body.dataset.printMode=" + JSON.stringify(config.mode) + "; })()");
  try {
    const pdf = await cdp.send("Page.printToPDF", { printBackground: true, preferCSSPageSize: true, landscape });
    const bytes = Buffer.from(pdf.data, "base64");
    assert.ok(bytes.length > 1000 && bytes.subarray(0, 5).toString() === "%PDF-", "Invalid rendered PDF");
    const file = type + "-" + await cdp.eval("document.documentElement.lang") + ".pdf";
    fs.writeFileSync(path.join(output, file), bytes);
    report.cases.push({ name: "print-" + file, sheets: metrics.count, bytes: bytes.length, orientation: landscape ? "landscape" : "portrait", artifact: file });
  } finally {
    await cdp.eval("document.getElementById('eng06d-print').remove(); delete document.body.dataset.printTarget; delete document.body.dataset.printMode;");
  }
}
(async () => {
  let browser, cdp;
  fs.mkdirSync(output, { recursive: true });
  try {
    assert.match(sha || "", /^[a-f0-9]{40}$/);
    assert.equal(execFileSync("git", ["rev-parse", "HEAD"], { cwd: __dirname, encoding: "utf8" }).trim(), sha);
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    const base = "http://127.0.0.1:" + server.address().port;
    browser = await launch(report); cdp = browser.cdp;
    for (const width of [320, 390, 1280]) {
      await cdp.size(width);
      for (const locale of ["en", "ms", "zh-Hans"]) {
        await cdp.navigate(base + "/ENG06D/Results/AllAround?lang=" + locale);
        await cdp.wait("document.getElementById('competitionName')?.textContent === " + JSON.stringify(name) + " && !!document.querySelector('#allaround-overall tbody tr')", "public results " + locale);
        const standings = await cdp.eval("document.querySelector('#allaround-overall tbody').innerText");
        assert.match(standings, /Alpha Fixture/);
        assert.match(standings, /18[.,]000/);
        assert.doesNotMatch(standings, /Incomplete Fixture/);
        await measure(cdp, "public-" + width + "-" + locale, width, locale, "publicLanguage");
      }
    }
    await cdp.eval(select("publicLanguage", "ms"));
    assert.equal(await cdp.eval("document.documentElement.lang"), "ms");
    assert.match(await cdp.eval("location.search"), /lang=ms/);
    info.withdrawn = true;
    await cdp.navigate(base + "/ENG06D/Results/AllAround?lang=en");
    await cdp.wait("document.getElementById('errorState') && !document.getElementById('errorState').hidden", "withdrawn public view");
    assert.equal(await cdp.eval("document.getElementById('allAround').hidden && !document.getElementById('allAroundGroups').children.length"), true);
    info.withdrawn = false; info.empty = true;
    await cdp.eval("document.getElementById('retryButton').click()");
    await cdp.wait("document.getElementById('errorState').hidden && document.getElementById('competitionName').textContent === " + JSON.stringify(name), "empty recovery");
    assert.equal(await cdp.eval("!document.querySelector('#allaround-overall tbody tr')"), true);
    info.empty = false;
    report.cases.push({ name: "public-language-withdrawal-empty-recovery", status: "passed" });
    await cdp.navigate(base + "/");
    await cdp.wait("!!document.getElementById('loginEmail') && document.body.classList.contains('auth-pending')", "login shell");
    // Submit ordinary login form to the local fixture, not a live auth service.
    await cdp.eval("document.getElementById('loginEmail').value='operator@example.test'; document.getElementById('loginPassword').value='fixture-only'; document.getElementById('loginForm').requestSubmit()");
    await cdp.wait("!document.getElementById('loginCompetitionStep').hidden && document.getElementById('loginCompetitionSelect').options.length > 0", "competition selection");
    await cdp.eval("document.getElementById('loginCompetitionSelect').value='7'; document.getElementById('loginForm').requestSubmit()");
    await cdp.wait("!document.body.classList.contains('auth-pending') && !!document.querySelector('#view .metric-grid, #view [data-action]')", "authenticated dashboard");
    await cdp.eval("location.hash='paperwork'");
    await cdp.wait("!!document.getElementById('printRangeFrom') && document.getElementById('printRangeFrom').options.length === 2", "SQL roster print center");
    for (const width of [320, 390, 1280]) {
      await cdp.size(width);
      for (const locale of ["en", "ms", "zh-Hans"]) {
        await cdp.eval(select("operatorLanguageAuthenticated", locale));
        await cdp.wait("document.documentElement.lang === " + JSON.stringify(locale) + " && !!document.getElementById('paperOutput')", "operator locale");
        await measure(cdp, "operator-" + width + "-" + locale, width, locale, "operatorLanguageAuthenticated");
        if (width === 1280) {
          await printSheet(cdp, "individual-prelim", "individual-time-sheet", false);
          await printSheet(cdp, "finals-individual", "final-time-sheet", true);
        }
      }
    }
    await cdp.eval("document.getElementById('operatorLanguageAuthenticated').focus()");
    assert.equal(await cdp.eval("document.activeElement.id"), "operatorLanguageAuthenticated");
    await cdp.send("Input.dispatchKeyEvent", { type: "keyDown", key: "Tab", code: "Tab", windowsVirtualKeyCode: 9 });
    await cdp.send("Input.dispatchKeyEvent", { type: "keyUp", key: "Tab", code: "Tab", windowsVirtualKeyCode: 9 });
    assert.equal(await cdp.eval("document.activeElement.id !== 'operatorLanguageAuthenticated'"), true);
    report.cases.push({ name: "keyboard-focus-tab", status: "passed" });
    assert.deepEqual(info.unexpected, [], "Unexpected API calls");
    assert.deepEqual(cdp.exceptions, [], "Uncaught browser exceptions");
    report.status = "passed";
  } catch (error) {
    report.error = error.stack || String(error); console.error(report.error); process.exitCode = 1;
    if (cdp) { try { await cdp.shot(path.join(output, "failure.png")); } catch {} }
  } finally {
    report.requests = info.calls; report.unexpectedRequests = info.unexpected;
    if (cdp) report.exceptions = cdp.exceptions;
    if (browser) browser.close();
    server.close();
    fs.writeFileSync(path.join(output, "browser-evidence.json"), JSON.stringify(report, null, 2) + "\n");
    console.log("ENG06D Edge evidence " + report.status + ": " + report.cases.length + " cases, " + report.findings.length + " findings");
  }
})();
