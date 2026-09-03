const assert = require('assert');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const auditServicePath = path.join(root, 'backend', 'StackMeet.Api', 'Services', 'DailyAuditReportService.cs');
const publishProfilePath = path.join(root, 'backend', 'StackMeet.Api', 'Properties', 'PublishProfiles', 'IISProfile.pubxml');
const gitignorePath = path.join(root, '.gitignore');

const auditService = fs.readFileSync(auditServicePath, 'utf8');
const gitignore = fs.readFileSync(gitignorePath, 'utf8');

assert.ok(
  auditService.includes('configuration["AuditReport:Recipient"]'),
  'Daily audit recipient must come from runtime configuration.'
);
assert.ok(
  !/const\s+string\s+Recipient\s*=/.test(auditService),
  'Daily audit recipient must not be hard-coded as a source constant.'
);
assert.ok(
  !/\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/.test(auditService),
  'Daily audit service must not contain a literal email address.'
);
assert.ok(
  !fs.existsSync(publishProfilePath),
  'The live IIS publish profile must not be tracked in the public-ready tree.'
);

for (const required of [
  '**/.env',
  '**/.env.*',
  '*.pem',
  '*.p12',
  '*.jks',
  '*.keystore',
  '*.publishsettings',
  '*.pubxml',
  '*.pubxml.user'
]) {
  assert.ok(gitignore.includes(required), `.gitignore must protect ${required}`);
}

console.log('Pre-public security static guards passed.');
