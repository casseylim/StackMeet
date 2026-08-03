const test = require('node:test');
const assert = require('node:assert/strict');
const packageApi = require('../backend/StackMeet.Api/wwwroot/js/CompetitionPackage.js');
const assembler = require('../backend/StackMeet.Api/wwwroot/js/CompetitionPackageAssembler.js');

const state = {
  settings: { name: 'Test Competition' }, events: { Individuals: ['Cycle'] },
  divisionSettings: { special: [12], doubles: [12], timedRelay: [12] },
  divisions: ['SS 12 & Under L1 M', 'SS 12 & Under L1 F', 'SS 12U', '12U'],
  stackers: [{ id: '1.1', division: 'SS 12 & Under L1 M' }],
  doubles: [{ id: '2.1', division: 'SS 12U' }], relays: [{ id: '3.1', division: '12U' }],
  results: [{ participant: '1.1', event: 'Cycle', attempts: [6.5] }],
  finalQualificationSnapshots: [], finals: [], awards: { individualPlaces: 3 }
};

test('creates a versioned package without mutating source state', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST', sourceMode: 'offline' }, '2026-08-03T00:00:00.000Z');
  assert.equal(pkg.manifest.packageFormat, packageApi.FORMAT);
  assert.equal(pkg.manifest.packageVersion, 1);
  assert.equal(pkg.manifest.competitionKey, 'TEST');
  assert.equal(pkg.manifest.sourceMode, 'offline');
  assert.equal(pkg.metadata.stackerCount, 1);
  pkg.participants.stackers[0].division = 'Changed';
  assert.equal(state.stackers[0].division, 'SS 12 & Under L1 M');
});

test('validates complete package sections and counts', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST' });
  assert.deepEqual(packageApi.validatePackage(pkg), []);
  assert.doesNotThrow(() => packageApi.assertPackage(pkg, 'test'));
});

test('rejects packages for another competition', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST' });
  assert.throws(() => packageApi.assertPackage(pkg, 'OTHER'), /does not match destination/);
});

test('rejects inconsistent package counts', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST' });
  pkg.metadata.resultCount = 99;
  assert.match(packageApi.validatePackage(pkg).join(' '), /Result count does not match/);
});

test('creates and verifies a deterministic content hash', async () => {
  const pkg = await packageApi.withContentHash(packageApi.createPackage(state, { competitionKey: 'TEST' }));
  assert.match(pkg.manifest.contentHash, /^[a-f0-9]{64}$/);
  assert.equal(await packageApi.verifyContentHash(pkg), true);
  pkg.participants.stackers[0].division = 'Tampered';
  assert.equal(await packageApi.verifyContentHash(pkg), false);
});

test('keeps Doubles and Relay divisions gender-neutral', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST' });
  pkg.participants.doubles[0].division = 'SS 12U M';
  assert.match(packageApi.validatePackage(pkg).join(' '), /invalid gendered division/);
  pkg.participants.doubles[0].division = 'SS 12U';
  pkg.participants.relays[0].division = '12U F';
  assert.match(packageApi.validatePackage(pkg).join(' '), /invalid gendered division/);
});

test('converts a valid package back to application state with teams intact', () => {
  const pkg = packageApi.createPackage(state, { competitionKey: 'TEST' });
  const restored = packageApi.packageToState(pkg);
  assert.equal(restored.stackers[0].division, 'SS 12 & Under L1 M');
  assert.equal(restored.doubles[0].division, 'SS 12U');
  assert.equal(restored.relays[0].division, '12U');
  assert.equal(restored.results[0].participant, '1.1');
});

test('uses authoritative SQL stackers when browser state is incomplete', () => {
  const authoritativeStackers = Array.from({ length: 104 }, (_, index) => ({ id: `1.${index + 1}`, name: `SQL ${index + 1}` }));
  const pkg = packageApi.createPackage({ ...state, stackers: [] }, { competitionKey: 'TEST' }, undefined, { stackers: authoritativeStackers });
  assert.equal(pkg.metadata.stackerCount, 104);
  assert.equal(pkg.participants.doubles.length, 1);
  assert.equal(pkg.participants.relays.length, 1);
});

test('online assembler merges SQL stackers with state-backed Doubles and Relay', async () => {
  const calls = [];
  const pkg = await assembler.downloadOnlinePackage({
    competitionKey: 'TEST', competitionId: 18, exportedAt: '2026-08-03T00:00:00.000Z',
    loadState: async key => { calls.push(`state:${key}`); return { settings: { name: 'Test' }, doubles: [{ id: 2, division: 'Doubles 8U' }], relays: [{ id: 3, timedRelayDivision: 'Relay 10U' }], results: [{ id: 9 }] }; },
    listStackers: async id => { calls.push(`stackers:${id}`); return [{ id: 1, gender: 'Male' }, { id: 2, gender: 'Female' }]; }
  });
  assert.deepEqual(calls.sort(), ['stackers:18', 'state:TEST']);
  assert.equal(pkg.manifest.competitionKey, 'TEST');
  assert.equal(pkg.participants.stackers.length, 2);
  assert.equal(pkg.participants.doubles.length, 1);
  assert.equal(pkg.participants.relays.length, 1);
  assert.equal(pkg.competitionData.results.length, 1);
  assert.equal(await packageApi.verifyContentHash(pkg), true);
});
