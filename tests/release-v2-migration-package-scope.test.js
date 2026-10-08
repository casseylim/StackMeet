'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'docs/deployment/production-release-v2-manifest.json'), 'utf8'));
const earlier = [
  '20260711145521_InitialCreate',
  '20260711174057_AddCompetitionAndStacker',
  '20260711175415_AddStackerRegistrationFields',
  '20260713034108_CompetitionAdminPhase1',
  '20260726104757_AccountAccessPhase1',
  '20260726112602_AccountEmailPhase2',
  '20260731035059_AddPublicCompetitionListing',
  '20260731054304_AddAccountLoginLockout',
  '20260817043721_CompetitionResultsPhase1',
  '20260817060000_CompetitionAssetsPhase1',
  '20260825093138_CompetitionStateOptimisticConcurrency',
  '20260826074651_AddAccountSessionVersion'
];
// Synthetic SQL is inspected as text by the actual production-package validator; never executed.
const block = id => `IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'${id}')
BEGIN
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'${id}', N'8.0.8');
END;
GO\n`;
const valid = manifest.databaseMigrations.map(block).join('\n');
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'stackmeet-release-scope-'));
const sqlPath = path.join(temp, 'package.sql');
const manifestPath = path.join(temp, 'manifest.json');
const shell = process.platform === 'win32' ? 'powershell.exe' : 'pwsh';
function validate(sql, expectedSuccess, description, configuration = manifest) {
  fs.writeFileSync(sqlPath, sql);
  fs.writeFileSync(manifestPath, JSON.stringify(configuration));
  const result = spawnSync(shell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
    path.join(root, 'scripts/deployment/Test-ReleaseV2MigrationPackage.ps1'),
    // Bound Windows runner cold-start/execution delays without changing validation assertions.
    '-SqlPath', sqlPath, '-ManifestPath', manifestPath], { encoding: 'utf8', timeout: 120000 });
  assert.ifError(result.error);
  assert.equal(result.status === 0, expectedSuccess, `${description}: ${result.stdout}\n${result.stderr}`);
  if (expectedSuccess) assert.match(result.stdout, /MIGRATION_PACKAGE_SCOPE=PASS/);
}
try {
  validate(valid, true, 'accept exactly three guarded release migrations');
  validate(earlier.map(block).join('\n') + valid, false, 'reject the former 15-migration full chain');
  for (const id of earlier) validate(valid + block(id), false, `reject earlier migration ${id}`);
  for (const id of manifest.databaseMigrations) {
    validate(manifest.databaseMigrations.filter(other => other !== id).map(block).join('\n'), false, `reject missing ${id}`);
    validate(valid.replace(`WHERE [MigrationId] = N'${id}'`, 'WHERE 1 = 1'), false, `reject unguarded ${id}`);
  }
  validate(valid + block('20261001000000_UnreviewedMigration'), false, 'reject unexpected later migration');
  validate(valid, false, 'reject migration zero boundary', { ...manifest, databaseMigrationFrom: '0' });
  validate(valid, false, 'reject moved upper boundary', { ...manifest, databaseMigrationTo: '20261001000000_UnreviewedMigration' });
  validate(valid, false, 'reject changed governed migration list', { ...manifest, databaseMigrations: manifest.databaseMigrations.slice(1) });
  console.log('Release v2 migration-package scope tests passed (text inspection only).');
} finally {
  for (const file of [sqlPath, manifestPath]) if (fs.existsSync(file)) fs.unlinkSync(file);
  fs.rmdirSync(temp);
}
