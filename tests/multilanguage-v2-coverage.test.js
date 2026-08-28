"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const rootPath = path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot");
const appSource = fs.readFileSync(path.join(rootPath, "app.js"), "utf8");
const htmlSource = fs.readFileSync(path.join(rootPath, "index.html"), "utf8");
const I18n = require(path.join(rootPath, "js", "i18n", "I18n.js"));
const localeRoot = {};
localeRoot.globalThis = localeRoot;
for (const file of ["ms.js", "zh-Hans.js"]) {
  vm.runInNewContext(fs.readFileSync(path.join(rootPath, "js", "i18n", "locales", file), "utf8"), { window: localeRoot, globalThis: localeRoot });
}
const locales = localeRoot.StackMeetI18nLocales;

assert.ok(Object.keys(locales.ms).length >= 200, "Malay locale must contain the complete operator dictionary");
assert.ok(Object.keys(locales["zh-Hans"]).length >= 200, "Chinese locale must contain the complete operator dictionary");
for (const key of ["Dashboard", "Settings", "Competition Settings", "First name", "Last name", "Optional", "Search stackers", "Name or stacker ID", "Yes", "No", "Actual Age on Competition Date", "Year Born Only", "Top", "Trophy", "Medal", "Saving...", "Saved", "Save Failed"]) {
  assert.ok(locales.ms[key], `Malay dictionary is missing ${key}`);
  assert.ok(locales["zh-Hans"][key], `Chinese dictionary is missing ${key}`);
}

assert.strictEqual(I18n.translate("Dashboard", "ms", { ms: {} }, locales), "Papan Pemuka");
assert.strictEqual(I18n.translate("Dashboard", "zh-Hans", { zh: { Dashboard: "legacy" }, "zh-Hans": {} }, locales), "legacy");
assert.strictEqual(I18n.translate("Dashboard", "zh-Hans", { zh: { Dashboard: "legacy" }, "zh-Hans": { Dashboard: "canonical" } }, locales), "canonical");
assert.strictEqual(I18n.translate("Settings", "zh-Hans", { "zh-Hans": {} }, locales), "设置");
assert.strictEqual(I18n.translate("Unknown", "ms", {}, locales), "Unknown");
assert.strictEqual(I18n.format("{name} is in {team} ({count})", { name: "Dashboard", team: "3.1", count: 4 }), "Dashboard is in 3.1 (4)");
assert.strictEqual(I18n.format("{missing}", {}), "{missing}");

const placeholders = [...htmlSource.matchAll(/data-i18n-placeholder="([^"]+)"/g)].map(match => match[1]);
assert.ok(placeholders.length >= 27, "Authenticated form placeholders must use explicit translation markers");
for (const key of placeholders) {
  assert.ok(locales.ms[key], `Malay placeholder coverage is missing ${key}`);
  assert.ok(locales["zh-Hans"][key], `Chinese placeholder coverage is missing ${key}`);
}
for (const marker of ["data-i18n", "data-i18n-placeholder", "data-i18n-aria-label", "data-i18n-title", "data-i18n-alt"]) {
  assert.match(appSource, new RegExp(marker.replace(/[=-]/g, "[=-]")), `${marker} must be supported by the runtime`);
}
assert.match(appSource, /function tf\(template, values\)/, "Parameterized runtime messages must use the safe formatter");
assert.match(appSource, /data-domain/, "Rendered domain values must have an explicit translation opt-out");
assert.doesNotMatch(appSource, /state\.translations\?\.\[code\]\?\./, "applyTranslations must not resolve dictionaries independently");
assert.match(appSource, /state\.translations\[code\] = state\.translations\[code\] \|\| \{\}/, "Custom language edits must remain state-backed");
assert.match(appSource, /preservedTranslations/, "normalizeState must preserve unknown and legacy translation dictionaries");
assert.match(appSource, /data\.translations = preservedTranslations/, "Canonical and legacy Chinese translations must survive normalization");

function makeElement(tagName, text, attributes = {}) {
  const element = {
    tagName,
    textContent: text,
    nodeValue: undefined,
    parentElement: null,
    children: [],
    attributes: { ...attributes },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attributes, name) ? this.attributes[name] : null; },
    hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attributes, name); },
    setAttribute(name, value) { this.attributes[name] = String(value); },
    replaceChildren(child) { this.textContent = child.nodeValue || child.textContent || ""; },
    closest(selector) {
      let current = this;
      while (current) {
        if (selector.includes("[data-domain]") && current.hasAttribute?.("data-domain")) return current;
        if (selector.includes(".no-auto-translate") && current.attributes?.class?.includes("no-auto-translate")) return current;
        current = current.parentElement;
      }
      return null;
    },
    querySelectorAll() { return this.children.flatMap(child => [child, ...child.querySelectorAll()]); }
  };
  return element;
}

const body = makeElement("BODY", "");
const staticText = makeElement("H1", "Dashboard");
const domainText = makeElement("SPAN", "Dashboard", { "data-domain": "true" });
const input = makeElement("INPUT", "", { "data-i18n-placeholder": "Search stackers", placeholder: "Search stackers" });
const button = makeElement("BUTTON", "", { "data-i18n-aria-label": "Close navigation", "aria-label": "Close navigation", "data-i18n-title": "Settings", title: "Settings" });
const image = makeElement("IMG", "", { "data-i18n-alt": "competition-logo preview", alt: "competition-logo preview" });
const option = makeElement("OPTION", "Yes", { value: "Yes" });
body.children = [staticText, domainText, input, button, image, option];
for (const child of body.children) child.parentElement = body;
const textNodes = body.children.filter(child => child.tagName !== "INPUT" && child.tagName !== "IMG" && child.tagName !== "OPTION").map(element => {
  const node = { parentElement: element };
  Object.defineProperty(node, "nodeValue", { get: () => element.textContent, set: value => { element.textContent = value; } });
  return node;
});
const context = {
  state: { settings: { language: "zh-Hans" }, translations: {} },
  window: { StackMeetLanguagePreference: { getPreferredLanguage: () => "zh-Hans" }, StackMeetI18n: I18n, StackMeetI18nLocales: locales },
  document: { createTreeWalker: () => { let index = -1; return { nextNode() { index += 1; this.currentNode = textNodes[index]; return Boolean(this.currentNode); } }; } },
  NodeFilter: { SHOW_TEXT: 4 },
  globalThis: null
};
context.globalThis = context;
const start = appSource.indexOf("function currentLanguage()");
const end = appSource.indexOf("function renderDashboard", start);
vm.runInNewContext(`${appSource.slice(start, end)}; globalThis.hooks = { applyTranslations };`, context);
context.hooks.applyTranslations(body);
assert.strictEqual(staticText.textContent, "仪表板", "Static template text must translate through the shared resolver");
assert.strictEqual(domainText.textContent, "Dashboard", "Participant/domain data must not be translated");
assert.strictEqual(input.attributes.placeholder, "搜索选手", "Placeholders must translate");
assert.strictEqual(button.attributes["aria-label"], "关闭导航", "ARIA labels must translate");
assert.strictEqual(button.attributes.title, "设置", "Titles must translate");
assert.strictEqual(image.attributes.alt, "比赛标志预览", "Alt text must translate");
assert.strictEqual(option.textContent, "是", "Option display text must translate");
assert.strictEqual(option.attributes.value, "Yes", "Option semantic values must remain unchanged");

console.log("Multilanguage v2 Phase 3A coverage and DOM characterization tests passed.");
