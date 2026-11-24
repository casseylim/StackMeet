"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = path.resolve(__dirname, "..", "backend/StackMeet.Api/wwwroot");
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, "frontend-architecture-matrix.json"), "utf8"));
const read = file => fs.readFileSync(path.join(root, file), "utf8");

// Record every ordered classic/deferred script entry, ignoring only version query tokens.
for (const [page, expected] of Object.entries(manifest.shells)) {
    const source = read(page);
    const actual = [...source.matchAll(/<script\b([^>]*)>/gi)].map(match => {
        const attrs = match[1];
        const src = /\bsrc="([^"]+)"/.exec(attrs);
        assert.ok(src, page + ": unexpected inline script");
        assert.doesNotMatch(attrs, /\btype\s*=\s*["']module["']/i,
            page + ": deliberate module conversion requires inventory review");
        const pathname = src[1].split("?")[0];
        assert.ok(!/^(?:https?:)?\/\//i.test(pathname), page + ": scripts must remain local");
        const resolved = pathname.startsWith("/") ? path.join(root, pathname.slice(1))
            : path.resolve(root, path.dirname(page), pathname);
        assert.ok(resolved.startsWith(root + path.sep) && fs.existsSync(resolved),
            page + ": missing local script " + pathname);
        return { src: pathname, mode: /\bdefer\b/.test(attrs) ? "defer" : /\basync\b/.test(attrs) ? "async" : "classic" };
    });
    assert.deepEqual(actual, expected, page + ": script dependency/order changed; review and update manifest");
}
const app = read("app.js");
const navigationSource = read("js/NavigationActions.js");
const navigation = require("../backend/StackMeet.Api/wwwroot/js/NavigationActions.js");
const start = app.indexOf('document.addEventListener("click", async (event) => {');
const end = app.indexOf('\ndocument.addEventListener("change", (event) => {', start);
assert.ok(start >= 0 && end > start, "Delegated action handler moved; update characterization harness");
const source = app.slice(start, end);
const actions = [...source.matchAll(/\baction === "([^"]+)"/g)].map(match => match[1]);
assert.deepEqual(actions, manifest.delegatedActions, "Delegated action inventory changed");
const extractedActions = [...navigationSource.matchAll(/\baction === "([^"]+)"/g)].map(match => match[1]);
assert.deepEqual(extractedActions, manifest.extractedActions, "Extracted action inventory changed");
const allActions = [...actions, ...extractedActions];
assert.equal(new Set(allActions).size, allActions.length);

async function scenario(name, dataset, overrides, expected, check) {
    const trace = [];
    let handler;
    const record = label => (...args) => { trace.push([label, ...args]); };
    const context = {
        document: { addEventListener(type, callback) { assert.equal(type, "click"); handler = callback; } },
        window: { StackMeetNavigationActions: navigation, print: record("print"), StackMeetPrintCenter: {
            buildPaperwork: record("paperwork"), printPaperPreview: record("preview")
        } },
        console: { error: record("save-error") },
        route: "competition", reportTab: "finals",
        leaveResultEditors: next => { trace.push(["leave", next]); return true; },
        closeNavigationDrawer: record("close"), render: record("render"),
        saveState: async () => { trace.push(["save"]); },
        savePrelimResults: record("prelim"), saveFinalResults: record("final"),
        uploadCompetitionAsset: record("upload"), exportResults: record("export"),
        runFinalsReport: record("report"),
        buildBracket: record("bracket"), printSingleStackerSheet: record("stacker-sheet"),
        printCurrentFinalSheet: record("final-sheet"),
        deleteDouble: id => { trace.push(["delete-double", id]); return false; },
        ...overrides
    };
    vm.runInNewContext(source, context);
    await handler({ target: { closest(selector) {
        assert.equal(selector, "[data-route], [data-action]");
        return dataset === null ? null : { dataset };
    } } });
    assert.deepEqual(trace, expected, name);
    if (check) check(context);
}
async function run() {
    await scenario("unrelated click", null, {}, []);
    await scenario("cancelled route preserves route and avoids render/save", { route: "reports" },
        { leaveResultEditors: () => false }, [], context => assert.equal(context.route, "competition"));
    await scenario("route selection renders without save", { route: "reports", reportTab: "admin" }, {},
        [["leave", "reports"], ["close"], ["render"]], context => {
            assert.equal(context.route, "reports"); assert.equal(context.reportTab, "admin");
        });
    await scenario("invalid report tab preserves selected tab", { route: "reports", reportTab: "invalid" }, {},
        [["leave", "reports"], ["close"], ["render"]], context => assert.equal(context.reportTab, "finals"));
    await scenario("direct print has no state side effect", { action: "print-current-view" }, {}, [["print"]]);
    await scenario("preliminary score save does not duplicate full-state save", { action: "save-prelim-results" }, {}, [["prelim"]]);
    await scenario("finals score save does not duplicate full-state save", { action: "save-final-results" }, {}, [["final"]]);
    await scenario("asset upload does not duplicate full-state save", { action: "upload-asset", assetType: "logo" }, {}, [["upload", "logo"]]);
    await scenario("result export is read-only", { action: "export-results-csv" }, {}, [["export", "csv"]]);
    await scenario("finals report is read-only", { action: "run-finals-report" }, {}, [["report"]]);
    await scenario("cancelled doubles deletion renders but does not save", { action: "delete-double", id: "2.1" }, {},
        [["delete-double", "2.1"], ["render"]]);
    await scenario("preview never schedules a full-state save", { action: "print-paper-preview" }, {}, [["preview"]]);
    await scenario("paperwork never schedules a full-state save", { action: "paperwork", type: "individual" }, {},
        [["paperwork", "individual"]]);
    await scenario("bracket never schedules a full-state save", { action: "build-bracket" }, {}, [["bracket"]]);
    await scenario("stacker sheet never schedules a full-state save", { action: "print-stacker-sheet", id: "1.1" }, {},
        [["stacker-sheet", "1.1"]]);
    await scenario("final sheet never schedules a full-state save", { action: "print-final-sheet" }, {}, [["final-sheet"]]);
    await scenario("route takes precedence over an action", { route: "reports", action: "print-current-view" }, {},
        [["leave", "reports"], ["close"], ["render"]]);
    await scenario("mutation still renders then saves", { action: "mark-read" },
        { state: { notifications: [{ id: "n1", read: false }] } }, [["render"], ["save"]],
        context => assert.equal(context.state.notifications[0].read, true));
    console.log("ENG-03A frontend architecture: " + Object.keys(manifest.shells).length + " shells, "
        + allActions.length + " delegated actions, 18 behavior scenarios passed.");
}
run().catch(error => { console.error(error); process.exitCode = 1; });
