const fs = require("fs");
const assert = require("assert");

const read = path => fs.readFileSync(path, "utf8");

const db = read("backend/StackMeet.Api/Data/StackMeetDbContext.cs");
const sessions = read("backend/StackMeet.Api/Controllers/AuthSessionsController.cs");
const auth = read("backend/StackMeet.Api/Controllers/AuthController.cs");

assert.match(db, /SaveChanges\(bool acceptAllChangesOnSuccess\)[\s\S]*ApplyAccountSessionRevocations\(\)/);
assert.match(db, /SaveChangesAsync\(bool acceptAllChangesOnSuccess[\s\S]*ApplyAccountSessionRevocations\(\)/);
assert.match(db, /ChangeTracker\.Entries<AppUser>\(\)[\s\S]*EntityState\.Modified/);
assert.match(db, /Property\(item => item\.PasswordHash\)\.IsModified/);
assert.match(db, /Property\(item => item\.IsActive\)\.IsModified/);
assert.match(db, /Property\(item => item\.IsSystemAdmin\)\.IsModified/);
assert.match(db, /Property\(item => item\.EmailConfirmed\)\.IsModified/);
assert.match(db, /Property\(item => item\.IsPermanentlyLocked\)\.IsModified/);
assert.match(db, /entry\.Entity\.SessionVersion\+\+/);

const revocationHelper = db.slice(
  db.indexOf("void ApplyAccountSessionRevocations()"),
  db.indexOf("protected override void OnModelCreating")
);
assert.doesNotMatch(revocationHelper, /DisplayName/);
assert.doesNotMatch(revocationHelper, /FailedLoginAttempts/);
assert.doesNotMatch(revocationHelper, /LoginLockoutRound/);
assert.doesNotMatch(revocationHelper, /LockoutUntil/);
assert.doesNotMatch(revocationHelper, /CompetitionUser/);
assert.doesNotMatch(revocationHelper, /SessionVersion\)\.IsModified/);

assert.match(sessions, /\[HttpPost\("logout-all"\)\]/);
assert.match(sessions, /HttpContext\.Items\["StackMeetSession"\][\s\S]*SessionToken session/);
assert.match(sessions, /!session\.IsAccountSession/);
assert.match(sessions, /user\.SessionVersion\+\+/);
assert.match(sessions, /SaveChangesAsync\(ct\)/);
assert.match(sessions, /auth\.logout_all\.account/);

const logoutStart = auth.indexOf('public async Task<IActionResult> Logout(');
const logoutEnd = auth.indexOf('[HttpPost("forgot-password")]', logoutStart);
assert.ok(logoutStart >= 0 && logoutEnd > logoutStart, "Normal logout method must remain present.");
const normalLogout = auth.slice(logoutStart, logoutEnd);
assert.doesNotMatch(normalLogout, /SessionVersion/);

console.log("Session revocation trigger safety tests passed.");
