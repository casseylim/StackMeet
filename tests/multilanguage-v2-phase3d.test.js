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
assert.ok(source.includes('t("No stackers")'));
assert.match(source, /A4 portrait/);
console.log("Phase 3D localization coverage tests passed.");
