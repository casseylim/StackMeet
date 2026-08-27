const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");

const root = path.resolve(__dirname, "..");
const css = fs.readFileSync(
  path.join(root, "backend/StackMeet.Api/wwwroot/styles.css"),
  "utf8"
);
const html = fs.readFileSync(
  path.join(root, "backend/StackMeet.Api/wwwroot/index.html"),
  "utf8"
);
const client = fs.readFileSync(
  path.join(root, "backend/StackMeet.Api/wwwroot/app.js"),
  "utf8"
);

assert.match(css, /\.form-grid\s*\{[\s\S]*repeat\(auto-fit, minmax\(min\(100%, 220px\), 1fr\)\)/);
assert.match(css, /\.result-grid\s*\{[\s\S]*repeat\(auto-fit, minmax\(min\(100%, 145px\), 1fr\)\)/);
assert.match(css, /\.division-grid\s*\{[\s\S]*repeat\(auto-fit, minmax\(min\(100%, 190px\), 1fr\)\)/);
assert.match(css, /\.report-filters\s*\{[\s\S]*repeat\(auto-fit, minmax\(min\(100%, 220px\), 1fr\)\)/);
assert.match(css, /#nav\s*\{\s*grid-template-columns:\s*repeat\(auto-fit, minmax\(140px, 1fr\)\)/);
assert.match(css, /\.panel-head > \*/,);
assert.match(css, /\.toolbar > \*/,);
assert.match(css, /\.table-wrap\s*\{[\s\S]*overflow-x:\s*auto/);
assert.doesNotMatch(css, /@media print[\s\S]*repeat\(auto-fit/);
assert.match(css, /\.drawer-close\.ghost,\s*\.nav-menu-button\.ghost,\s*\.drawer-backdrop\s*\{\s*display:\s*none/);
assert.match(css, /\.drawer-close\.ghost,\s*\.nav-menu-button\.ghost\s*\{\s*display:\s*inline-flex/);
assert.match(css, /\.drawer-backdrop\[hidden\]\s*\{\s*display:\s*none/);
assert.match(css, /\.nav-menu-button\.ghost\s*\{[\s\S]*align-self:\s*flex-start;[\s\S]*width:\s*auto/);
assert.match(css, /\.drawer-close\s*\{[\s\S]*position:\s*absolute;[\s\S]*top:\s*18px;[\s\S]*right:\s*18px/);
assert.match(css, /\.sidebar \{[\s\S]*padding-bottom:\s*28px;[\s\S]*scrollbar-width:\s*thin/);
assert.match(css, /\.sidebar::-webkit-scrollbar-thumb[\s\S]*border-radius:\s*999px/);
assert.match(html, /id="navMenuBtn"[^>]*aria-expanded="false"[^>]*aria-controls="sidebarNav"/);
assert.match(html, /id="sidebarNav"[^>]*aria-label="Primary navigation"/);
assert.match(html, /id="navCloseBtn"[^>]*aria-label="Close navigation"/);
assert.match(html, /id="navBackdrop"[^>]*class="drawer-backdrop"/);
assert.match(client, /function setNavigationDrawer\(open\)/);
assert.match(client, /aria-expanded/);
assert.match(client, /event\.key === "Escape"/);
assert.match(client, /closeNavigationDrawer\(\);/);

console.log("Responsive layout static tests passed.");
