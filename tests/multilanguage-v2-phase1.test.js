"use strict";

const assert = require("assert");
const I18n = require("../backend/StackMeet.Api/wwwroot/js/i18n/I18n.js");

assert.strictEqual(I18n.normalizeLanguageCode("en"), "en");
assert.strictEqual(I18n.normalizeLanguageCode("ms"), "ms");
assert.strictEqual(I18n.normalizeLanguageCode("zh"), "zh-Hans");
assert.strictEqual(I18n.normalizeLanguageCode("unknown"), "en");
assert.deepStrictEqual(I18n.supportedLanguages(), ["en", "ms", "zh-Hans"]);
assert.strictEqual(I18n.translate("Dashboard", "en", {}), "Dashboard");
assert.strictEqual(I18n.translate("Dashboard", "ms", { ms: { Dashboard: "Papan Pemuka" } }), "Papan Pemuka");
assert.strictEqual(I18n.translate("Dashboard", "zh-Hans", { "zh-Hans": { Dashboard: "仪表板" } }), "仪表板");
assert.strictEqual(I18n.translate("Unknown", "ms", { ms: {} }), "Unknown");
assert.strictEqual(I18n.translate("Dashboard", "ms", { ms: { Dashboard: "Custom Dashboard" } }), "Custom Dashboard");

const before = { stackers: [{ id: "1.1" }], results: [{ id: "r1" }], settings: { language: "en" } };
const after = JSON.parse(JSON.stringify(before));
after.settings.language = "ms";
assert.deepStrictEqual({ stackers: after.stackers, results: after.results }, { stackers: before.stackers, results: before.results });

const documentObject = { documentElement: {} };
assert.strictEqual(I18n.setDocumentLanguage("zh", documentObject), "zh-Hans");
assert.strictEqual(documentObject.documentElement.lang, "zh-Hans");
console.log("Multilanguage v2 Phase 1 characterization tests passed.");
