"use strict";
const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const stackers = read("backend/StackMeet.Api/Controllers/StackersController.cs");
const state = read("backend/StackMeet.Api/Controllers/CompetitionStateController.cs");
const permissions = read("backend/StackMeet.Api/Services/CompetitionPermissionService.cs");

assert(/CompetitionPermissionService permissions/.test(stackers), "StackersController must receive CompetitionPermissionService.");
assert(/RoleForCompetitionId/.test(stackers), "StackersController must resolve account role by competition ID.");
assert(/CanViewCompetition/.test(stackers), "Stacker reads must be guarded by competition view permission.");
assert(/CanManageCompetition/.test(stackers), "Stacker writes must be guarded by competition manage permission.");
assert(/Access\(competitionId, false/.test(stackers), "Stacker list/get endpoints must use read access checks.");
assert((stackers.match(/Access\(competitionId, true/g) || []).length >= 3, "Stacker create/update/delete endpoints must use write access checks.");

assert(/CompetitionPermissionService permissions/.test(state), "CompetitionStateController must receive CompetitionPermissionService.");
assert(/RoleForCompetitionKey/.test(state), "CompetitionStateController must resolve account role by competition key.");
assert(/Access\(normalizedKey, false/.test(state), "Competition state GET must require view permission.");
assert(/Access\(normalizedKey, true/.test(state), "Competition state POST must require manage permission.");
assert(/CanViewCompetition/.test(state), "Competition state reads must be guarded by competition view permission.");
assert(/CanManageCompetition/.test(state), "Competition state writes must be guarded by competition manage permission.");

assert(/StackMeetMaintenanceApiKey/.test(stackers) && /StackMeetMaintenanceApiKey/.test(state), "Maintenance recovery access must remain explicit.");
assert(/!session\.IsAccountSession/.test(stackers) && /!session\.IsAccountSession/.test(state), "Legacy competition-password sessions must remain compatible during migration.");

assert(/CanManageCompetition[\s\S]*CompetitionManager/.test(permissions), "Competition managers must retain management capability.");
assert(!/CanManageCompetition[\s\S]{0,180}DataEntry/.test(permissions), "Data Entry must not receive competition management capability.");
assert(!/CanManageCompetition[\s\S]{0,180}Viewer/.test(permissions), "Viewer must not receive competition management capability.");
assert(/CanViewCompetition[\s\S]*DataEntry[\s\S]*Viewer/.test(permissions), "Data Entry and Viewer must retain read access.");

console.log("Core authorization hardening static tests passed.");
