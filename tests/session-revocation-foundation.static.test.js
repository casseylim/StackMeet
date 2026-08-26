const fs = require("fs");
const assert = require("assert");

const read = path => fs.readFileSync(path, "utf8");

const appUser = read("backend/StackMeet.Api/Models/AppUser.cs");
const db = read("backend/StackMeet.Api/Data/StackMeetDbContext.cs");
const token = read("backend/StackMeet.Api/Services/SessionToken.cs");
const tokenService = read("backend/StackMeet.Api/Services/SessionTokenService.cs");
const auth = read("backend/StackMeet.Api/Controllers/AuthController.cs");
const program = read("backend/StackMeet.Api/Program.cs");

assert.match(appUser, /long SessionVersion\s*\{\s*get;\s*set;\s*\}\s*=\s*1/);
assert.match(db, /SessionVersion\)\.IsRequired\(\)\.HasDefaultValue\(1L\)/);
assert.match(token, /long\? SessionVersion = null/);

assert.match(tokenService, /AccountPayloadVersion = "v3"/);
assert.doesNotMatch(tokenService, /AccountPayloadVersion = "v2"/);
assert.match(
    tokenService,
    /CreateForUser\([\s\S]*long sessionVersion[\s\S]*sessionVersion,[\s\S]*expiresAt\.ToUnixTimeSeconds/
);
assert.match(
    tokenService,
    /payload\.Length == 7[\s\S]*TryParse\(payload\[5\], out var sessionVersion\)[\s\S]*payload\[6\]/
);

assert.match(auth, /CreateForUser\([\s\S]*user\.SessionVersion/);
assert.match(program, /AccountSessionIsCurrent\(adminSession/);
assert.match(program, /session\.IsAccountSession[\s\S]*AccountSessionIsCurrent\(session/);
assert.match(
    program,
    /item\.IsActive[\s\S]*item\.SessionVersion == session\.SessionVersion\.Value[\s\S]*item\.IsSystemAdmin == session\.IsSystemAdmin/
);

assert.match(tokenService, /payload\.Length != 3/);

console.log("Session revocation foundation safety tests passed.");
