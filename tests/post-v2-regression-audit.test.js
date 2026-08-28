"use strict";

const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const appPath = path.join(__dirname, "..", "backend", "StackMeet.Api", "wwwroot", "app.js");
const source = fs.readFileSync(appPath, "utf8");

function extractFunction(name) {
  const marker = `function ${name}(`;
  const start = source.indexOf(marker);
  assert.notStrictEqual(start, -1, `Unable to find ${name} in app.js`);
  const braceStart = source.indexOf("{", start);
  assert.notStrictEqual(braceStart, -1, `Unable to find ${name} body`);
  let depth = 0;
  let quote = null;
  let escaped = false;
  for (let index = braceStart; index < source.length; index += 1) {
    const char = source[index];
    if (quote) {
      if (escaped) escaped = false;
      else if (char === "\\") escaped = true;
      else if (char === quote) quote = null;
      continue;
    }
    if (char === "\"" || char === "'" || char === "`") {
      quote = char;
      continue;
    }
    if (char === "{") depth += 1;
    if (char === "}") {
      depth -= 1;
      if (depth === 0) return source.slice(start, index + 1);
    }
  }
  throw new Error(`Unable to extract complete ${name} function`);
}

function loadFunctions(names) {
  const context = { structuredClone, Map, Set, Array, String };
  context.globalThis = context;
  const code = names.map(extractFunction).join("\n") + `\nglobalThis.__hooks = { ${names.join(", ")} };`;
  vm.runInNewContext(code, context, { filename: "post-v2-regression-audit.vm.js" });
  return context.__hooks;
}

const { mergeConcurrentState, legacyStateForSave } = loadFunctions([
  "mergeConcurrentState",
  "legacyStateForSave"
]);

const findings = [];
function audit(name, test) {
  try {
    test();
    console.log(`PASS ${name}`);
  } catch (error) {
    findings.push(`${name}: ${error.message}`);
    console.error(`CONFIRMED ${name}: ${error.message}`);
  }
}

const latestTeams = {
  doubles: [{ id: "2.1" }, { id: "2.2" }],
  relays: [{ id: "3.1" }, { id: "3.2" }],
  notifications: [],
  auditLogs: []
};
const localTeams = {
  doubles: [{ id: "2.1" }],
  relays: [{ id: "3.1" }],
  notifications: [],
  auditLogs: []
};

// Existing additive merge behavior is intentional: if this computer simply has not
// seen a team created elsewhere yet, the latest server copy must survive the merge.
audit("REMOTE-TEAM-ADDITION-PRESERVED", () => {
  const merged = mergeConcurrentState(latestTeams, localTeams);
  assert.deepStrictEqual(
    JSON.parse(JSON.stringify(merged.doubles.map(item => item.id))),
    ["2.1", "2.2"],
    "A server-side Doubles addition was lost without explicit local deletion intent."
  );
  assert.deepStrictEqual(
    JSON.parse(JSON.stringify(merged.relays.map(item => item.id))),
    ["3.1", "3.2"],
    "A server-side Relay addition was lost without explicit local deletion intent."
  );
});

// P0 regression: an explicit local deletion must override the latest server record.
// The third argument is the required deletion-intent contract; current code ignores it,
// which proves why explicit deletes are presently resurrected.
audit("TEAM-DELETION-MERGE", () => {
  const deleted = {
    doubles: new Set(["2.2"]),
    relays: new Set(["3.2"])
  };
  const merged = mergeConcurrentState(latestTeams, localTeams, deleted);
  assert.deepStrictEqual(
    JSON.parse(JSON.stringify(merged.doubles.map(item => item.id))),
    ["2.1"],
    "Deleted Doubles team 2.2 was resurrected during concurrent-state merge."
  );
  assert.deepStrictEqual(
    JSON.parse(JSON.stringify(merged.relays.map(item => item.id))),
    ["3.1"],
    "Deleted Relay team 3.2 was resurrected during concurrent-state merge."
  );
});

// P0 regression: Year Born is a competition rule used by division calculation and by
// the public results API. The authenticated shared-state save payload must preserve it
// so a reload or public-results calculation cannot silently fall back to actual age.
audit("YEAR-BORN-PERSISTENCE", () => {
  const saved = legacyStateForSave({
    settings: { ageCalculationMode: "yearBorn", language: "en" },
    stackers: [{ id: "1.1" }],
    results: [{ id: "r1" }]
  });
  assert.strictEqual(
    saved.settings.ageCalculationMode,
    "yearBorn",
    "Shared-state save removed ageCalculationMode, so Year Born cannot reliably survive reload/public-results use."
  );
  assert.deepStrictEqual(JSON.parse(JSON.stringify(saved.stackers)), [], "SQL-owned stackers must remain excluded from shared state.");
  assert.deepStrictEqual(JSON.parse(JSON.stringify(saved.results)), [], "SQL-owned results must remain excluded from shared state.");
});

if (findings.length) {
  throw new Error(`Post-v2 regression audit confirmed ${findings.length} defect(s):\n- ${findings.join("\n- ")}`);
}

console.log("PASS post-v2 regression audit — concurrency additions, explicit team deletions and Year Born persistence are protected");
