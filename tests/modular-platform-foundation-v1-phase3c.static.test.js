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
assert.ok(
  controller.includes('_ = activityResolver.Resolve(item);'),
  'Phase 3C single-competition GET resolver seam must remain active'
);

const itemIndex = controller.indexOf('var item = await query.SingleOrDefaultAsync(ct);');
const notFoundIndex = controller.indexOf('if (item is null) return NotFound();', itemIndex);
const resolveIndex = controller.indexOf('_ = activityResolver.Resolve(item);', notFoundIndex);
const mapIndex = controller.indexOf('return Ok(Map(item));', resolveIndex);
const activityRouteIndex = controller.indexOf('[HttpGet("{id:int}/activity")]');
assert.ok(itemIndex >= 0 && notFoundIndex > itemIndex && resolveIndex > notFoundIndex && mapIndex > resolveIndex, 'single-competition GET must resolve activity after materialization and before projection');
assert.ok(activityRouteIndex > mapIndex, 'later read-only activity seams may follow the original Phase 3C GET without changing it');

const expectedMap = 'static CompetitionResponse Map(Competition x)=>new(x.Id,x.CompetitionCode,x.CompetitionName,x.Venue,x.StartDate,x.EndDate,x.Status,x.IsPubliclyListed,x.CreatedAt,x.UpdatedAt);';
assert.ok(controller.includes(expectedMap), 'existing Sport Stacking competition response mapping must remain byte-for-byte equivalent');
const expectedDto = 'public sealed record CompetitionResponse(int Id, string CompetitionCode, string CompetitionName, string Venue, DateOnly StartDate, DateOnly EndDate, string Status, bool IsPubliclyListed, DateTime CreatedAt, DateTime UpdatedAt);';
assert.ok(dto.includes(expectedDto), 'competition response contract must remain unchanged after Phase 3C');

assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must not route through the resolver');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy competition state must not route through the resolver');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'no persisted activity field may be added');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'no activity migration may be added');

console.log('Modular Platform Foundation v1 Phase 3C bounded read projection guards passed.');
