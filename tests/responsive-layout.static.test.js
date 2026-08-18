const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");

const root = path.resolve(__dirname, "..");
const css = fs.readFileSync(
  path.join(root, "backend/StackMeet.Api/wwwroot/styles.css"),
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

console.log("Responsive layout static tests passed.");
