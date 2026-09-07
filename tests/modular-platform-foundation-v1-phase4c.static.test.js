const fs = require('fs');
const path = require('path');
const assert = require('assert');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const runtime = read('tests/Phase4CActivityAssignmentIntegrationTests/Program.cs');
const csproj = read('tests/Phase4CActivityAssignmentIntegrationTests/Phase4CActivityAssignmentIntegrationTests.csproj');
const workflow = read('.github/workflows/ci.yml');
const registration = read('backend/StackMeet.Api/Activities/ActivityModuleRegistration.cs');

function readSourceTree(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    if (entry.name === 'bin' || entry.name === 'obj') return [];
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return [readSourceTree(full)];
    if (!/\.(cs|js|json|csproj|html|css)$/i.test(entry.name)) return [];
    return [fs.readFileSync(full, 'utf8')];
  }).join('\n');
}
const backend = readSourceTree(path.join(root, 'backend/StackMeet.Api'));

assert.ok(csproj.includes('Microsoft.AspNetCore.Mvc.Testing'), 'Phase 4C must use the standard ASP.NET in-process test host');
assert.ok(runtime.includes('await Phase4CActivityAssignmentRuntimeAssertions.RunAsync();'), 'Phase 4C runtime suite must have an explicit executable entrypoint');
assert.ok(!runtime.includes('ModuleInitializer'), 'Phase 4C must not rely on module-initializer execution');
assert.ok(runtime.includes('WebApplicationFactory<CompetitionActivityResolver>'), 'test host must target the real API assembly without modifying production Program');
assert.ok(runtime.includes('services.RemoveAll<IActivityModule>()'), 'test host must isolate module registrations inside the test service provider');
assert.ok(runtime.includes('services.AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'test registry must preserve the compatibility module');
assert.ok(runtime.includes('services.AddSingleton<IActivityModule, Phase4CTestActivityModule>()'), 'test-only second module must exist only in integration DI');
assert.ok(runtime.includes('public string Code => "test-activity";'), 'test-only module code must be explicit');
assert.ok(workflow.includes('Run Phase 4C activity assignment integration tests'), 'required CI must execute the Phase 4C runtime project');
assert.ok(workflow.includes('dotnet restore tests/Phase4CActivityAssignmentIntegrationTests/Phase4CActivityAssignmentIntegrationTests.csproj'), 'required CI must restore the isolated Phase 4C test project');

for (const token of [
  'HttpStatusCode.Unauthorized',
  'HttpStatusCode.Forbidden',
  'HttpStatusCode.BadRequest',
  'HttpStatusCode.OK',
  'HttpStatusCode.Conflict',
  'CompetitionState',
  'Stacker',
  'CompetitionResult',
  'same module remains idempotent after saved state',
  'saved state blocks valid module switch',
  'participant data blocks valid module switch',
  'result data blocks valid module switch',
  'resolver observes persisted assignment'
]) assert.ok(runtime.includes(token), `Phase 4C runtime matrix missing proof token: ${token}`);

assert.ok(runtime.includes('StackMeet_Phase4C_'), 'Phase 4C database names must use a dedicated safety prefix');
assert.ok(runtime.includes('InitialCatalog = "master"'), 'Phase 4C cleanup must reconnect through master');
assert.ok(runtime.includes('SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE'), 'Phase 4C must clean up only its isolated LocalDB database');
assert.ok(!backend.includes('test-activity'), 'test-only activity module must never appear in backend production source');
assert.ok(!registration.includes('Phase4CTestActivityModule'), 'production registration must remain unaware of the test-only module');
assert.ok(registration.includes('AddSingleton<IActivityModule, SportStackingActivityModule>()'), 'Sport Stacking must remain the only production module during Phase 4C');

console.log('Modular Platform Foundation v1 Phase 4C runtime characterization guards passed.');
