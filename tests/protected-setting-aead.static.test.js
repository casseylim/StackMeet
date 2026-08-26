const fs = require("fs");
const assert = require("assert");

const source = fs.readFileSync("backend/StackMeet.Api/Services/ProtectedSettingService.cs", "utf8");

assert.match(source, /const string CurrentFormat = "v2"/);
assert.match(source, /const int GcmNonceSize = 12/);
assert.match(source, /const int GcmTagSize = 16/);
assert.match(source, /new AesGcm\(encryptionKey, GcmTagSize\)/);
assert.match(source, /aes\.Encrypt\(nonce, plainBytes, cipherBytes, tag, AssociatedData\(settingKey\)\)/);
assert.match(source, /aes\.Decrypt\(nonce, cipherBytes, tag, plainBytes, AssociatedData\(settingKey\)\)/);
assert.match(source, /StackMeet\.ProtectedSetting\.\{CurrentFormat\}\|\{settingKey\}/);
assert.match(source, /CryptographicOperations\.ZeroMemory\(encryptionKey\)/);

assert.match(source, /IsCurrentFormat\(value\)[\s\S]*UnprotectCurrent\(settingKey, value\)[\s\S]*UnprotectLegacy\(value\)/);
assert.match(source, /using var aes = Aes\.Create\(\);[\s\S]*aes\.IV = Convert\.FromBase64String\(parts\[0\]\)[\s\S]*CreateDecryptor\(\)/);
assert.doesNotMatch(source, /CreateEncryptor\(\)/);

assert.match(source, /var isLegacy = !IsCurrentFormat\(setting\.Value\)/);
assert.match(source, /TryUpgradeLegacyValue\(key, setting, value, ct\)/);
assert.match(source, /item\.Id == setting\.Id && item\.IsProtected && item\.Value == setting\.Value/);
assert.match(source, /ExecuteUpdateAsync/);
assert.doesNotMatch(source, /SetProperty\(item => item\.UpdatedAt/);

assert.match(source, /logger\.LogWarning\(error,[\s\S]*legacy encryption upgrade failed/);
assert.doesNotMatch(source, /Log(?:Information|Warning|Error|Debug)\([^\n]*plainValue/);
assert.doesNotMatch(source, /Log(?:Information|Warning|Error|Debug)\([^\n]*upgradedValue/);

assert.match(source, /setting\.Value = protect \? Protect\(key, value\) : value/);
assert.match(source, /public bool CanProtect => !string\.IsNullOrWhiteSpace\(EncryptionKey\)/);

console.log("Protected setting authenticated-encryption safety tests passed.");
