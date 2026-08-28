"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const i18n = require("../backend/StackMeet.Api/wwwroot/js/i18n/I18n.js");
const source = fs.readFileSync(path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "js", "i18n", "LanguagePreference.js"), "utf8");

function harness() {
  const values = new Map();
  const storage = { getItem: key => values.get(key) || null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
  const root = { StackMeetI18n: i18n, localStorage: storage };
  root.globalThis = root;
  vm.runInNewContext(source, { window: root, globalThis: root });
  return { preference: root.StackMeetLanguagePreference, values };
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
console.log("Multilanguage v2 Phase 2 integration tests passed.");
