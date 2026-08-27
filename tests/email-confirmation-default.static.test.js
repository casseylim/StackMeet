const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const auth = fs.readFileSync(path.join(root, "backend/StackMeet.Api/Controllers/AuthController.cs"), "utf8");

const start = auth.indexOf("async Task<bool> IsEmailConfirmationRequired");
const end = auth.indexOf("static string? BearerToken", start);
const method = auth.slice(start, end);

assert.match(method, /bool\.TryParse\(stored, out var required\) && required/,
  "Email confirmation must only be required when the stored setting explicitly parses to true.");
assert.doesNotMatch(method, /string\.IsNullOrWhiteSpace\(stored\)\s*\|\|/,
  "A missing email-confirmation setting must default to false during rollout.");

console.log("Email confirmation default checks passed.");
