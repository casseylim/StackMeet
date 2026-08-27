const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const controller = fs.readFileSync(path.join(root, "backend/StackMeet.Api/Controllers/CompetitionsController.cs"), "utf8");

const getStart = controller.indexOf('[HttpGet("{id:int}")]');
const createStart = controller.indexOf("[HttpPost]", getStart);
const getMethod = controller.slice(getStart, createStart);

assert.match(getMethod, /session\.IsAccountSession && !session\.IsSystemAdmin/,
  "Account users must use competition assignments when reading one competition.");
assert.match(getMethod, /CompetitionUsers\.Any\(access => access\.IsActive && access\.UserId == session\.UserId\)/,
  "Non-admin account users must only read assigned competitions.");
assert.match(getMethod, /else if \(!session\.IsAccountSession\)/,
  "Legacy competition-password sessions must remain scoped by CompetitionKey.");
assert.doesNotMatch(getMethod, /query = query\.Where\(item => item\.CompetitionKey == session\.CompetitionId\);[\s\S]*session\.IsAccountSession/,
  "Account sessions must not be filtered by the empty legacy CompetitionId token field.");

console.log("Account competition detail access checks passed.");
