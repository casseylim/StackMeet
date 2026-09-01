const assert = require("assert");
const fs = require("fs");
const vm = require("vm");

const localeFiles = {
  en: "backend/StackMeet.Api/wwwroot/js/i18n/locales/en.js",
  ms: "backend/StackMeet.Api/wwwroot/js/i18n/locales/ms.js",
  "zh-Hans": "backend/StackMeet.Api/wwwroot/js/i18n/locales/zh-Hans.js"
};
const locales = {};
for (const [code, file] of Object.entries(localeFiles)) {
  const window = {};
  vm.runInNewContext(fs.readFileSync(file, "utf8"), { window });
  locales[code] = window.StackMeetI18nLocales[code];
}

const allowedIdenticalMs = new Set(["1.15 / 215 / 34","3.145 / 999","4.276 / 999","8.512 / 999","Bahasa Malaysia","Import","Import XML","Menu","Relay","Status","SOC","Logo","{status}: {id} {name}.","ID","3-3-3","3-6-3","Platform","NADITrack","noreply@example.com","smtp-relay.brevo.com","name@example.com","Item","Stack of Champions"]);
const allowedIdenticalZhHans = new Set(["1.15 / 215 / 34","3.145 / 999","4.276 / 999","8.512 / 999","WSSA ID","SOC","ID","3-3-3","3-6-3","NADITrack","noreply@example.com","smtp-relay.brevo.com","name@example.com","Brevo API","Stack of Champions"]);

const keys = Object.keys(locales.en).sort();
for (const code of ["ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[code]).sort(), keys, code + " locale key parity");
}
for (const [code, allowlist] of [["ms", allowedIdenticalMs], ["zh-Hans", allowedIdenticalZhHans]]) {
  const identical = new Set(keys.filter(key => locales[code][key] === locales.en[key]));
  const unjustified = [...identical].filter(key => !allowlist.has(key));
  assert.deepStrictEqual(unjustified, [], code + " has unjustified English-identical translations");
  for (const key of allowlist) {
    assert.ok(key in locales.en && locales[code][key] === locales.en[key], code + " allowlist entry is invalid: " + key);
  }
}
for (const code of Object.keys(locales)) {
  for (const [key, value] of Object.entries(locales[code])) {
    assert.ok(typeof value === "string" && value.trim(), code + " blank translation: " + key);
    const placeholders = value.match(/\{[A-Za-z]\w*\}/g) || [];
    const sourcePlaceholders = (locales.en[key].match(/\{[A-Za-z]\w*\}/g) || []);
    assert.deepStrictEqual(placeholders.sort(), sourcePlaceholders.sort(), code + " placeholder parity: " + key);
  }
}

assert.strictEqual(locales.ms["NADITrack Results"], "Keputusan NADITrack");
assert.strictEqual(locales.ms["Waiting for Results"], "Menunggu keputusan");
assert.strictEqual(locales.ms["{count} participants"], "{count} peserta");
assert.strictEqual(locales["zh-Hans"]["NADITrack Results"], "NADITrack 成绩");
assert.strictEqual(locales["zh-Hans"]["Waiting for Results"], "等待成绩");
assert.strictEqual(locales["zh-Hans"]["{count} participants"], "{count} 名选手");

const client = fs.readFileSync("backend/StackMeet.Api/wwwroot/results/results.js", "utf8");
const html = fs.readFileSync("backend/StackMeet.Api/wwwroot/results/index.html", "utf8");
const controller = fs.readFileSync("backend/StackMeet.Api/Controllers/PublicResultsController.cs", "utf8");
assert.match(client, /payload.settings?.language/);
assert.match(client, /normalizeLanguageCode/);
assert.match(client, /function publicUrlFor/);
assert.match(client, /hasExplicitLanguage/);
assert.match(client, /if \(!hasExplicitLanguage\)/);
assert.match(client, /t\("Official results"\)/);
assert.match(client, /t\("Live results"\)/);
assert.match(client, /function updatePublicNavigation/);
assert.match(client, /updatePublicNavigation\(\)/);
assert.match(client, /hasExplicitLanguage \|\| query\.get\("lang"\)/);
for (const key of ["Automatic live refresh", "Competition results", "Latest results", "Results by stage", "Individual Preliminary Results", "Individual Final Results", "All-Around Results", "Doubles Results", "Relay Results", "Medal Table", "Powered by NADITrack"]) {
  assert.ok(html.includes("data-i18n=\"" + key + "\""), "static UI must be localization-marked: " + key);
}
assert.match(client, /history.replaceState/);
assert.match(client, /document.title/);
assert.match(client, /setDocumentLanguage/);
assert.match(client, /Asia\/Kuala_Lumpur/);
assert.doesNotMatch(client, /saveState|CompetitionState/);
assert.match(controller, /language\s*=\s*Text\(settings, "language"\)/);
assert.match(controller, /PublicTranslations/);
assert.match(controller, /ValueKind == JsonValueKind.String/);
assert.doesNotMatch(controller, /return root/);

const domainValues = ["Kejuaraan ABC 2026", "Tan Wei Ming", "SJK(C) Example", "Speed Dragons", "Elite Open A", "3-6-3", "7.123", "ABC-2026"];
for (const value of domainValues) assert.ok(!client.includes("t(" + JSON.stringify(value) + ")"), "domain value must not be translated: " + value);

console.log("Phase 3C localization coverage tests passed.");
