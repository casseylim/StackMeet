const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const controller = fs.readFileSync(path.join(root, "backend/StackMeet.Api/Controllers/CompetitionsController.cs"), "utf8");

const authorization = fs.readFileSync(path.join(root, "backend/StackMeet.Api/Services/SessionAuthorization.cs"), "utf8");
const scope = authorization.slice(authorization.indexOf("public static IQueryable<Competition> ScopeCompetitions("));
assert.ok(scope.startsWith("public static IQueryable<Competition> ScopeCompetitions("), "Shared competition scope must exist.");

const getStart = controller.indexOf('[HttpGet("{id:int}")]');
const nextReadStart = controller.indexOf('[HttpGet("{id:int}/activity")]', getStart);
const createStart = controller.indexOf("[HttpPost]", getStart);
assert.ok(getStart >= 0 && createStart > getStart, "Competition detail GET and create action must exist.");
const getEnd = nextReadStart > getStart && nextReadStart < createStart ? nextReadStart : createStart;
const getMethod = controller.slice(getStart, getEnd);

assert.match(getMethod, /query = SessionAuthorization\.ScopeCompetitions\(query, HttpContext\);/,
  "Competition detail must invoke the shared scope before loading the entity.");
assert.match(getMethod, /ScopeCompetitions\(query, HttpContext\);[\s\S]*query\.SingleOrDefaultAsync\(ct\)/,
  "Competition detail must apply authorization before querying the database.");
assert.match(scope, /if \(context\.Items\["StackMeetSession"\] is not SessionToken session\) return query\.Where\(_ => false\);/,
  "Missing session authority must fail closed.");
assert.match(scope, /if \(!session\.IsAccountSession\)\s*return query\.Where\(item => item\.CompetitionKey == session\.CompetitionId\);\s*if \(session\.IsSystemAdmin\) return query;/,
  "Only legacy sessions use the legacy competition key; administrators retain global scope.");
assert.match(scope, /CompetitionUsers\.Any\(access =>\s*access\.IsActive && access\.UserId == session\.UserId && access\.User\.IsActive\s*&& StackMeetRoles\.All\.Contains\(access\.Role\.Name\)\)/,
  "Non-admin account users must only read actively assigned competitions with recognized roles.");

console.log("Account competition detail access checks passed.");
