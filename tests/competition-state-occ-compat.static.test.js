const fs = require("fs");
const assert = require("assert");

const program = fs.readFileSync("backend/StackMeet.Api/Program.cs", "utf8");
const admin = fs.readFileSync("backend/StackMeet.Api/wwwroot/admin.js", "utf8");
const controller = fs.readFileSync("backend/StackMeet.Api/Controllers/CompetitionAdminController.cs", "utf8");
const securityScript = fs.readFileSync("tests/security-api.integration.ps1", "utf8");

assert.match(program, /WithHeaders\([^;]*"If-Match"\)/s);
assert.match(program, /WithExposedHeaders\("ETag"\)/);

assert.match(admin, /async function requestResponse\(/);
assert.match(admin, /\/api\/admin\/competitions\/\$\{encodeURIComponent\(key\)\}\/state\/export/);
assert.match(admin, /headers\?\.get\?\.\("ETag"\)/);
assert.match(admin, /\/api\/admin\/competitions\/\$\{encodeURIComponent\(key\)\}\/state\/import/);
assert.match(admin, /"If-Match": etag/);
assert.doesNotMatch(admin, /request\(`\/api\/state\/\$\{encodeURIComponent\(key\)\}`[\s\S]*method: "POST"/);

assert.match(controller, /\[HttpGet\("\{competitionKey\}\/state\/export"\)\][\s\S]*SetEtag\(state\.StateRevision\)/);
assert.match(controller, /\[HttpPost\("\{competitionKey\}\/state\/import"\)\]/);
assert.match(controller, /ImportState[\s\S]*TryExpectedRevision\(Request\.Headers\["If-Match"\]/);
assert.match(controller, /ImportState[\s\S]*BeginTransactionAsync\(IsolationLevel\.Serializable/);
assert.match(controller, /ImportState[\s\S]*UPDLOCK, HOLDLOCK/);
assert.match(controller, /ImportState[\s\S]*state\.StateRevision\+\+/);
assert.match(controller, /admin\.competition\.state_imported/);
assert.match(controller, /ImportState[\s\S]*SendAsync\("CompetitionChanged"/);
assert.match(controller, /ResetState[\s\S]*state\.StateRevision\+\+/);
assert.match(controller, /ResetState[\s\S]*SendAsync\("CompetitionChanged"/);
assert.doesNotMatch(controller, /ResultsUpdated/);

assert.match(securityScript, /\$etag = \[string\]\$before\.Headers\["ETag"\]/);
assert.match(securityScript, /"If-Match" = \$etag/);
assert.match(securityScript, /Assert-Status \$invalidJson 400 "malformed state JSON rejection"/);

console.log("CompetitionState OCC compatibility safety tests passed.");
