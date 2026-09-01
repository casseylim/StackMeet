const assert = require("assert");
const fs = require("fs");
const vm = require("vm");
const files = { en: "en", ms: "ms", "zh-Hans": "zh-Hans" };
const locales = {};
for (const [code, file] of Object.entries(files)) {
  const window = {};
  vm.runInNewContext(fs.readFileSync("backend/StackMeet.Api/wwwroot/js/i18n/locales/" + file + ".js", "utf8"), { window });
  locales[code] = window.StackMeetI18nLocales[code];
}
const keys = Object.keys(locales.en).sort();
for (const code of ["ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[code]).sort(), keys);
  for (const key of keys) {
    assert.ok(locales[code][key].trim());
    assert.deepStrictEqual((locales[code][key].match(/\{[A-Za-z]\w*\}/g) || []).sort(), (locales.en[key].match(/\{[A-Za-z]\w*\}/g) || []).sort());
  }
}
const source = fs.readFileSync("backend/StackMeet.Api/wwwroot/js/printing/TimeSheetPrinting.js", "utf8");
const appSource = fs.readFileSync("backend/StackMeet.Api/wwwroot/app.js", "utf8");
assert.ok(source.includes('t("No stackers")'));
assert.ok(source.includes('tf("{from} to {to}"'));
assert.match(source, /A4 portrait/);
assert.ok(appSource.includes('finalsReportActions'));
assert.ok(appSource.includes('t("Draft qualification snapshots")'));
assert.ok(appSource.includes('t("No qualifiers match the approved snapshots.")'));
assert.ok(appSource.includes('headers = ["Rank", "Organization"'));
assert.ok(appSource.includes('row.special === "Yes" ? t("Special") : t("Normal")'));
assert.ok(appSource.includes('t("Equal performance")'));
assert.ok(appSource.includes('t("Organization placement contributors")'));
assert.ok(appSource.includes('t("All Division Counts")'));
assert.ok(appSource.includes('headers: ["Stage", "Name", "Event", "Official"].map(t)'));
assert.ok(appSource.includes('const headers = selectedColumns.map(col => t(col.label))'));
assert.ok(appSource.includes('t("Unable to build report.")'));
assert.ok(appSource.includes('value: group'));
assert.ok(!appSource.includes('group.title.replace(/^Division: /'));
assert.ok(appSource.includes('t("Print")'));
assert.ok(appSource.includes('t("Export CSV")'));
console.log("Phase 3D localization coverage tests passed.");
