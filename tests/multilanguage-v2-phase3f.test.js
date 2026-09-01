const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot");
const localeFiles = { en: "en", ms: "ms", "zh-Hans": "zh-Hans" };
const locales = {};
for (const [code, file] of Object.entries(localeFiles)) {
  const window = {};
  vm.runInNewContext(fs.readFileSync(path.join(root, "js", "i18n", "locales", `${file}.js`), "utf8"), { window });
  locales[code] = window.StackMeetI18nLocales[code];
}
const keys = Object.keys(locales.en).sort();
const placeholders = value => (String(value).match(/\{[A-Za-z]\w*\}/g) || []).sort();
for (const code of ["ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[code]).sort(), keys, `${code} locale parity`);
  for (const key of keys) {
    assert.ok(String(locales[code][key]).trim(), `${code} blank locale value: ${key}`);
    assert.deepStrictEqual(placeholders(locales[code][key]), placeholders(locales.en[key]), `${code} placeholder parity: ${key}`);
  }
}
const preference = fs.readFileSync(path.join(root, "js", "i18n", "LanguagePreference.js"), "utf8");
const i18n = fs.readFileSync(path.join(root, "js", "i18n", "I18n.js"), "utf8");
const app = fs.readFileSync(path.join(root, "app.js"), "utf8");
const resultsIndex = fs.readFileSync(path.join(root, "results", "index.html"), "utf8");
const resultsJs = fs.readFileSync(path.join(root, "results", "results.js"), "utf8");
const phase3e = fs.readFileSync(path.join(__dirname, "multilanguage-v2-phase3e.test.js"), "utf8");
assert.match(preference, /setPreferredLanguage/);
assert.match(i18n, /zh-Hans/);
for (const source of [app, resultsIndex, resultsJs]) assert.match(source, /t\(|tf\(|data-i18n/);
for (const marker of ["t(doubleTypeLabel(d))", "t(doubleStatusLabel(d))", "tf(\"Member {slot}\"", "placeNumber", "tf(\"Place {number}\"", "tf(\"{from} to {to}\"", "t(val(\"bracketType\"))"]) assert.ok(phase3e.includes(marker) || app.includes(marker), `Phase 3E guard missing: ${marker}`);
for (const file of ["multilanguage-v2-phase1.test.js", "multilanguage-v2-phase2.test.js", "multilanguage-v2-phase3b.test.js", "multilanguage-v2-phase3c.test.js", "multilanguage-v2-phase3d.test.js", "multilanguage-v2-phase3e.test.js", "multilanguage-v2-coverage.test.js"]) assert.ok(fs.existsSync(path.join(__dirname, file)), `regression test missing: ${file}`);
assert.ok(fs.existsSync(path.join(root, "results", "index.html")) && fs.existsSync(path.join(root, "results", "results.js")), "public Results assets missing");
assert.ok(!/t\(\s*(?:participant|stacker)\.(?:name|org|division|event|id)\s*\)/.test(app), "domain data must not be indiscriminately translated");
for (const locale of Object.values(locales)) for (const value of Object.values(locale)) assert.ok(!/\{[A-Za-z]\w*\}/.test(String(value)) || placeholders(value).length, "invalid unresolved locale placeholder");
console.log("Phase 3F automated acceptance guards passed.");
