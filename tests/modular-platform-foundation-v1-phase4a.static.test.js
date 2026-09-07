const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const resolver = read('backend/StackMeet.Api/Activities/CompetitionActivityResolver.cs');
const registry = read('backend/StackMeet.Api/Activities/ActivityModuleRegistry.cs');
const dto = read('backend/StackMeet.Api/Dtos/CompetitionDtos.cs');
const controller = read('backend/StackMeet.Api/Controllers/CompetitionsController.cs');
const resultsController = read('backend/StackMeet.Api/Controllers/CompetitionResultsController.cs');
const stateController = read('backend/StackMeet.Api/Controllers/CompetitionStateController.cs');
const migration = read('backend/StackMeet.Api/Migrations/20260906033000_AddCompetitionActivityModuleCode.cs');
const snapshot = read('backend/StackMeet.Api/Migrations/StackMeetDbContextModelSnapshot.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const app = read('backend/StackMeet.Api/wwwroot/app.js');

assert.strictEqual((competition.match(/public string\? ActivityModuleCode \{ get; set; \}/g) || []).length, 1, 'Phase 4A must add exactly one nullable shared activity selector');
assert.ok(/\[MaxLength\(100\)\]\s*public string\? ActivityModuleCode/.test(competition), 'activity selector must be length-bounded to 100 characters');
assert.ok(!competition.includes('ActivityCode'), 'Phase 4A must not introduce a competing activity selector');

assert.ok(resolver.includes('return _registry.Resolve(competition.ActivityModuleCode);'), 'resolver must consume only the persisted shared selector');
assert.ok(registry.includes('string.IsNullOrWhiteSpace(moduleCode)'), 'registry must preserve null/blank compatibility resolution');
assert.ok(registry.includes('? CompatibilityDefaultCode'), 'null/blank selector must still use the compatibility default');
assert.ok(registry.includes('Unknown activity module'), 'unknown explicit selectors must continue to fail closed');

assert.ok(migration.includes('[Migration("20260906033000_AddCompetitionActivityModuleCode")]'), 'Phase 4A migration must have a stable migration id');
const upStart = migration.indexOf('protected override void Up');
const downStart = migration.indexOf('protected override void Down');
assert.ok(upStart >= 0 && downStart > upStart, 'migration must define bounded Up and Down operations');
const up = migration.slice(upStart, downStart);
assert.ok(up.includes('migrationBuilder.AddColumn<string>('), 'migration Up must add one string selector column');
assert.ok(up.includes('name: "ActivityModuleCode"'), 'migration must target the shared selector column');
assert.ok(up.includes('table: "Competition"'), 'selector must live on Competition');
assert.ok(up.includes('type: "nvarchar(100)"') && up.includes('maxLength: 100'), 'selector database type must be bounded');
assert.ok(up.includes('nullable: true'), 'existing competitions must remain null after migration');
assert.ok(!up.includes('defaultValue') && !up.includes('UpdateData') && !up.includes('.Sql('), 'Phase 4A must not backfill or default existing competitions');
assert.ok(migration.slice(downStart).includes('migrationBuilder.DropColumn('), 'migration Down must be reversible');

const snapshotProperty = snapshot.match(/b\.Property<string>\("ActivityModuleCode"\)[\s\S]{0,140}?HasColumnType\("nvarchar\(100\)"\);/);
assert.ok(snapshotProperty, 'EF model snapshot must include the nullable bounded selector');
assert.ok(!snapshotProperty[0].includes('.IsRequired()'), 'EF snapshot selector must remain nullable');

assert.ok(!dto.includes('ActivityModuleCode') && !dto.includes('ActivityCode'), 'existing CompetitionRequest/CompetitionResponse contract must remain selector-neutral');
const activityPutStart = controller.indexOf('[HttpPut("{id:int}/activity")]');
const legacyAdminStart = controller.indexOf('[HttpPost]', activityPutStart);
assert.ok(activityPutStart >= 0 && legacyAdminStart > activityPutStart, 'later phases may add only a bounded activity-specific write seam before existing admin actions');
assert.strictEqual((controller.match(/item\.ActivityModuleCode\s*=/g) || []).length, 1, 'the persisted selector may be assigned in exactly one bounded write seam');
assert.ok(!controller.slice(legacyAdminStart).includes('ActivityModuleCode'), 'existing competition POST/PUT/DELETE admin actions must remain unable to mutate the selector');
assert.ok(!resultsController.includes('CompetitionActivityResolver'), 'SQL-authoritative results must remain outside module routing');
assert.ok(!stateController.includes('CompetitionActivityResolver'), 'legacy CompetitionState must remain outside module routing');
assert.ok(!app.includes('ActivityModuleCode') && !app.includes('ActivityCode'), 'Sport Stacking application monolith must remain unaware of persisted selection');
assert.ok(!/ChessActivityModule|SwimmingActivityModule|AthleticsActivityModule/.test(registration), 'Phase 4A must not add a second activity module');

console.log('Modular Platform Foundation v1 Phase 4A persisted activity selector guards passed.');
