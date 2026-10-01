'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root,
  'backend/StackMeet.Api/Migrations/20260914093000_FinalsRankingGovernanceSp4g.cs'), 'utf8');
assert.match(source, /\[Migration\("20260914093000_FinalsRankingGovernanceSp4g"\)\]/);
const operations = [...source.matchAll(/migrationBuilder\.Sql\(@"([\s\S]*?)"\);/g)]
  .map(match => match[1].trim());
assert.equal(operations.filter(sql => /^CREATE\s+TRIGGER\b/i.test(sql)).length, 0,
  'CREATE TRIGGER must not be a direct operation nested by EF idempotent guards');
const triggerOperations = operations.filter(sql => /CREATE\s+TRIGGER\b/i.test(sql));
assert.equal(triggerOperations.length, 1);
const operation = triggerOperations[0];
assert.match(operation, /^EXEC\(N'[\s\S]*'\);$/);
// Decode the actual dynamic batch, including escaped SQL literals.
const triggerSql = operation.slice("EXEC(N'".length, -3).replace(/''/g, "'").trim();
assert.match(triggerSql, /^CREATE TRIGGER \[dbo\]\.\[TR_FinalsRankingGovernance_ImmutableSnapshot\]/);
assert.match(triggerSql, /ON \[dbo\]\.\[FinalsRankingGovernance\]/);
assert.match(triggerSql, /AFTER UPDATE, DELETE/);
assert.match(triggerSql, /IF EXISTS \(SELECT 1 FROM deleted WHERE \[SnapshotCapturedAt\] IS NOT NULL\)/);
assert.match(triggerSql, /THROW 51041, 'Captured Finals ranking governance records are immutable\.', 1;/);
assert.match(triggerSql, /SET NOCOUNT ON;/);
const validator = fs.readFileSync(path.join(root,
  'scripts/deployment/Test-ReleaseV2MigrationPackage.ps1'), 'utf8');
assert.match(validator, /\[System\.Security\.Cryptography\.SHA256\]::Create\(\)/);
assert.match(validator, /4101718F6B550480A9CA579015B8143EDAE23FF78F7C4165CF17F755432364BA'[\s\S]*?throw 'Blocked Release v2 package/);
console.log('Release v2 idempotent trigger syntax regression passed (offline source inspection).');
