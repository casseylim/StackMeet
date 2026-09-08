const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const policy = read('backend/StackMeet.Api/Activities/ActivityAssignmentPolicy.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const controller = read('backend/StackMeet.Api/Controllers/CompetitionsController.cs');
const activityDto = read('backend/StackMeet.Api/Dtos/CompetitionActivityDtos.cs');
const competitionDto = read('backend/StackMeet.Api/Dtos/CompetitionDtos.cs');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const app = read('backend/StackMeet.Api/wwwroot/app.js');
const migrations = fs.readdirSync(path.join(root, 'backend/StackMeet.Api/Migrations'))
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(root, 'backend/StackMeet.Api/Migrations', name), 'utf8'));

assert.ok(policy.includes('public sealed class ActivityAssignmentPolicy(ActivityModuleRegistry registry)'), 'assignment decisions must be registry-backed');
assert.ok(policy.includes('ActivityAssignmentFailure.MissingModuleCode'), 'blank module codes must fail closed');
assert.ok(policy.includes('ActivityAssignmentFailure.UnknownModuleCode'), 'unknown module codes must fail closed');
assert.ok(policy.includes('registry.TryResolve(requestedModuleCode'), 'requested module must be registry-validated');
assert.ok(policy.includes('registry.Resolve(competition.ActivityModuleCode)'), 'policy must compare effective current module');
assert.ok(/changesEffectiveModule && hasDurableActivityData/.test(policy), 'effective module changes must be blocked after durable activity data exists');
assert.ok(!/DbContext|EntityFrameworkCore|SqlServer|CompetitionState|CompetitionResult|Stacker/.test(policy), 'assignment policy must remain persistence-neutral');
for (const token of ['3-3-3','3-6-3','Cycle','WssaId','SpecialStacker','Doubles','Relay','Prelims','Finals']) assert.ok(!policy.includes(token), `policy must remain activity-neutral: ${token}`);

assert.ok(registration.includes('AddSingleton<ActivityAssignmentPolicy>()'), 'assignment policy must be DI-registered');
assert.ok(activityDto.includes('CompetitionActivityAssignmentRequest(string? ActivityModuleCode)'), 'assignment must use a dedicated activity DTO');
assert.ok(!competitionDto.includes('ActivityModuleCode'), 'generic competition CRUD DTOs must remain selector-neutral');

const putStart = controller.indexOf('[HttpPut("{id:int}/activity")]');
const postStart = controller.indexOf('[HttpPost]', putStart);
assert.ok(putStart >= 0 && postStart > putStart, 'assignment must be bounded to PUT /{id}/activity');
const assignment = controller.slice(putStart, postStart);
assert.ok(assignment.includes('[FromServices] ActivityAssignmentPolicy activityAssignmentPolicy'), 'policy consumption must be local to the write seam');
assert.ok(assignment.indexOf('if (!IsMaintenanceRequest())') < assignment.indexOf('BeginTransactionAsync'), 'maintenance authorization must precede transaction work');
assert.ok(assignment.includes('IsolationLevel.Serializable'), 'assignment must use serializable isolation');
assert.ok(assignment.includes('WITH (UPDLOCK, HOLDLOCK)'), 'competition row must be locked before assignment evaluation');
assert.ok(assignment.includes('database.Stackers.AnyAsync(x => x.CompetitionId == id, ct)'), 'participants must lock module switching');
assert.ok(assignment.includes('database.CompetitionResults.AnyAsync(x => x.CompetitionId == id, ct)'), 'results must lock module switching');
assert.ok(assignment.includes('database.CompetitionStates.AnyAsync(x => x.CompetitionKey == item.CompetitionKey, ct)'), 'any saved activity state must lock module switching');
assert.ok(!assignment.includes('CompetitionAssets.AnyAsync') && !assignment.includes('CompetitionUsers.AnyAsync'), 'shared-core assets/access must not be classified as activity data');
assert.ok(assignment.includes('activityAssignmentPolicy.Evaluate(item, request.ActivityModuleCode, hasDurableActivityData)'), 'controller must delegate policy');
assert.ok(assignment.includes('ActivityAssignmentFailure.DurableActivityData') && assignment.includes('return Conflict('), 'durable data rejection must return conflict');
assert.ok(assignment.includes('return BadRequest('), 'invalid module codes must return bad request');
assert.strictEqual((controller.match(/item\.ActivityModuleCode = decision\.Module!\.Code;/g) || []).length, 1, 'selector write must exist exactly once');
assert.ok(assignment.includes('await transaction.CommitAsync(ct);'), 'selector write must commit transactionally');
assert.ok(assignment.includes('return Ok(MapActivity(decision.Module'), 'successful assignment must return the generic activity descriptor');

assert.ok(!resultsController.includes('ActivityAssignmentPolicy') && !stateController.includes('ActivityAssignmentPolicy'), 'results/state controllers must remain outside assignment policy');
assert.ok(!app.includes('ActivityModuleCode'), 'Sport Stacking frontend must remain unaware of assignment mechanics');
assert.strictEqual(migrations.filter(content => content.includes('name: "ActivityModuleCode"')).length, 1, 'Phase 4B must add no new selector migration');
assert.ok(registration.includes('AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'Sport Stacking compatibility registration must remain present as later phases add modules');

console.log('Modular Platform Foundation v1 Phase 4B activity assignment policy guards passed.');