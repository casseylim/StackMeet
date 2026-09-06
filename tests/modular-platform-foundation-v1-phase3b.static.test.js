const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const resolver = read('backend/StackMeet.Api/Activities/CompetitionActivityResolver.cs');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');
const competition = read('backend/StackMeet.Api/Models/Competition.cs');
const controllersDir = path.join(root, 'backend/StackMeet.Api/Controllers');
const controllerFiles = fs.readdirSync(controllersDir)
  .filter(name => name.endsWith('.cs'));
const controllers = controllerFiles
  .map(name => fs.readFileSync(path.join(controllersDir, name), 'utf8'))
  .join('\n');
const resolverConsumers = controllerFiles.filter(name =>
  fs.readFileSync(path.join(controllersDir, name), 'utf8').includes('CompetitionActivityResolver'));
const migrationsDir = path.join(root, 'backend/StackMeet.Api/Migrations');
const migrations = fs.readdirSync(migrationsDir)
  .filter(name => name.endsWith('.cs'))
  .map(name => fs.readFileSync(path.join(migrationsDir, name), 'utf8'))
  .join('\n');

assert.ok(resolver.includes('public sealed class CompetitionActivityResolver'), 'compatibility resolver must exist');
assert.ok(resolver.includes('ActivityModuleRegistry _registry'), 'resolver must depend on the activity registry');
assert.ok(resolver.includes('Resolve(Competition competition)'), 'resolver must accept the shared Competition model');
assert.ok(resolver.includes('_registry.Resolve(competition.ActivityModuleCode)'), 'later schema activation must preserve registry-owned compatibility semantics');
assert.ok(registration.includes('AddSingleton<CompetitionActivityResolver>()'), 'compatibility resolver must be registered with DI');

for (const forbiddenRule of ['3-3-3', '3-6-3', 'Cycle', 'WssaId', 'SpecialStacker', 'Child/Parent', 'Timed Relay']) {
  assert.ok(!resolver.includes(forbiddenRule), `compatibility resolver must not contain Sport Stacking rule token: ${forbiddenRule}`);
}

assert.deepStrictEqual(resolverConsumers, ['CompetitionsController.cs'], 'resolver consumption must remain limited to the bounded competition read seams');
assert.ok(competition.includes('public string? ActivityModuleCode { get; set; }'), 'later schema activation must keep the selector nullable for compatibility');
assert.ok(!competition.includes('ActivityCode'), 'no competing activity selector field may be introduced');
assert.ok(migrations.includes('name: "ActivityModuleCode"'), 'later schema activation must use the shared activity selector column');
assert.ok(!migrations.includes('name: "ActivityCode"'), 'no competing activity selector migration may be introduced');

console.log('Modular Platform Foundation v1 Phase 3B compatibility resolver guards passed.');
