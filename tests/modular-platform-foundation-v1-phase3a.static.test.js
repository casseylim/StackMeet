const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const program = read('backend/StackMeet.Api/Program.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const controllersDir = path.join(root, 'backend/StackMeet.Api/Controllers');
const controllerFiles = fs.readdirSync(controllersDir).filter(name => name.endsWith('.cs'));
const controllersByName = Object.fromEntries(
  controllerFiles.map(name => [name, fs.readFileSync(path.join(controllersDir, name), 'utf8')])
);
const controllers = Object.values(controllersByName).join('\n');
const competitionController = controllersByName['CompetitionsController.cs'] || '';
const otherControllers = Object.entries(controllersByName)
  .filter(([name]) => name !== 'CompetitionsController.cs')
  .map(([, content]) => content)
  .join('\n');
const migrationsDir = path.join(root, 'backend/StackMeet.Api/Migrations');
const migrations = fs.readdirSync(migrationsDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(migrationsDir, name), 'utf8'))
  .join('\n');

assert.ok(program.includes('using StackMeet.Api.Activities;'), 'Program must import the activity module registration seam');
const registrations = program.match(/builder\.Services\.AddNadiTrackActivityModules\(\);/g) || [];
assert.strictEqual(registrations.length, 1, 'activity modules must be registered exactly once');
assert.ok(
  program.indexOf('builder.Services.AddNadiTrackActivityModules();') < program.indexOf('var app = builder.Build();'),
  'activity modules must be registered before the service provider is built'
);

assert.ok(registration.includes('AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'Sport Stacking must remain the only built-in module');
assert.ok(registration.includes('AddSingleton<ActivityModuleRegistry>()'), 'registry must remain registered');

for (const forbiddenConsumer of ['ActivityModuleRegistry', 'SportStackingActivityModule']) {
  assert.ok(!controllers.includes(forbiddenConsumer), `controllers must not directly consume module implementation services: ${forbiddenConsumer}`);
}
assert.ok(!otherControllers.includes('IActivityModule'), 'only the bounded competition activity descriptor seam may consume the generic module contract');
assert.strictEqual(
  (competitionController.match(/IActivityModule/g) || []).length,
  1,
  'CompetitionsController may reference IActivityModule exactly once for the bounded Phase 3D descriptor mapper'
);
assert.ok(
  competitionController.includes('static CompetitionActivityResponse MapActivity(IActivityModule module)'),
  'the permitted generic module contract reference must remain confined to the Phase 3D activity descriptor mapper'
);

assert.ok(!program.includes('GetRequiredService<ActivityModuleRegistry>'), 'Program must not resolve the registry at runtime yet');
assert.ok(!program.includes('.Resolve('), 'Program must not select an activity module yet');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3A must not add an activity column to Competition');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3A must not add a database migration for activity selection');

console.log('Modular Platform Foundation v1 Phase 3A activation guards passed.');
