"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const i18n = require("../backend/StackMeet.Api/wwwroot/js/i18n/I18n.js");
const source = fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "js", "i18n", "LanguagePreference.js"), "utf8");
const appSource = fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "app.js"), "utf8");

function harness() {
  const values = new Map();
  const storage = { getItem: key => values.get(key) || null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
  const root = { StackMeetI18n: i18n, localStorage: storage };
  root.globalThis = root;
  vm.runInNewContext(source, { window: root, globalThis: root });
  return { preference: root.StackMeetLanguagePreference, values };
}

function appTranslationHarness(language, translations) {
  const start = appSource.indexOf("function currentLanguage()");
  const end = appSource.indexOf("function translateChrome", start);
  const context = {
    state: { settings: { language: "en" }, translations },
    window: { StackMeetLanguagePreference: { getPreferredLanguage: () => language }, StackMeetI18n: i18n, StackMeetI18nLocales: { ms: { Dashboard: "built in" }, "zh-Hans": { Dashboard: "built in" } } }
  };
  context.globalThis = context;
  vm.runInNewContext(`${appSource.slice(start, end)}; globalThis.hooks = { currentLanguage, t };`, context);
  return context.hooks;
}

for (const [input, expected] of [["en", "en"], ["EN", "en"], [" en ", "en"], ["MS", "ms"], [" ms ", "ms"], ["ZH", "zh-Hans"], ["zh-hans", "zh-Hans"], [" ZH-HANS ", "zh-Hans"], ["", "en"], ["unknown", "en"]]) assert.strictEqual(i18n.normalizeLanguageCode(input), expected);
const a = harness();
assert.strictEqual(a.preference.setPreferredLanguage("ms"), "ms");
assert.strictEqual(a.values.get("naditrack.uiLanguage"), "ms");
assert.strictEqual(a.preference.getPreferredLanguage(), "ms");
assert.strictEqual(a.preference.setPreferredLanguage("zh"), "zh-Hans");
assert.strictEqual(a.preference.getPreferredLanguage(), "zh-Hans");
a.preference.clearPreferredLanguage();
assert.strictEqual(a.preference.getPreferredLanguage(), null);
const broken = { StackMeetI18n: i18n, localStorage: { getItem() { throw new Error("blocked"); }, setItem() { throw new Error("blocked"); }, removeItem() { throw new Error("blocked"); } } };
broken.globalThis = broken;
vm.runInNewContext(source, { window: broken, globalThis: broken });
assert.doesNotThrow(() => broken.StackMeetLanguagePreference.setPreferredLanguage("ms"));
assert.strictEqual(broken.StackMeetLanguagePreference.getPreferredLanguage(), null);
const b = harness();
b.preference.setPreferredLanguage("zh-Hans");
assert.strictEqual(a.preference.getPreferredLanguage(), null);
assert.strictEqual(b.preference.getPreferredLanguage(), "zh-Hans");
assert.strictEqual(i18n.translate("Dashboard", "zh-Hans", { "zh-Hans": { Dashboard: "canonical" }, zh: { Dashboard: "legacy" } }), "canonical");
assert.strictEqual({ settings: { language: "en" }, stackers: [], results: [] }.settings.language, "en");
const translated = appTranslationHarness("zh-Hans", { zh: { Dashboard: "legacy" }, "zh-Hans": { Dashboard: "canonical" } });
assert.strictEqual(translated.currentLanguage(), "zh-Hans");
assert.strictEqual(translated.t("Dashboard"), "canonical");
assert.strictEqual(appTranslationHarness("zh-Hans", { zh: { Dashboard: "legacy" } }).t("Dashboard"), "legacy");
assert.strictEqual(appTranslationHarness("zh-Hans", {}).t("Dashboard"), "built in");

const syncStart = appSource.indexOf("function currentLanguage()");
const syncEnd = appSource.indexOf("function languageLabel", syncStart);
const control = { value: "en" };
let renders = 0;
let saves = 0;
const operatorHarness = {
  state: { settings: { language: "en", ageCalculationMode: "yearBorn" }, stackers: [{ id: "1.1" }], results: [{ id: "r1" }], doubles: [{ id: "2.1" }], relays: [{ id: "3.1" }] },
  window: { StackMeetLanguagePreference: { getPreferredLanguage: () => control.value, setPreferredLanguage: value => { control.value = value; return value; } }, StackMeetI18n: { setDocumentLanguage: value => { operatorHarness.document.documentElement.lang = value; }, normalizeLanguageCode: value => value }, },
  document: { documentElement: { lang: "en" }, getElementById: id => id === "operatorLanguage" ? control : null },
  render: () => { renders += 1; },
  saveState: () => { saves += 1; },
  globalThis: null
};
operatorHarness.globalThis = operatorHarness;
vm.runInNewContext(`${appSource.slice(syncStart, syncEnd)}; globalThis.hooks = { applyOperatorLanguage };`, operatorHarness);
const snapshot = JSON.stringify(operatorHarness.state);
operatorHarness.hooks.applyOperatorLanguage("ms");
assert.strictEqual(control.value, "ms");
assert.strictEqual(operatorHarness.document.documentElement.lang, "ms");
assert.strictEqual(renders, 1);
operatorHarness.hooks.applyOperatorLanguage("zh-Hans");
assert.strictEqual(control.value, "zh-Hans");
assert.strictEqual(operatorHarness.document.documentElement.lang, "zh-Hans");
assert.strictEqual(renders, 2);
assert.strictEqual(saves, 0);
assert.strictEqual(JSON.stringify(operatorHarness.state), snapshot);
console.log("Multilanguage v2 Phase 2 integration tests passed.");
