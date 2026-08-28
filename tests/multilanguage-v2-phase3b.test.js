"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const rootPath = path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot");
const I18n = require(path.join(rootPath, "js", "i18n", "I18n.js"));
const uiSource = fs.readFileSync(path.join(rootPath, "js", "i18n", "UiLocalization.js"), "utf8");
const authSource = fs.readFileSync(path.join(rootPath, "js", "auth", "AuthSession.js"), "utf8");
const accountSource = fs.readFileSync(path.join(rootPath, "account.js"), "utf8");
const adminSource = fs.readFileSync(path.join(rootPath, "admin.js"), "utf8");
const pageSources = ["index.html", "account.html", "admin.html"].map(file => fs.readFileSync(path.join(rootPath, file), "utf8"));

const localeRoot = {};
localeRoot.globalThis = localeRoot;
for (const file of ["en.js", "ms.js", "zh-Hans.js"]) {
  vm.runInNewContext(fs.readFileSync(path.join(rootPath, "js", "i18n", "locales", file), "utf8"), { window: localeRoot, globalThis: localeRoot });
}
const locales = localeRoot.StackMeetI18nLocales;
const keys = Object.keys(locales.en).sort();
for (const language of ["en", "ms", "zh-Hans"]) {
  assert.deepStrictEqual(Object.keys(locales[language]).sort(), keys, `${language} must have exact Phase 3B locale parity`);
  keys.forEach(key => assert.notStrictEqual(String(locales[language][key]).trim(), "", `${language} has a blank value for ${key}`));
}

let selectedLanguage = "en";
const uiContext = {
  StackMeetI18n: I18n,
  StackMeetI18nLocales: locales,
  StackMeetLanguagePreference: {
    getPreferredLanguage: () => selectedLanguage,
    setPreferredLanguage: code => { selectedLanguage = I18n.normalizeLanguageCode(code); return selectedLanguage; }
  }
};
uiContext.window = uiContext;
uiContext.globalThis = uiContext;
vm.runInNewContext(uiSource, uiContext);
const ui = uiContext.StackMeetUiLocalization;

function translated(template, language, values = {}) {
  selectedLanguage = language;
  return ui.tf(template, values);
}

const expected = {
  ms: {
    login: "Log Masuk NADITrack", forgot: "Hantar pautan tetapan semula", reset: "Tetapkan Semula Kata Laluan", activate: "Aktifkan Akaun",
    gender: "2 Perempuan // 3 Lelaki", competition: "Pilih pertandingan yang ditetapkan.", role: "Pengurus Pertandingan", status: "Aktif / Belum Disahkan",
    saved: "Pengguna dan kata laluan disimpan.", confirm: "Hantar pautan tetapan semula kata laluan kepada operator@example.com?", email: "E-mel sudah wujud.", locked: "Terlalu banyak percubaan kata laluan gagal. Cuba lagi dalam kira-kira 5 minit."
  },
  "zh-Hans": {
    login: "NADITrack 登录", forgot: "发送重置链接", reset: "重置密码", activate: "激活账户",
    gender: "2 名女子 // 3 名男子", competition: "请选择已分配的比赛。", role: "比赛管理员", status: "启用 / 未确认",
    saved: "用户和密码已保存。", confirm: "要向 operator@example.com 发送密码重置链接吗？", email: "邮箱已存在。", locked: "密码失败尝试次数过多。请在大约 5 分钟后重试。"
  }
};

for (const language of ["ms", "zh-Hans"]) {
  assert.strictEqual(I18n.translate("NADITrack Login", language, {}, locales), expected[language].login, `${language} login heading`);
  assert.strictEqual(I18n.translate("Send reset link", language, {}, locales), expected[language].forgot, `${language} forgot-password action`);
  assert.strictEqual(I18n.translate("Reset Password", language, {}, locales), expected[language].reset, `${language} reset heading`);
  assert.strictEqual(I18n.translate("Activate Account", language, {}, locales), expected[language].activate, `${language} activation heading`);
  assert.strictEqual(translated("{female} Female // {male} Male", language, { female: 2, male: 3 }), expected[language].gender, `${language} parameter formatting`);
  assert.strictEqual(I18n.translate("Choose an assigned competition.", language, {}, locales), expected[language].competition, `${language} auth validation`);
  assert.strictEqual(ui.roleLabel("CompetitionManager"), expected[language].role, `${language} stored role display label`);
  assert.strictEqual(translated("{status} / Unconfirmed", language, { status: I18n.translate("Active", language, {}, locales) }), expected[language].status, `${language} user status`);
  assert.strictEqual(I18n.translate("User and password saved.", language, {}, locales), expected[language].saved, `${language} admin success message`);
  assert.strictEqual(translated("Send password reset link to {email}?", language, { email: "operator@example.com" }), expected[language].confirm, `${language} reset confirmation`);
  assert.strictEqual(ui.translateKnownMessage("Email already exists."), expected[language].email, `${language} known server validation`);
  assert.strictEqual(ui.translateKnownMessage("Too many failed password attempts. Try again in about 5 minutes."), expected[language].locked, `${language} lockout message`);
  assert.strictEqual(translated("Team {id}: {name}", language, { id: "2.1", name: "Domain Participant" }), language === "ms" ? "Pasukan 2.1: Domain Participant" : "队伍 2.1：Domain Participant", `${language} domain values remain unchanged`);
}

assert.strictEqual(ui.translateKnownMessage("Unexpected provider detail: secret-value"), "Unexpected provider detail: secret-value", "unknown technical details are not blindly translated");
assert.strictEqual(translated("This will permanently delete {email} and remove their competition access.\n\nType {confirmation} to confirm.", "ms", { email: "operator@example.com", confirmation: "DELETE operator@example.com" }), "Ini akan memadam operator@example.com secara kekal dan membuang akses pertandingan mereka.\n\nTaip DELETE operator@example.com untuk mengesahkan.");
assert.strictEqual(translated("This will permanently delete {email} and remove their competition access.\n\nType {confirmation} to confirm.", "zh-Hans", { email: "operator@example.com", confirmation: "DELETE operator@example.com" }), "这将永久删除 operator@example.com 并移除其比赛访问权限。\n\n输入 DELETE operator@example.com 以确认。");

assert.match(authSource, /initializeLoginLanguage/);
assert.match(authSource, /knownMessage\(message, "Login failed\."\)/);
assert.match(authSource, /data-domain-option/);
assert.match(accountSource, /ui\.apply\(accountRoot\)/);
assert.match(adminSource, /ui\?\.apply\(document\.querySelector\("\.admin-shell"\)\)/);
assert.match(adminSource, /roleLabel\(item\.role\)/);
assert.match(adminSource, /new Intl\.DateTimeFormat\(stackMeetLocale\(\)/);
assert.doesNotMatch(authSource, /throw new Error\(message\)/, "auth must map known errors before displaying them");
assert.doesNotMatch(adminSource, /message\("(Admin data refreshed|Email setup saved|Logged out|User saved|User access updated)\./, "admin UI messages must use translation helpers");
for (const source of pageSources) {
  assert.match(source, /js\/i18n\/UiLocalization\.js\?v=multilanguage-v2-phase3b/);
  for (const match of source.matchAll(/data-i18n(?:-(?:placeholder|aria-label|title|alt))?="([^"]+)"/g)) {
    for (const language of ["en", "ms", "zh-Hans"]) assert.ok(Object.prototype.hasOwnProperty.call(locales[language], match[1]), `${language} marker coverage for ${match[1]}`);
  }
}

console.log("Multilanguage v2 Phase 3B authenticated UI regression tests passed.");
