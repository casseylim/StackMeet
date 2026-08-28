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
for (const file of ["en.js", "ms.js", "zh-Hans.js"]) {
  vm.runInNewContext(fs.readFileSync(path.join(rootPath, "js", "i18n", "locales", file), "utf8"), { window: localeRoot, globalThis: localeRoot });
}
const locales = localeRoot.StackMeetI18nLocales;

const localeKeys = Object.keys(locales.en).sort();
for (const language of ["en", "ms", "zh-Hans"]) {
  const keys = Object.keys(locales[language]).sort();
  assert.deepStrictEqual(keys, localeKeys, `${language} locale keys must exactly match English`);
  keys.forEach(key => assert.notStrictEqual(String(locales[language][key]).trim(), "", `${language} translation is blank for ${key}`));
}
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
const markerKeys = [...htmlSource.matchAll(/data-i18n(?:-(?:placeholder|aria-label|title|alt))?="([^"]+)"/g)].map(match => match[1]);
const missingMarkers = [];
for (const key of markerKeys) for (const language of ["en", "ms", "zh-Hans"]) if (!Object.prototype.hasOwnProperty.call(locales[language], key)) missingMarkers.push(`${language}:${key}`);
assert.deepStrictEqual(missingMarkers, [], `Authenticated marker coverage is missing: ${missingMarkers.join(", ")}`);
const documentedStaticUiKeys = [
  "ID", "Name", "Age", "Gender", "Special", "Organization", "Division", "Country", "Paid", "Check-In", "Type", "Status", "Location", "Team Name", "Timed Relay Division", "Head-to-Head Division", "Members", "# Members", "Individual Time Sheets", "Doubles Time Sheets", "Relay Time Sheets", "Individual Finals", "Doubles Finals", "Relay Finals", "All Packets", "Name Badges", "SOC Packet", "Award Group", "Basis", "Places", "Item", "Quantity", "Rank", "Stacker / Team", "Prelims", "Attempt 1", "Attempt 2", "Attempt 3", "Best Time", "Place", "Stage", "Event", "Best", "Penalty", "Access", "Last Active", "Platform", "Browser"
];
for (const key of documentedStaticUiKeys) for (const language of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[language], key), `${language} documented static inventory is missing ${key}`);
const authenticatedRouteCoverage = {
  dashboard: ["Dashboard", "Tournament Snapshot"],
  settings: ["Competition Settings", "Competition Branding"],
  language: ["Language Translation Setup", "Search Translation"],
  stackers: ["Stackers List", "Create Competition"],
  doubles: ["Doubles", "Completed"],
  relay: ["Relay Teams", "Ready"],
  paperwork: ["Print Center", "Head To Head Brackets"],
  awards: ["Awards Planner", "Awards Summary"],
  competition: ["Individual Prelim Entry", "Recent Results"],
  reports: ["Competition Reports", "Admin Reports"],
  leaderboard: [],
  users: ["Users", "User Levels"]
};
for (const [route, requiredKeys] of Object.entries(authenticatedRouteCoverage)) {
  assert.ok(htmlSource.includes(`id="${route}View"`), `Authenticated route template is missing ${route}`);
  for (const key of requiredKeys) assert.ok(htmlSource.includes(`data-i18n="${key}"`) || locales.en[key], `${route} route coverage is missing ${key}`);
}
for (const marker of ["data-i18n", "data-i18n-placeholder", "data-i18n-aria-label", "data-i18n-title", "data-i18n-alt"]) {
  assert.match(appSource, new RegExp(marker.replace(/[=-]/g, "[=-]")), `${marker} must be supported by the runtime`);
}
assert.match(appSource, /function tf\(template, values\)/, "Parameterized runtime messages must use the safe formatter");
assert.match(appSource, /data-domain/, "Rendered domain values must have an explicit translation opt-out");
assert.match(appSource, /data-domain-option/, "Rendered domain options must have an explicit translation opt-out");
assert.match(appSource, /gender:\s*tf\("\{female\} Female \/\/ \{male\} Male"/, "Dashboard gender metrics must use a translated template");
assert.match(appSource, /tf\("\{prelims\} prelim \/ \{finals\} final"/, "Dashboard rounds must use a translated template");
assert.match(appSource, /<span data-i18n="Public Results">Public Results<\/span>/, "Public Results label must remain translatable");
assert.match(appSource, /rel="noopener" data-domain>\$\{esc\(resultsUrl\)\}/, "Only the public results URL must be protected from translation");
assert.doesNotMatch(appSource, /results-share no-auto-translate/, "Public Results label must not be inside a translation opt-out");
assert.match(appSource, /tf\("Team \{id\}: \{name\}"/, "Picker team status must translate its UI fragment");
assert.match(appSource, /t\("Already selected here"\)/, "Relay duplicate status must translate");
assert.match(appSource, /t\("Available"\)/, "Available picker status must translate");
assert.doesNotMatch(appSource, /`; removed from \$\{displaced\.join\(\", \"\)\}/, "Displaced-team suffixes must not be raw English");
assert.match(appSource, /tf\("\{id\} \{name\} was saved; removed from \{teams\}\."/, "Stacker doubles save displacement must use a complete translated template");
assert.match(appSource, /\[t\("English"\), t\("Bahasa Malaysia"\), t\("Simplified Chinese"\)\]/, "Navigation language list must use translated labels");
assert.match(appSource, /<span>\$\{esc\(t\("Stacker"\)\)\}<\/span><span>\$\{esc\(t\("Time"\)\)\}<\/span><span>\$\{esc\(t\("Gap"\)\)\}<\/span>/, "Leaderboard headers must translate without changing result values");
const dashboardRefreshStart = appSource.indexOf('if (rerender && route === "dashboard")');
const dashboardRefreshEnd = appSource.indexOf('if (rerender && route === "reports")', dashboardRefreshStart);
assert.ok(dashboardRefreshStart >= 0 && dashboardRefreshEnd > dashboardRefreshStart, "Dashboard polling rerender must remain detectable");
assert.match(appSource.slice(dashboardRefreshStart, dashboardRefreshEnd), /renderDashboard\(\);\s*applyTranslations\(view\);/, "Dashboard polling rerenders must reapply authenticated translations");
for (const resource of ["js/i18n/I18n.js", "js/i18n/locales/en.js", "js/i18n/locales/ms.js", "js/i18n/locales/zh-Hans.js", "js/i18n/LanguagePreference.js", "app.js"]) {
  assert.match(htmlSource, new RegExp(`${resource.replace(/[./-]/g, "\\$&")}\\?v=multilanguage-v2-phase3b`), `${resource} must use the Phase 3B cache key`);
}
assert.doesNotMatch(appSource, /applyTranslations\(document\.body\)/, "Login UI must not be included in authenticated translation traversal");
assert.match(appSource, /operatorIntlLocale/, "Date/time display must follow the selected operator locale");
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
const domainOption = makeElement("OPTION", "Dashboard", { value: "Dashboard", "data-domain-option": "true" });
body.children = [staticText, domainText, input, button, image, option, domainOption];
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
assert.strictEqual(domainOption.textContent, "Dashboard", "Domain option labels must not be translated");
assert.strictEqual(domainOption.attributes.value, "Dashboard", "Domain option values must remain unchanged");

const runtimeTemplates = [
  "Invalid time: {value}. Enter a time to 3 decimals or 999 for scratch.",
  "Invalid {event} time. Enter a time to 3 decimals or 999 for scratch.",
  "Save failed. Times remain on screen and were not cleared: {error}",
  "Save failed. Results were not committed: {error}",
  "Ready for Finals {id}: {entryType} // {division} // {event}.",
  "{id} saved. {count} final result(s) recorded; latest updates will synchronize automatically.",
  "Team {id}: {name}",
  "; removed from {teams}.",
  "{id} {name} was saved.",
  "{id} {name} was saved; removed from {teams}.",
  "{conflicts}. Saving will remove them from the current relay team.",
  "Delete {id} {name}?",
  "This will also remove {teamCount} related team(s) and {resultCount} result record(s)."
];
for (const template of runtimeTemplates) {
  const expected = [...template.matchAll(/\{([^}]+)\}/g)].map(match => match[1]).sort();
  for (const language of ["en", "ms", "zh-Hans"]) {
    assert.deepStrictEqual([...String(locales[language][template]).matchAll(/\{([^}]+)\}/g)].map(match => match[1]).sort(), expected, `${language} placeholders must match for ${template}`);
  }
}

function translated(template, language, values = {}) {
  return I18n.format(I18n.translate(template, language, {}, locales), values);
}

const residualAssertions = {
  ms: {
    gender: "2 Perempuan // 3 Lelaki",
    rounds: "1 awal / 2 akhir",
    publicResults: "Keputusan Awam",
    available: "Tersedia",
    duplicate: "Sudah dipilih di sini",
    team: "Pasukan 2.1: Domain Participant",
    removed: "; dibuang daripada 2.1, 3.1.",
    saved: "2.1 Domain Participant telah disimpan.",
    savedRemoved: "2.1 Domain Participant telah disimpan; dibuang daripada 2.1, 3.1.",
    navLanguages: "Inggeris / Bahasa Malaysia / Cina Ringkas",
    leaderboard: "Peserta / Masa / Jurang",
    relayConflict: "Domain Participant kini berada dalam 3.1. Menyimpan akan membuangnya daripada pasukan relay semasa."
  },
  "zh-Hans": {
    gender: "2 名女子 // 3 名男子",
    rounds: "1 轮预赛 / 2 轮决赛",
    publicResults: "公开成绩",
    available: "可用",
    duplicate: "已在此选择",
    team: "队伍 2.1：Domain Participant",
    removed: "；已从 2.1, 3.1 中移除。",
    saved: "2.1 Domain Participant 已保存。",
    savedRemoved: "2.1 Domain Participant 已保存；已从 2.1, 3.1 中移除。",
    navLanguages: "英文 / 马来文 / 简体中文",
    leaderboard: "选手 / 时间 / 差值",
    relayConflict: "Domain Participant 现在在 3.1 中。保存后将从当前接力队中移除。"
  }
};
for (const [language, expected] of Object.entries(residualAssertions)) {
  assert.strictEqual(translated("{female} Female // {male} Male", language, { female: 2, male: 3 }), expected.gender, `${language} dashboard gender output`);
  assert.strictEqual(translated("{prelims} prelim / {finals} final", language, { prelims: 1, finals: 2 }), expected.rounds, `${language} dashboard rounds output`);
  assert.strictEqual(I18n.translate("Public Results", language, {}, locales), expected.publicResults, `${language} Public Results label`);
  const resultsUrl = "https://results.example.test/2.1";
  assert.strictEqual(I18n.translate(resultsUrl, language, {}, locales), resultsUrl, `${language} Public Results URL remains domain data`);
  assert.strictEqual(I18n.translate("Available", language, {}, locales), expected.available, `${language} available picker status`);
  assert.strictEqual(I18n.translate("Already selected here", language, {}, locales), expected.duplicate, `${language} duplicate picker status`);
  assert.strictEqual(translated("Team {id}: {name}", language, { id: "2.1", name: "Domain Participant" }), expected.team, `${language} picker team status preserves domain data`);
  assert.strictEqual(translated("; removed from {teams}.", language, { teams: "2.1, 3.1" }), expected.removed, `${language} displaced-team suffix`);
  assert.strictEqual(translated("{id} {name} was saved.", language, { id: "2.1", name: "Domain Participant" }), expected.saved, `${language} stacker doubles save output`);
  assert.strictEqual(translated("{id} {name} was saved; removed from {teams}.", language, { id: "2.1", name: "Domain Participant", teams: "2.1, 3.1" }), expected.savedRemoved, `${language} stacker doubles displaced save output`);
  assert.strictEqual(["English", "Bahasa Malaysia", "Simplified Chinese"].map(key => I18n.translate(key, language, {}, locales)).join(" / "), expected.navLanguages, `${language} navigation language badge`);
  assert.strictEqual(["Stacker", "Time", "Gap"].map(key => I18n.translate(key, language, {}, locales)).join(" / "), expected.leaderboard, `${language} leaderboard headers`);
  const conflict = translated("{name} is now in {team}", language, { name: "Domain Participant", team: "3.1" });
  assert.strictEqual(translated("{conflicts}. Saving will remove them from the current relay team.", language, { conflicts: conflict }), expected.relayConflict, `${language} displaced relay warning`);
}

console.log("Multilanguage v2 Phase 3A.2 coverage and DOM characterization tests passed.");
