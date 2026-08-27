const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const publicResults = read("backend/StackMeet.Api/Controllers/PublicResultsController.cs");
const app = read("backend/StackMeet.Api/wwwroot/app.js");

assert.match(
  publicResults,
  /var stackers = sqlStackers\.Length > 0 \? sqlStackers : hasLegacyStackerFallback \? stateStackers : \[\];/,
  "Public Results must prefer SQL Stackers whenever SQL participant rows exist."
);
assert.match(
  publicResults,
  /var hasLegacyStackerFallback = stateStackers\.Length > 0;/,
  "Legacy JSON stackers may remain only as a migration fallback for competitions with no SQL stackers."
);
assert.match(
  publicResults,
  /stackersUpdatedAt/,
  "Public Results last-updated metadata must include SQL stacker changes."
);
assert.match(
  app,
  /function legacyStateForSave\(data\)[\s\S]*legacy\.stackers\s*=\s*\[\]/,
  "Normal CompetitionState saves must not persist SQL-owned stackers back into legacy JSON."
);
assert.match(
  app,
  /function withoutLegacyStackers\(data\)[\s\S]*legacy\.stackers\s*=\s*\[\]/,
  "CompetitionState loads must strip legacy stackers before SQL-owned stackers are restored."
);

console.log("SQL stacker authority checks passed.");
