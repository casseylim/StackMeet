const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");
const auth = read("backend/StackMeet.Api/Controllers/AuthController.cs");
const tokens = read("backend/StackMeet.Api/Services/AccountTokenService.cs");
const admins = read("backend/StackMeet.Api/Controllers/AdminUsersController.cs");
const state = read("backend/StackMeet.Api/Controllers/CompetitionStateController.cs");
const results = read("backend/StackMeet.Api/Controllers/CompetitionResultsController.cs");
const admin = read("backend/StackMeet.Api/Controllers/CompetitionAdminController.cs");
const reset = auth.slice(auth.indexOf("ResetPassword"), auth.indexOf("async Task<ActionResult<LoginResponse>> LoginAccount"));
assert.doesNotMatch(reset, /user\.IsActive\s*=/);
assert.match(reset, /FailedLoginAttempts = 0/);
assert.match(tokens, /ExecuteUpdateAsync/);
assert.match(tokens, /UsedAt == null/);
assert.match(tokens, /ExpiresAt > now/);
assert.match(tokens, /IsolationLevel\.Serializable/);
assert.match(admins, /IsActive && item\.IsSystemAdmin/);
assert.match(admins, /At least one active global system administrator must remain/);
for (const source of [state, results, admin]) {
  assert.match(source, /try\s*\{[\s\S]*SendAsync/);
  assert.match(source, /catch \(Exception ex\)[\s\S]*LogWarning/);
  assert.match(source, /CommitAsync[\s\S]*try\s*\{[\s\S]*SendAsync/);
}
console.log("Core integrity V2 static guards passed.");
