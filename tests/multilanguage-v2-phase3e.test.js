const assert = require("assert");
const fs = require("fs");
const vm = require("vm");

const root = "backend/StackMeet.Api/wwwroot";
const localeFiles = { en: "en", ms: "ms", "zh-Hans": "zh-Hans" };
const locales = {};
for (const [code, file] of Object.entries(localeFiles)) {
  const window = {};
  vm.runInNewContext(fs.readFileSync(`${root}/js/i18n/locales/${file}.js`, "utf8"), { window });
  locales[code] = window.StackMeetI18nLocales[code];
}
const keys = Object.keys(locales.en).sort();
for (const code of ["ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[code]).sort(), keys, `${code} locale parity`);
  for (const key of keys) {
    assert.ok(String(locales[code][key]).trim(), `${code} blank translation: ${key}`);
    assert.deepStrictEqual((String(locales[code][key]).match(/\{[A-Za-z]\w*\}/g) || []).sort(), (String(locales.en[key]).match(/\{[A-Za-z]\w*\}/g) || []).sort(), `${code} placeholder parity: ${key}`);
  }
}

const index = fs.readFileSync(`${root}/index.html`, "utf8");
const app = fs.readFileSync(`${root}/app.js`, "utf8");
const auth = fs.readFileSync(`${root}/js/auth/AuthSession.js`, "utf8");
const print = fs.readFileSync(`${root}/js/printing/TimeSheetPrinting.js`, "utf8");
const required = ["Dashboard", "Settings", "Competition", "Participant", "Awards Planner", "Leader Board", "Save", "Cancel", "Delete", "Search stackers", "Available", "Already selected here", "Normal", "Special", "Male", "Female", "Yes", "No", "Online mode", "Saved", "Save Failed", "Print", "Export CSV", "Individual", "Doubles", "Relay", "Preliminary", "Finals", "Results", "No results"];
for (const key of required) {
  for (const code of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[code], key), `${code} missing required Phase 3E key: ${key}`);
  if (!["Yes", "No", "Normal", "Special", "Male", "Female", "Individual", "Doubles", "Relay"].includes(key)) {
    assert.notStrictEqual(locales.ms[key], locales.en[key], `Malay fallback: ${key}`);
    assert.notStrictEqual(locales["zh-Hans"][key], locales.en[key], `Chinese fallback: ${key}`);
  }
}

const phase3eKeys = ["Version", "Prelim Entry", "Finals Entry", "{type} {stage} Entry", "{count} added", "{count} updated", "Unable to verify persistence", "Place {number}", "Individual - {division}", "Doubles - {division}", "Relay Teams - {division}", "Planned division // {event}", "Planned category // {event} // 2 awards per team", "Planned category // {event} // {count} awards per team", "Top Male", "Top Female", "Top Special Male", "Top Special Female", "Top Overall Combined", "Normal male stackers", "Normal female stackers", "Special male stackers", "Special female stackers", "All normal stackers combined", "Award Item", "Trophy", "Medal", "Yes", "No", "No missing divisions", "Member {slot}", "Optional Member {slot}", "Search Member {slot}", "Search Optional Member {slot}", "Location: {location}", "{country} · Organization: {organization}", "Stackers: {members} · Division: {division}"];
for (const key of phase3eKeys) {
  for (const code of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[code], key), `${code} missing generated UI key: ${key}`);
}
assert.ok(app.includes('t(s.special || "No")') && app.includes('t(s.paid || "No")') && app.includes('t(s.checkedIn || "No")'), "participant status display must be localized");
assert.ok(app.includes('t("Version")'), "dashboard version must be localized");
assert.ok(app.includes('tf("{count} added"') && app.includes('tf("{count} updated"'), "prelim actions must be localized");
assert.ok(app.includes('tf("{type} {stage} Entry"'), "entry heading must be localized");
assert.ok(app.includes('tf("Individual - {division}"') && app.includes('tf("Planned division // {event}"'), "award summaries must preserve domain placeholders");
assert.ok(app.includes('t(row.item)') && app.includes('t("Award Item")'), "award presentation must be localized");
assert.ok(app.includes('t("No missing divisions")') && app.includes('t(sheet.entryType)'), "final presentation values must be localized");
assert.ok(!/t\(\s*s\.(name|org|division|country)\s*\)/.test(app) && !/t\(\s*participant\.name\s*\)/.test(app), "domain data must not be translated");
assert.ok(app.includes('group: t(group.label)') && app.includes('basis: t(group.basis)') && app.includes('tf("Top {count}"'), "dynamic award rows must be localized at source");
assert.ok(app.includes('tf("Place {number}"') && !app.includes('tf("{place} place"'), "award ordinals must not leak English fragments");
assert.ok(app.includes('tf("Update {id}"') && app.includes('tf("Edit Stacker {id}"'), "stacker edit labels must survive rerender");
assert.ok(app.includes('tf("ID: {id}"') && app.includes('tf("Type: {type}"') && app.includes('tf("Status: {status}"') && app.includes('tf("Partner: {partner}"'), "doubles profile info must use formatted localization");
assert.ok(app.includes('t(r.stage)') && app.includes('t(resultStatusLabel(r))') && app.includes('tf("No {stage} results yet."'), "result rows must localize controlled display values");
assert.ok(app.includes('t("All required prelim times entered")') && app.includes('t("Complete")'), "missing-time rerenders must localize states");
assert.ok(app.includes('tf("{from} to {to}"') && app.includes('t("Print Range")') && app.includes('t("No stackers found in this range.")'), "print preview rerenders must localize chrome");
assert.ok(app.includes('t("All (by Overall)")') && app.includes('t("All (by Division)")') && app.includes('t("No Limit")'), "report filter options must localize dynamic labels");
assert.ok(app.includes('t("English")'), "language editor header must localize on rerender");
assert.ok(app.includes('t(doubleTypeLabel(d))') && app.includes('t(doubleStatusLabel(d))') && app.includes('t("No doubles found for this tab.")'), "doubles tab rerender must localize presentation");
assert.ok(app.includes('t(status)') && app.includes('t("No relay teams found for this tab.")') && app.includes('tf("Member {slot}"'), "relay tab rerender must localize presentation");
assert.ok(app.includes("placeNumber") && app.includes('tf("Place {number}"') && !app.includes('tf("{place} place"'), "award rows must use numeric localized placement");
assert.ok(app.includes('tf("Search Member {slot}"') && app.includes('tf("Search Optional Member {slot}"') && app.includes('tf("{country} · Organization: {organization}"') && app.includes('tf("Stackers: {members} · Division: {division}"'), "dynamic print/editor composites must preserve domain placeholders");
assert.ok(app.includes('packets: "All Packets"') && app.includes('badges: "Name Badges"') && app.includes('soc: "SOC Packet"') && app.includes('t(title)'), "print center generic titles must use translated labels");
assert.ok(app.includes('t(val("bracketType"))'), "bracket type must be presentation-localized");
assert.ok(!app.includes("translateAwardText"), "no-op award translation helper must not remain");

for (const file of [index, app, auth, print]) assert.ok(file.includes("t(") || file.includes("data-i18n"), "operator source uses localization");
assert.ok(index.includes("data-i18n=\"Language\""));
assert.ok(app.includes("translateChrome()") || app.includes("translateChrome"));
assert.ok(app.includes("t(\"Available\")") || app.includes("t(\"Already selected here\")"));
assert.ok(app.includes("t(\"Print\")") && app.includes("t(\"Export CSV\")"));
assert.ok(print.includes("tf(\"{from} to {to}\""));
assert.ok(print.includes("A4 portrait"));
assert.ok(!/\bt\(\s*stacker\.name\s*\)/.test(app) && !/\bt\(\s*division\s*\)/.test(app), "domain data must not be translated indiscriminately");
assert.ok(fs.existsSync(`${root}/js/reports/FinalsReportEngine.js`));
assert.ok(fs.existsSync("backend/StackMeet.Api/wwwroot/results/index.html"));
assert.ok(fs.existsSync("backend/StackMeet.Api/wwwroot/results/results.js"));
console.log("Phase 3E operator localization tests passed.");
