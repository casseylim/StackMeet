const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..");
const store = new Map();
const context = { window: {}, console };
context.window.window = context.window;
context.window.sessionStorage = {
  getItem: key => store.has(key) ? store.get(key) : null,
  setItem: (key, value) => store.set(key, String(value)),
  removeItem: key => store.delete(key)
};

for (const file of [
  ["results", "BestResultEngine.js"],
  ["reports", "FinalsRankingPolicy.js"],
  ["reports", "FinalsReportEngine.js"]
]) {
  const fullPath = path.join(root, "backend", "StackMeet.Api", "wwwroot", "js", file[0], file[1]);
  vm.runInNewContext(fs.readFileSync(fullPath, "utf8"), context, { filename: file[1] });
}

const finals = context.window.StackMeetFinalsReports;
const policy = context.window.StackMeetFinalsRankingPolicy;
const plain = value => JSON.parse(JSON.stringify(value));
const same = (actual, expected, message) => assert.deepStrictEqual(plain(actual), expected, message);
const selectCompetition = (id, ruleVersion) => {
  store.set("stackmeet-sql-competition-id", String(id));
  context.window.StackMeetFinalsRankingRules = Object.freeze({ [String(id)]: ruleVersion });
};

// No SQL-selected competition preserves the historical compatibility default.
store.delete("stackmeet-sql-competition-id");
delete context.window.StackMeetFinalsRankingRules;
assert.strictEqual(finals.operatorFinalsRuleVersion(), policy.LEGACY_FINALS_V1);
assert.strictEqual(finals.ruleVersionForStage("Prelims"), policy.LEGACY_FINALS_V1);

// A selected SQL competition must have a loaded persisted rule. Missing/unknown values fail closed.
store.set("stackmeet-sql-competition-id", "42");
assert.throws(() => finals.operatorFinalsRuleVersion(), /rule is unavailable/i);
context.window.StackMeetFinalsRankingRules = Object.freeze({ "42": "future-finals-v99" });
assert.throws(() => finals.operatorFinalsRuleVersion(), /Unsupported Finals ranking rule version/);

const state = {
  stackers: [
    { id: "1.1", name: "Alpha", division: "Open", gender: "M", special: "No", org: "Club A", country: "MY", region: "NS" },
    { id: "1.2", name: "Beta", division: "Open", gender: "M", special: "No", org: "Club B", country: "MY", region: "NS" }
  ],
  doubles: [],
  relays: [],
  results: [
    { id: "A", stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [5.0, 5.2, 5.3], penalty: 0.5 },
    { id: "B", stage: "Finals", type: "Individual", participant: "1.2", event: "3-3-3", attempts: [5.1, 5.2, 5.3], penalty: 0 },
    { id: "PA", stage: "Prelims", type: "Individual", participant: "1.1", event: "Cycle", attempts: [6.0, 6.2, 6.4], penalty: 999 }
  ]
};

// Legacy v1 remains byte-for-behavior compatible: raw best still orders ahead despite a finite penalty.
selectCompetition(42, policy.LEGACY_FINALS_V1);
assert.strictEqual(finals.operatorFinalsRuleVersion(), policy.LEGACY_FINALS_V1);
let placements = finals.placementRows(state, { participantType: "Individual", division: "Open", event: "3-3-3" });
same(placements.map(row => [row.participant, row.rank, row.ruleVersion]), [
  ["1.1", 1, "legacy-finals-v1"],
  ["1.2", 2, "legacy-finals-v1"]
]);

// Governed v2 activates only when persisted explicitly: official best time includes the finite penalty.
selectCompetition(42, policy.GOVERNED_FINALS_V2);
assert.strictEqual(finals.operatorFinalsRuleVersion(), policy.GOVERNED_FINALS_V2);
placements = finals.placementRows(state, { participantType: "Individual", division: "Open", event: "3-3-3" });
same(placements.map(row => [row.participant, row.rank, row.ruleVersion]), [
  ["1.1", 2, "governed-finals-v2"],
  ["1.2", 1, "governed-finals-v2"]
]);

// Governed v2 scratch penalty overrides otherwise valid Finals attempts.
const scratchState = {
  ...state,
  results: [
    { id: "S", stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [4.9, 5.0, 5.1], penalty: 999 },
    { id: "V", stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [5.2, 5.3, 5.4], penalty: 0 },
    { id: "PS", stage: "Prelims", type: "Individual", participant: "1.1", event: "Cycle", attempts: [4.9, 5.0, 5.1], penalty: 999 }
  ]
};
const finalsRows = finals.stagePlacementRows(scratchState, "Finals", { participantType: "Individual", division: "Open", event: "Cycle" });
assert.strictEqual(finalsRows.find(row => row.participant === "1.1").resultStatus, "scratch");
assert.strictEqual(finalsRows.find(row => row.participant === "1.1").rank, null);
assert.strictEqual(finalsRows.find(row => row.participant === "1.2").rank, 1);

// Preliminary reports remain on frozen legacy behavior even while the selected Finals rule is v2.
const prelimRows = finals.stageResultRows(scratchState, "Prelims", { participantType: "Individual", event: "Cycle" });
assert.strictEqual(prelimRows[0].resultStatus, "valid");
assert.strictEqual(prelimRows[0].ruleVersion, policy.LEGACY_FINALS_V1);

// All-Around is deliberately outside SP-4H and therefore remains legacy-compatible.
const allAroundState = {
  stackers: [state.stackers[0]], doubles: [], relays: [],
  results: [
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [5, 5.1, 5.2], penalty: 999 },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-6-3", attempts: [6, 6.1, 6.2], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [7, 7.1, 7.2], penalty: 0 }
  ]
};
const allAround = finals.stageAllAroundRows(allAroundState, "Finals", {});
assert.strictEqual(allAround[0].resultStatus, "valid");
assert.strictEqual(allAround[0].ruleVersion, policy.LEGACY_FINALS_V1);

// The read path is authenticated/read-only and the client explicitly loads the reviewed policy module.
const controller = fs.readFileSync(path.join(root, "backend", "StackMeet.Api", "Controllers", "FinalsRankingRulesController.cs"), "utf8");
const stackerApi = fs.readFileSync(path.join(root, "backend", "StackMeet.Api", "wwwroot", "js", "storage", "StackerApi.js"), "utf8");
assert.match(controller, /\[HttpGet\]/);
assert.match(controller, /CacheControl = "no-store"/);
assert.ok(!controller.includes("SelectRuleVersionAsync"), "SP-4H read endpoint must not select a rule");
assert.ok(!controller.includes("CaptureFinalizedSnapshotAsync"), "SP-4H read endpoint must not certify a historical snapshot");
assert.match(stackerApi, /api\/competitions\/finals-ranking-rules/);
assert.match(stackerApi, /FinalsRankingPolicy\.js\?v=stacker-identity-v1-sp4h/);
assert.match(stackerApi, /StackMeetFinalsRankingRules/);

const architecture = fs.readFileSync(path.join(root, "docs", "architecture", "STACKER_IDENTITY_SP4H.md"), "utf8");
assert.match(architecture, /event-level Finals/i);
assert.match(architecture, /Prelims[^\n]*legacy/i);
assert.match(architecture, /All-Around[^\n]*outside/i);
assert.match(architecture, /no rule-selection/i);
assert.match(architecture, /no production deployment/i);
assert.match(architecture, /web\.config[^\n]*must never be overwritten/i);

console.log("SP-4H operator Finals ranking activation characterization passed.");
