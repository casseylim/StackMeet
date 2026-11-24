"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "..");
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, "security-endpoint-matrix.json"), "utf8"));
const controllers = path.join(root, "backend/StackMeet.Api/Controllers");
const actual = [];
for (const file of fs.readdirSync(controllers).filter(file => file.endsWith(".cs"))) {
    const source = fs.readFileSync(path.join(controllers, file), "utf8");
    let prefix = null;
    const attributes = /\bRoute\("([^"]+)"\)|\[Http(Get|Post|Put|Delete|Patch|Head|Options)(?:\("([^"]*)"\))?\]/g;
    let match;
    let verbs = 0;
    while ((match = attributes.exec(source))) {
        if (match[1] !== undefined) { prefix = "/" + match[1].replace(/^\/+|\/+$/g, ""); continue; }
        assert.ok(prefix, file + ": HTTP action missing reviewed route prefix");
        const template = match[3] || "";
        const route = template.startsWith("/") ? template : prefix + (template ? "/" + template : "");
        actual.push({ controller: file, method: match[2].toUpperCase(), route });
        verbs++;
    }
    assert.equal(verbs, (source.match(/\[Http[A-Za-z]+/g) || []).length,
        file + ": unrecognized HTTP action attribute; update the security inventory parser");
}
const identity = row => row.controller + "|" + row.method + "|" + row.route;
assert.deepEqual(actual.map(identity).sort(), manifest.map(identity).sort(),
    "Every controller HTTP action must have a reviewed authorization classification.");
assert.equal(new Set(manifest.map(identity)).size, manifest.length, "Duplicate inventory entry");
const allowed = new Set(["admin", "public-auth", "public-read", "public-shell", "session", "account",
    "account-read", "account-manage", "account-score", "compatible-read", "compatible-manage", "maintenance"]);
for (const row of manifest) {
    assert.ok(allowed.has(row.authority), "Unknown authority for " + row.route);
    if (row.route.startsWith("/api/admin/") || row.route === "/api/admin")
        assert.equal(row.authority, "admin");
    if (row.route.startsWith("/api/public/") || row.route === "/api/public") {
        assert.equal(row.authority, "public-read");
        assert.equal(row.method, "GET");
    }
    if (row.authority === "public-auth")
        assert.ok(row.method === "POST" && ["/api/auth/login", "/api/auth/forgot-password",
            "/api/auth/activate", "/api/auth/reset-password"].includes(row.route));
    if (row.authority === "public-read")
        assert.ok(row.method === "GET" && (row.route.startsWith("/api/public/") ||
            ["/api/health", "/api/version"].includes(row.route)));
    if (row.authority === "public-shell")
        assert.equal(row.route, "/Stackers/{nadiTrackId}");
}
console.log("ENG-02G endpoint inventory verified: " + manifest.length + " controller HTTP actions.");
