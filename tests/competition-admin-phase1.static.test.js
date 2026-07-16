"use strict";
const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const migration = read("backend/StackMeet.Api/Migrations/20260713034108_CompetitionAdminPhase1.cs");
const program = read("backend/StackMeet.Api/Program.cs");
const auth = read("backend/StackMeet.Api/Controllers/AuthController.cs");
const admin = read("backend/StackMeet.Api/Controllers/CompetitionAdminController.cs");
const appsettings = read("backend/StackMeet.Api/appsettings.json");

const up = migration.slice(migration.indexOf("protected override void Up"), migration.indexOf("protected override void Down"));
assert(!/DropTable|DropColumn|DeleteData|TRUNCATE|DELETE FROM/i.test(up), "Migration Up must not contain destructive operations.");
assert(/UPDATE \[dbo\]\.\[Competition\] SET \[CompetitionKey\] = UPPER\(\[CompetitionCode\]\)/.test(up), "Existing competitions must be backfilled from CompetitionCode.");
assert(/UPDATE \[dbo\]\.\[CompetitionState\] SET \[CreatedAt\] = \[UpdatedAt\]/.test(up), "Existing CompetitionState CreatedAt must be backfilled from UpdatedAt.");
assert(!/LoginPassword/.test(auth), "Competition login must not use one shared login password.");
assert(/PasswordHash/.test(auth) && /PasswordHash/.test(admin), "Competition password hashes must be used by auth/admin flows.");
assert(/X-StackMeet-Admin-Key/.test(program), "Admin endpoints must use separate admin authorization.");
assert(/SessionCanAccessPath/.test(program) && /CompetitionKey == session\.CompetitionId/.test(program), "Session isolation must compare token CompetitionKey to route data.");
assert(/DEFAULT state reset is blocked/.test(admin), "DEFAULT reset must be blocked in Phase 1.");
assert(!/"(AdminKey|ApiKey|SessionSigningKey|LoginPassword)"\s*:/.test(appsettings), "Secrets must not be committed in appsettings.json.");
console.log("Competition admin static safety tests passed.");