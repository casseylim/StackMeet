const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const controller = read('backend/StackMeet.Api/Controllers/CompetitionsController.cs');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const dto = read('backend/StackMeet.Api/Dtos/CompetitionDtos.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const migrationsDir = path.join(root, 'backend/StackMeet.Api/Migrations');
const migrations = fs.readdirSync(migrationsDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(migrationsDir, name), 'utf8'))
  .join('\n');

assert.ok(controller.includes('using StackMeet.Api.Activities;'), 'competition controller must import the activity resolver namespace');
assert.ok(
  controller.includes('CompetitionsController(StackMeetDbContext database, CompetitionActivityResolver activityResolver)'),
  'competition controller must receive the compatibility resolver through DI'
);
assert.strictEqual(
  (controller.match(/activityResolver\.Resolve\(/g) || []).length,
  1,
  'Phase 3C must have exactly one runtime resolver consumption in CompetitionsController'
);

const itemIndex = controller.indexOf('var item = await query.SingleOrDefaultAsync(ct);');
const notFoundIndex = controller.indexOf('if (item is null) return NotFound();', itemIndex);
const resolveIndex = controller.indexOf('_ = activityResolver.Resolve(item);', notFoundIndex);
const mapIndex = controller.indexOf('return Ok(Map(item));', resolveIndex);
const postIndex = controller.indexOf('[HttpPost]');
assert.ok(itemIndex >= 0 && notFoundIndex > itemIndex && resolveIndex > notFoundIndex && mapIndex > resolveIndex, 'single-competition GET must resolve activity after materialization and before projection');
assert.ok(postIndex > mapIndex, 'resolver consumption must remain inside the read-only GET path and before all write actions');

const expectedMap = 'static CompetitionResponse Map(Competition x)=>new(x.Id,x.CompetitionCode,x.CompetitionName,x.Venue,x.StartDate,x.EndDate,x.Status,x.IsPubliclyListed,x.CreatedAt,x.UpdatedAt);';
assert.ok(controller.includes(expectedMap), 'existing Sport Stacking competition response mapping must remain byte-for-byte equivalent');
const expectedDto = 'public sealed record CompetitionResponse(int Id, string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, bool IsPubliclyListed, DateTime CreatedAt, DateTime UpdatedAt);';
assert.ok(dto.includes(expectedDto), 'competition response contract must remain unchanged in Phase 3C');

assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'Phase 3C must not route SQL-authoritative results through the resolver');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'Phase 3C must not route legacy competition state through the resolver');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3C must not add a persisted activity field');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3C must not add an activity migration');

console.log('Modular Platform Foundation v1 Phase 3C bounded read projection guards passed.');
