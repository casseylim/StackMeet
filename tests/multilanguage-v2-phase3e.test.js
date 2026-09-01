const assert = require("assert");
const fs = require("fs");
const vm = require("vm");

const root = "backend/StackMeet.Api/wwwroot";
const localeFiles = { en: "en", ms: "ms", "zh-Hans": "zh-Hans" };
const locales = {};
for (const [code, file] of Object.entries(localeFiles)) {
  const window = {};
  vm.runInNewContext(fs.readFileSync(`${root}/js/i18n/locales/${file}.js`, "utf8"), { window });
  locales[code] = window.StackMeetI18nLocales[code];
}
const keys = Object.keys(locales.en).sort();
for (const code of ["ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[code]).sort(), keys, `${code} locale parity`);
  for (const key of keys) {
    assert.ok(String(locales[code][key]).trim(), `${code} blank translation: ${key}`);
    assert.deepStrictEqual((String(locales[code][key]).match(/\{[A-Za-z]\w*\}/g) || []).sort(), (String(locales.en[key]).match(/\{[A-Za-z]\w*\}/g) || []).sort(), `${code} placeholder parity: ${key}`);
  }
}

const index = fs.readFileSync(`${root}/index.html`, "utf8");
const app = fs.readFileSync(`${root}/app.js`, "utf8");
const auth = fs.readFileSync(`${root}/js/auth/AuthSession.js`, "utf8");
const print = fs.readFileSync(`${root}/js/printing/TimeSheetPrinting.js`, "utf8");
const required = ["Dashboard", "Settings", "Competition", "Participant", "Awards Planner", "Leader Board", "Save", "Cancel", "Delete", "Search stackers", "Available", "Already selected here", "Normal", "Special", "Male", "Female", "Yes", "No", "Online mode", "Saved", "Save Failed", "Print", "Export CSV", "Individual", "Doubles", "Relay", "Preliminary", "Finals", "Results", "No results"];
for (const key of required) {
  for (const code of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[code], key), `${code} missing required Phase 3E key: ${key}`);
  if (!["Yes", "No", "Normal", "Special", "Male", "Female", "Individual", "Doubles", "Relay"].includes(key)) {
    assert.notStrictEqual(locales.ms[key], locales.en[key], `Malay fallback: ${key}`);
    assert.notStrictEqual(locales["zh-Hans"][key], locales.en[key], `Chinese fallback: ${key}`);
  }
}

const phase3eKeys = ["Version", "Prelim Entry", "Finals Entry", "{type} {stage} Entry", "{count} added", "{count} updated", "Unable to verify persistence", "{place} place", "Individual - {division}", "Doubles - {division}", "Relay Teams - {division}", "Planned division // {event}", "Planned category // {event} // 2 awards per team", "Planned category // {event} // {count} awards per team", "Top Male", "Top Female", "Top Special Male", "Top Special Female", "Top Overall Combined", "Normal male stackers", "Normal female stackers", "Special male stackers", "Special female stackers", "All normal stackers combined", "Award Item", "Trophy", "Medal", "Yes", "No", "No missing divisions"];
for (const key of phase3eKeys) {
  for (const code of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[code], key), `${code} missing generated UI key: ${key}`);
}
assert.ok(app.includes('t(s.special || "No")') && app.includes('t(s.paid || "No")') && app.includes('t(s.checkedIn || "No")'), "participant status display must be localized");
assert.ok(app.includes('t("Version")'), "dashboard version must be localized");
assert.ok(app.includes('tf("{count} added"') && app.includes('tf("{count} updated"'), "prelim actions must be localized");
assert.ok(app.includes('tf("{type} {stage} Entry"'), "entry heading must be localized");
assert.ok(app.includes('tf("Individual - {division}"') && app.includes('tf("Planned division // {event}"'), "award summaries must preserve domain placeholders");
assert.ok(app.includes('t(row.item)') && app.includes('t("Award Item")'), "award presentation must be localized");
assert.ok(app.includes('t("No missing divisions")') && app.includes('t(sheet.entryType)'), "final presentation values must be localized");
assert.ok(!/t\(\s*s\.(name|org|division|country)\s*\)/.test(app) && !/t\(\s*participant\.name\s*\)/.test(app), "domain data must not be translated");

for (const file of [index, app, auth, print]) assert.ok(file.includes("t(") || file.includes("data-i18n"), "operator source uses localization");
assert.ok(index.includes("data-i18n=\"Language\""));
assert.ok(app.includes("translateChrome()") || app.includes("translateChrome"));
assert.ok(app.includes("t(\"Available\")") || app.includes("t(\"Already selected here\")"));
assert.ok(app.includes("t(\"Print\")") && app.includes("t(\"Export CSV\")"));
assert.ok(print.includes("tf(\"{from} to {to}\""));
assert.ok(print.includes("A4 portrait"));
assert.ok(!/\bt\(\s*stacker\.name\s*\)/.test(app) && !/\bt\(\s*division\s*\)/.test(app), "domain data must not be translated indiscriminately");
assert.ok(fs.existsSync(`${root}/js/reports/FinalsReportEngine.js`));
assert.ok(fs.existsSync("backend/StackMeet.Api/wwwroot/results/index.html"));
assert.ok(fs.existsSync("backend/StackMeet.Api/wwwroot/results/results.js"));
console.log("Phase 3E operator localization tests passed.");
