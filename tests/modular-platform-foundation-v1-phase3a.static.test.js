const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const program = read('backend/StackMeet.Api/Program.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const controllersDir = path.join(root, 'backend/StackMeet.Api/Controllers');
const controllers = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(controllersDir, name), 'utf8'))
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

for (const forbiddenConsumer of ['ActivityModuleRegistry', 'IActivityModule', 'SportStackingActivityModule']) {
  assert.ok(!controllers.includes(forbiddenConsumer), `Phase 3A controllers must not consume module services yet: ${forbiddenConsumer}`);
}

assert.ok(!program.includes('GetRequiredService<ActivityModuleRegistry>'), 'Program must not resolve the registry at runtime yet');
assert.ok(!program.includes('.Resolve('), 'Program must not select an activity module yet');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'Phase 3A must not add an activity column to Competition');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'Phase 3A must not add a database migration for activity selection');

console.log('Modular Platform Foundation v1 Phase 3A activation guards passed.');
