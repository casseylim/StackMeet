const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..");
const context = { window: {}, console };
context.window.window = context.window;

vm.runInNewContext(
  fs.readFileSync(path.join(root, "backend", "StackMeet.Api", "wwwroot", "js", "results", "BestResultEngine.js"), "utf8"),
  context,
  { filename: "BestResultEngine.js" }
);
vm.runInNewContext(
  fs.readFileSync(path.join(root, "backend", "StackMeet.Api", "wwwroot", "js", "reports", "FinalsReportEngine.js"), "utf8"),
  context,
  { filename: "FinalsReportEngine.js" }
);

const best = context.window.StackMeetBestResult;
const finals = context.window.StackMeetFinalsReports;
const same = (actual, expected, message) => assert.deepStrictEqual(JSON.parse(JSON.stringify(actual)), expected, message);

function baseState() {
  return {
    stackers: [
      { id: "1.1", name: "Alpha", division: "12U", org: "School A", gender: "M", special: "No" },
      { id: "1.2", name: "Beta", division: "12U", org: "School B", gender: "M", special: "No" },
      { id: "1.3", name: "Gamma", division: "12U", org: "School C", gender: "F", special: "No" },
      { id: "1.4", name: "Delta", division: "12U", org: "School D", gender: "F", special: "Yes" },
      { id: "1.5", name: "Epsilon", division: "14U", org: "School E", gender: "M", special: "No" }
    ],
    doubles: [
      { id: "2.1", name: "Alpha / Beta", one: "1.1", two: "1.2", division: "12U", org: "School A" }
    ],
    relays: [],
    results: []
  };
}

// Canonical Finals tie key is raw valid attempts sorted ascending: best, second-best, third-best.
same(finals.finalTieKey({ attempts: [5.4, 5.1, 5.3], penalty: 0 }), [5.1, 5.3, 5.4]);

const lexicographic = finals.rankFinalRows([
  { participant: "1.1", name: "Alpha", result: { attempts: [5.0, 5.4, 5.7], penalty: 0 } },
  { participant: "1.2", name: "Beta", result: { attempts: [5.0, 5.3, 5.8], penalty: 0 } },
  { participant: "1.3", name: "Gamma", result: { attempts: [5.0, 5.3, 5.7], penalty: 0 } }
]);
same(lexicographic.map(row => [row.participant, row.rank]), [["1.3", 1], ["1.2", 2], ["1.1", 3]], "best, then second-best, then third-best must decide Finals placement");

// Equal complete tie keys keep equal competition rank; display name only stabilizes display order.
const equal = finals.rankFinalRows([
  { participant: "1.2", name: "Beta", result: { attempts: [6.0, 6.2, 6.4], penalty: 0 } },
  { participant: "1.1", name: "Alpha", result: { attempts: [6.0, 6.2, 6.4], penalty: 0 } },
  { participant: "1.3", name: "Gamma", result: { attempts: [6.1, 6.2, 6.4], penalty: 0 } }
]);
same(equal.map(row => [row.participant, row.rank, row.tie]), [["1.1", 1, false], ["1.2", 1, true], ["1.3", 3, false]], "equal performances must rank 1,1,3 and name must not break the tie");

// Finals placement is Finals-stage only; a faster Preliminary result cannot displace a Finals result.
{
  const state = baseState();
  state.results = [
    { stage: "Prelims", type: "Individual", participant: "1.1", event: "Cycle", attempts: [4.0, 4.1, 4.2], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [7.0, 7.1, 7.2], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.2", event: "Cycle", attempts: [6.0, 6.1, 6.2], penalty: 0 }
  ];
  const rows = finals.placementRows(state, { participantType: "Individual", division: "12U", event: "Cycle", category: "normal" });
  same(rows.map(row => [row.participant, row.rank]), [["1.1", 2], ["1.2", 1]], "Preliminary result must not enter Finals placement");
}

// Ranking scope is participant type + competition-snapshot division + event. Each scope ranks independently.
{
  const state = baseState();
  state.results = [
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-3-3", attempts: [5.0], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.5", event: "3-3-3", attempts: [4.0], penalty: 0 },
    { stage: "Finals", type: "Doubles", participant: "2.1", event: "3-3-3", attempts: [3.0], penalty: 0 }
  ];
  const rows = finals.stagePlacementRows(state, "Finals", { category: "mixed" });
  same(rows.map(row => [row.type, row.division, row.participant, row.rank]), [
    ["Individual", "12U", "1.1", 1],
    ["Individual", "14U", "1.5", 1],
    ["Doubles", "12U", "2.1", 1]
  ], "type and division scopes must rank independently");
}

// Non-valid rows stay visible to reporting but are unplaced.
{
  const state = baseState();
  state.results = [
    { stage: "Finals", type: "Individual", participant: "1.1", event: "3-6-3", attempts: [5.0], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.2", event: "3-6-3", attempts: [999, 999], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.3", event: "3-6-3", attempts: [], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.4", event: "3-6-3", attempts: [0], penalty: 0 }
  ];
  const rows = finals.stagePlacementRows(state, "Finals", { division: "12U", event: "3-6-3", category: "mixed" });
  const byParticipant = Object.fromEntries(rows.map(row => [row.participant, row]));
  assert.strictEqual(byParticipant["1.1"].rank, 1);
  assert.strictEqual(byParticipant["1.2"].resultStatus, "scratch");
  assert.strictEqual(byParticipant["1.2"].rank, null);
  assert.strictEqual(byParticipant["1.3"].resultStatus, "missing");
  assert.strictEqual(byParticipant["1.3"].rank, null);
  assert.strictEqual(byParticipant["1.4"].resultStatus, "invalid");
  assert.strictEqual(byParticipant["1.4"].rank, null);
}

// Category and gender are pre-ranking filters. Historical placement therefore needs an explicit scope contract.
{
  const state = baseState();
  state.results = [
    { stage: "Finals", type: "Individual", participant: "1.1", event: "Cycle", attempts: [7.0], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.3", event: "Cycle", attempts: [6.0], penalty: 0 },
    { stage: "Finals", type: "Individual", participant: "1.4", event: "Cycle", attempts: [5.0], penalty: 0 }
  ];
  const normalMale = finals.stagePlacementRows(state, "Finals", { participantType: "Individual", division: "12U", event: "Cycle", category: "normal", gender: "M" });
  const normalFemale = finals.stagePlacementRows(state, "Finals", { participantType: "Individual", division: "12U", event: "Cycle", category: "normal", gender: "F" });
  const specialFemale = finals.stagePlacementRows(state, "Finals", { participantType: "Individual", division: "12U", event: "Cycle", category: "special", gender: "F" });
  const mixed = finals.stagePlacementRows(state, "Finals", { participantType: "Individual", division: "12U", event: "Cycle", category: "mixed" });
  same(normalMale.map(row => [row.participant, row.rank]), [["1.1", 1]]);
  same(normalFemale.map(row => [row.participant, row.rank]), [["1.3", 1]]);
  same(specialFemale.map(row => [row.participant, row.rank]), [["1.4", 1]]);
  same(mixed.map(row => [row.participant, row.rank]), [["1.1", 3], ["1.3", 2], ["1.4", 1]]);
}

// Compatibility finding: shared rankingTime applies a normal finite penalty, while Finals finalTieKey does not.
// This intentionally locks the observed legacy behavior so historical placement cannot silently choose one semantic.
{
  const penalized = { attempts: [5.0, 5.2, 5.3], penalty: 0.5 };
  const unpenalized = { attempts: [5.1, 5.2, 5.3], penalty: 0 };
  assert.strictEqual(best.rankingTime(penalized), 5.5);
  assert.strictEqual(best.rankingTime(unpenalized), 5.1);
  assert.ok(best.rankingTime(penalized) > best.rankingTime(unpenalized), "shared penalty-adjusted ranking time makes the penalized row slower");
  assert.ok(finals.compareKeys(finals.finalTieKey(penalized), finals.finalTieKey(unpenalized)) < 0, "legacy Finals tie key still orders by raw attempts");
  const ranked = finals.rankFinalRows([
    { participant: "1.1", name: "Penalized", result: penalized },
    { participant: "1.2", name: "Unpenalized", result: unpenalized }
  ]);
  same(ranked.map(row => [row.participant, row.rank]), [["1.1", 1], ["1.2", 2]], "legacy Finals placement is raw-attempt based despite finite penalty divergence");
}

// Compatibility finding: an otherwise-valid attempt is classified Valid before a 999 penalty is considered.
// This is recorded, not endorsed or changed, by SP-4E.
{
  const legacyScratchPenalty = { attempts: [5.0, 5.2, 5.3], penalty: 999 };
  same(finals.classifyResult(legacyScratchPenalty), { status: "valid", bestTime: 5.0, bestValidTime: 5.0, eligibleForRanking: true });
  assert.strictEqual(best.rankingTime(legacyScratchPenalty), 5.0);
  same(finals.finalTieKey(legacyScratchPenalty), [5.0, 5.2, 5.3]);
}

console.log("SP-4E Finals ranking compatibility characterization passed.");
