"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const validator = read("backend/StackMeet.Api/Services/CompetitionResultValidator.cs");
const controller = read("backend/StackMeet.Api/Controllers/CompetitionResultsController.cs");
const migrations = fs.readdirSync(path.join(root, "backend/StackMeet.Api/Migrations"));

assert.match(validator, /public const int MaxBatchChanges = 1000;/);
assert.match(validator, /public const int MaxAttemptsPerResult = 3;/);
assert.match(validator, /Result stage must be Prelims or Finals/);
assert.match(validator, /"PRELIMS" => "Prelims"/);
assert.match(validator, /"FINALS" => "Finals"/);
assert.match(validator, /"INDIVIDUAL" => "Individual"/);
assert.match(validator, /"DOUBLES" => "Doubles"/);
assert.match(validator, /"TIMED RELAY" => "Timed Relay"/);
assert.match(validator, /"RELAY" => "Timed Relay"/);
assert.match(validator, /"3-3-3" => "3-3-3"/);
assert.match(validator, /"3-6-3" => "3-6-3"/);
assert.match(validator, /"CYCLE" => "Cycle"/);
assert.match(validator, /MaxParticipantLength = 50/);
assert.match(validator, /value <= 0 \|\| value > MaxAttemptSeconds/);
assert.match(validator, /HasAtMostThreeDecimalPlaces\(value\)/);
assert.match(validator, /item\.Penalty < 0 \|\| item\.Penalty > MaxPenalty/);
assert.match(validator, /logicalKeys\.Distinct\(StringComparer\.Ordinal\)/);
assert.match(validator, /Where\(item => item\.Type == "Individual"\)/);
assert.match(validator, /database\.Stackers/);
assert.match(validator, /TeamIds\(json, "doubles"\)/);
assert.match(validator, /TeamIds\(json, "relays"\)/);
assert.match(validator, /ValidateUpsertParticipants/);
assert.match(validator, /Deletes remain available for cleaning up/);

assert.match(controller, /CompetitionResultValidator\.TryNormalize/);
assert.match(controller, /CompetitionResultValidator\.ValidateUpsertParticipants/);
assert.doesNotMatch(controller, /static bool Valid\(/);
assert.match(controller, /IsUniqueConstraintViolation/);
assert.match(controller, /sql\.Number is 2601 or 2627/);
assert.match(controller, /Refresh the latest results and retry/);
assert.match(controller, /Stage = item\.Stage,/);
assert.match(controller, /ParticipantType = item\.Type,/);
assert.match(controller, /EventCode = item\.Event,/);

assert.equal(
  migrations.filter(name => /CoreIntegrity|ResultValidation|PhaseC/i.test(name)).length,
  0,
  "Phase C must not introduce an EF schema migration."
);

console.log("Core integrity V2 Phase C static tests passed.");
