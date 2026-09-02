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
const authSession = fs.readFileSync(path.join(root, "js", "auth", "AuthSession.js"), "utf8");
const printEmptyState = "Choose a print item to generate a printable preview.";
const phase3e = fs.readFileSync(path.join(__dirname, "multilanguage-v2-phase3e.test.js"), "utf8");
assert.match(preference, /setPreferredLanguage/);
assert.match(i18n, /zh-Hans/);
for (const source of [app, resultsIndex, resultsJs]) assert.match(source, /t\(|tf\(|data-i18n/);
for (const marker of ["t(doubleTypeLabel(d))", "t(doubleStatusLabel(d))", "tf(\"Member {slot}\"", "placeNumber", "tf(\"Place {number}\"", "tf(\"{from} to {to}\"", "t(val(\"bracketType\"))"]) assert.ok(phase3e.includes(marker) || app.includes(marker), `Phase 3E guard missing: ${marker}`);
for (const file of ["multilanguage-v2-phase1.test.js", "multilanguage-v2-phase2.test.js", "multilanguage-v2-phase3b.test.js", "multilanguage-v2-phase3c.test.js", "multilanguage-v2-phase3d.test.js", "multilanguage-v2-phase3e.test.js", "multilanguage-v2-coverage.test.js"]) assert.ok(fs.existsSync(path.join(__dirname, file)), `regression test missing: ${file}`);
assert.ok(fs.existsSync(path.join(root, "results", "index.html")) && fs.existsSync(path.join(root, "results", "results.js")), "public Results assets missing");
assert.ok(!/t\(\s*(?:participant|stacker)\.(?:name|org|division|event|id)\s*\)/.test(app), "domain data must not be indiscriminately translated");
assert.ok(!app.includes("<h2>Preview</h2><p class=\"muted\">Choose a print item to generate a printable preview.</p>"), "print empty state must not hardcode English");
assert.match(app, /t\("Preview"\)/, "print empty-state heading must use localization");
assert.match(app, /t\("Choose a print item to generate a printable preview\."\)/, "print empty-state message must use localization");
assert.strictEqual(locales.en.Preview, "Preview");
assert.strictEqual(locales.ms.Preview, "Pratonton");
assert.strictEqual(locales["zh-Hans"].Preview, "预览");
assert.strictEqual(locales.en[printEmptyState], printEmptyState);
assert.strictEqual(locales.ms[printEmptyState], "Pilih item cetakan untuk menjana pratonton yang boleh dicetak.");
assert.strictEqual(locales["zh-Hans"][printEmptyState], "选择一个打印项目以生成可打印的预览。");
assert.strictEqual(locales.en["Live Results"], "Live Results");
assert.strictEqual(locales.ms["Live Results"], "Keputusan Langsung");
assert.strictEqual(locales["zh-Hans"]["Live Results"], "实时成绩");
assert.strictEqual(locales.en["NADITrack System"], "NADITrack System");
assert.strictEqual(locales.ms["NADITrack System"], "Sistem NADITrack");
assert.strictEqual(locales["zh-Hans"]["NADITrack System"], "NADITrack 系统");
assert.match(resultsJs, /const ownText = \[\.\.\.node\.childNodes\]/, "public static translation must preserve nested controls");
assert.match(resultsJs, /publicLanguage.*addEventListener|addEventListener\("change".*updatePublicLanguage/, "public language selector listener must remain");
assert.match(resultsJs, /current\.type \? t\(current\.type\)/, "dashboard type must be translated separately from event data");
assert.match(resultsJs, /statusDisplay\([^\n]*Official/, "Official must use presentation translation");
assert.match(resultsJs, /statusDisplay\([^\n]*Provisional/, "Provisional must use presentation translation");
assert.match(resultsJs, /statusDisplay\([^\n]*Qualified/, "Qualified must use presentation translation");
assert.match(resultsJs, /organizationDisplay\(row\.stacker\.org \|\| "Independent"\)/, "organization values must remain domain-safe");
assert.match(resultsJs, /t\("Doubles event"\)/, "Doubles event chrome must be localized");
assert.match(resultsJs, /t\("Relay event"\)/, "Relay event chrome must be localized");
assert.match(resultsJs, /\["Place", "Team", "Stackers", "Organization", "Best", "Status"\][\s\S]*?t\(label\)/, "Doubles headings must be localized");
assert.match(resultsJs, /\["Place", "Team", "Members", "Organization", "Best", "Status"\][\s\S]*?t\(label\)/, "Relay headings must be localized");
assert.match(resultsJs, /organizationDisplay\(row\.meta\.organization\)/, "Doubles/Relay organization display must be localized safely");
assert.match(authSession, /throw new Error\(message \|\| "Login failed\."\);/, "login errors must remain canonical before display translation");
assert.doesNotMatch(authSession, /throw new Error\(knownMessage\(message, "Login failed\."\)\);/, "login must not pre-translate API errors");
assert.strictEqual(locales.en["Invalid email or password."], "Invalid email or password.");
assert.strictEqual(locales.ms["Invalid email or password."], "E-mel atau kata laluan tidak sah.");
assert.strictEqual(locales["zh-Hans"]["Invalid email or password."], "邮箱或密码无效。");
assert.match(app, /browserTitle: "NADITrack System"/);
assert.match(app, /document\.title = t\(brandText\("browserTitle"\)\)/, "browser title must localize");
for (const locale of Object.values(locales)) for (const value of Object.values(locale)) assert.ok(!/\{[A-Za-z]\w*\}/.test(String(value)) || placeholders(value).length, "invalid unresolved locale placeholder");
console.log("Phase 3F automated acceptance guards passed.");
