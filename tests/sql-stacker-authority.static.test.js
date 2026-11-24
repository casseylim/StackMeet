const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const publicResults = read("backend/StackMeet.Api/Controllers/PublicResultsController.cs");
const app = read("backend/StackMeet.Api/wwwroot/app.js");
const repository = read("backend/StackMeet.Api/wwwroot/js/storage/Repository.js");

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
assert.match(app, /function legacyStateForSave\(data\) \{\s*return CompetitionRepository\.legacyStateForSave\(data\);/);
assert.match(app, /function withoutLegacyStackers\(data\) \{\s*return CompetitionRepository\.withoutLegacyStackers\(data\);/);
assert.match(repository, /static legacyStateForSave\(data\) \{\s*const legacy = structuredClone\(data\);\s*legacy\.stackers = \[\];\s*legacy\.results = \[\];\s*return legacy;/,
  "Normal CompetitionState saves must not persist SQL-owned stackers or results back into legacy JSON.");
assert.match(repository, /static withoutLegacyStackers\(data\) \{\s*const legacy = structuredClone\(data\);\s*legacy\.stackers = \[\];\s*return legacy;/,
  "CompetitionState loads must strip legacy stackers before SQL-owned stackers are restored.");
assert.match(app, /await repository\.save\(legacyStateForSave\(mergedState\)\)/,
  "The actual queued save must apply the legacy projection.");

console.log("SQL stacker authority checks passed.");
