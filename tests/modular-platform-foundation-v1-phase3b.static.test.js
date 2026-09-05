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
assert.ok(resolver.includes('_registry.Resolve(moduleCode: null)'), 'existing competitions must resolve through the compatibility default');
assert.ok(registration.includes('AddSingleton<CompetitionActivityResolver>()'), 'compatibility resolver must be registered with DI');

for (const forbiddenRule of ['3-3-3', '3-6-3', 'Cycle', 'WssaId', 'SpecialStacker', 'Child/Parent', 'Timed Relay']) {
  assert.ok(!resolver.includes(forbiddenRule), `compatibility resolver must not contain Sport Stacking rule token: ${forbiddenRule}`);
}

assert.deepStrictEqual(resolverConsumers, ['CompetitionsController.cs'], 'resolver consumption must remain limited to the bounded Phase 3C competition detail read seam');
assert.ok(!competition.includes('ActivityModuleCode') && !competition.includes('ActivityCode'), 'compatibility phase must not add a persisted activity field');
assert.ok(!migrations.includes('ActivityModuleCode') && !migrations.includes('ActivityCode'), 'compatibility phase must not add an activity migration');

console.log('Modular Platform Foundation v1 Phase 3B compatibility resolver guards passed.');
