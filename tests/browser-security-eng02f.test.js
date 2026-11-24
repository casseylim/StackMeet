"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const root = path.resolve(__dirname, "..", "backend/StackMeet.Api/wwwroot");
const read = name => fs.readFileSync(path.join(root, name), "utf8");
const app = read("app.js");
const results = read("results/results.js");
const printing = read("js/printing/TimeSheetPrinting.js");

assert.match(app, /\$\{esc\(tf\("\{prelims\} prelim \/ \{finals\} final", \{ prelims: state\.settings\.prelims, finals: state\.settings\.finals \}\)\)\}/);
assert.doesNotMatch(app + printing, /onclick\s*=\s*["']window\.print/);
assert.match(app, /if \(action === "print-current-view"\) \{ window\.print\(\); return; \}/);
assert.match(app, /if \(asset\.publicUrl !== expectedPath\) continue;/);

const start = results.indexOf("  function publicBrandingPath(");
const end = results.indexOf("\n  function render(payload)", start);
assert.ok(start >= 0 && end > start);
const validate = vm.runInNewContext("(" + results.slice(start, end).trim() + ")");
const fallback = "/assets/stackmeet-logo.png";
assert.equal(validate("/api/public/competitions/12/assets/logo", "logo", fallback), "/api/public/competitions/12/assets/logo");
for (const value of ["https://example.test/logo.png", "//example.test/logo.png",
    "data:image/svg+xml,invalid", "/api/public/competitions/12/assets/banner",
    "/api/public/competitions/12/assets/logo?extra=true", "/api/public/competitions/12/assets/logo\n",
    "/api/public/competitions/12/assets/../logo", null])
    assert.equal(validate(value, "logo", fallback), fallback);

async function checkAccount() {
    const nodes = new Map();
    const handlers = {};
    const node = id => {
        if (!nodes.has(id)) nodes.set(id, { value: "", textContent: "", classList: { toggle() {} },
            setAttribute() {}, addEventListener(name, handler) { handlers[id + ":" + name] = handler; }, reset() {} });
        return nodes.get(id);
    };
    node("accountPassword").value = node("accountConfirmPassword").value = "Synthetic-only-password";
    const location = new URL("https://example.test/account.html?purpose=reset&token=synthetic-onetime&lang=ms");
    let replaced, submitted;
    vm.runInNewContext(read("account.js"), {
        URL, URLSearchParams, location, window: {},
        history: { state: null, replaceState(_state, _title, url) { replaced = url; } },
        document: { getElementById: node, querySelector: () => node("root") },
        fetch: async (url, options) => { submitted = { url, body: JSON.parse(options.body) }; return { ok: true }; }
    });
    assert.equal(replaced, "https://example.test/account.html?purpose=reset&lang=ms");
    await handlers["accountForm:submit"]({ preventDefault() {} });
    assert.equal(submitted.url, "/api/auth/reset-password");
    assert.equal(submitted.body.token, "synthetic-onetime");
    submitted = null;
    await handlers["accountForm:submit"]({ preventDefault() {} });
    assert.equal(submitted, null, "Successful submission must discard the token in memory.");
}
checkAccount().then(() => console.log("ENG-02F browser security checks passed.")).catch(error => { console.error(error); process.exitCode = 1; });
