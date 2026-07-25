const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const assert = require("node:assert/strict");

class TestNode {
  constructor(tagName = "div") {
    this.tagName = String(tagName).toUpperCase();
    this.children = [];
    this.className = "";
    this.textContent = "";
    this.dataset = {};
    this.style = {};
    this.hidden = false;
    this.attributes = {};
  }
  append(...children) { this.children.push(...children.flat().filter(Boolean)); }
  replaceChildren(...children) { this.children = children.flat().filter(Boolean); }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  addEventListener() {}
}

const nodes = new Map();
const document = {
  createElement: tag => new TestNode(tag),
  getElementById: id => nodes.get(id) || null,
  querySelectorAll: () => [],
  querySelector: selector => selector === ".medal-table" ? nodes.get("medalTable") || null : null
};
const window = {
  setInterval() { return 0; },
  setTimeout() { return 0; }
};
const context = {
  console,
  document,
  window,
  location: { pathname: "/SAMPLE/Results", },
  Intl,
  Map,
  Set,
  Date,
  Number,
  String,
  Array,
  Math,
  encodeURIComponent,
  decodeURIComponent
};
context.globalThis = context;

const sourcePath = path.resolve(__dirname, "../backend/StackMeet.Api/wwwroot/results/results.js");
let source = fs.readFileSync(sourcePath, "utf8");
source = source.replace(
  /\s*void refresh\(true\);\s*void connectLiveUpdates\(\);\s*window\.setInterval\(\(\) => void refresh\(false\), 15000\);/,
  ""
);
source = source.replace(
  /\}\)\(\);\s*$/,
  `globalThis.__portalTest = {
    bestTime,
    sectionHasPublishedResults,
    renderFinalEvent,
    renderAllAround,
    renderMedals,
    renderDoubles,
    renderRelay,
    allAroundEventKey
  };
})();`
);
vm.runInNewContext(source, context, { filename: "results.js" });
const portal = context.__portalTest;
assert.ok(portal, "Portal test hooks must load.");

const node = (id, tag = "div") => {
  const value = new TestNode(tag);
  nodes.set(id, value);
  return value;
};
const resetNodes = () => nodes.clear();
const descendants = (root, tagName) => {
  const matches = [];
  const visit = current => {
    if (!current || typeof current !== "object") return;
    if (!tagName || current.tagName === tagName.toUpperCase()) matches.push(current);
    (current.children || []).forEach(visit);
  };
  visit(root);
  return matches;
};
const cells = row => row.children.filter(child => child.tagName === "TD");
const result = (participant, stage, type, event, attempts, penalty = 0) => ({
  participant, stage, type, event, attempts, penalty
});
const stackers = [
  { id: "A", name: "Alpha", division: "U12 Male", org: "Org A" },
  { id: "B", name: "Bravo", division: "U12 Male", org: "Org B" },
  { id: "C", name: "Charlie", division: "U12 Male", org: "Org C" },
  { id: "D", name: "Delta", division: "U12 Female", org: "Org A" },
  { id: "E", name: "Echo", division: "U12 Female", org: "Org B" },
  { id: "F", name: "Foxtrot", division: "U12 Female", org: "Org C" }
];

assert.equal(portal.bestTime(result("A", "Final", "Individual", "3-3-3", [6.2, 5.1, 5.8], 0.2)), 5.3);
assert.ok(Number.isNaN(portal.bestTime(result("A", "Final", "Individual", "3-3-3", [], 0))));
assert.ok(Number.isNaN(portal.bestTime(result("A", "Final", "Individual", "3-3-3", [5], 999))));
assert.equal(portal.allAroundEventKey("The Cycle"), "cycle");
assert.equal(portal.allAroundEventKey("3-6-3"), "363");

const availabilityPayload = {
  results: [
    result("A", "Prelim", "Individual", "3-3-3", [5]),
    result("A", "Preliminary", "Individual", "3-6-3", [6]),
    result("A", "Prelims", "Individual", "Cycle", [7]),
    result("D1", "Finals", "Doubles", "Doubles", [10]),
    result("R1", "Final", "Timed Relay", "3-6-3 Relay", [20])
  ]
};
assert.equal(portal.sectionHasPublishedResults(availabilityPayload, "preliminary"), true);
assert.equal(portal.sectionHasPublishedResults(availabilityPayload, "finals"), false);
assert.equal(portal.sectionHasPublishedResults(availabilityPayload, "allaround"), true);
assert.equal(portal.sectionHasPublishedResults(availabilityPayload, "doubles"), true);
assert.equal(portal.sectionHasPublishedResults(availabilityPayload, "relay"), true);
assert.equal(portal.sectionHasPublishedResults({ results: [] }, "medals"), false);

const finalRows = [
  { result: result("C", "Final", "Individual", "Cycle", [], 999), stacker: stackers[2], best: Number.NaN },
  { result: result("B", "Final", "Individual", "Cycle", [8]), stacker: stackers[1], best: 8 },
  { result: result("A", "Final", "Individual", "Cycle", [8]), stacker: stackers[0], best: 8 },
  { result: result("D", "Final", "Individual", "Cycle", [9]), stacker: stackers[3], best: 9 }
];
const finalCard = portal.renderFinalEvent("Cycle", finalRows, false);
const finalBody = descendants(finalCard, "tbody")[0];
assert.deepEqual(finalBody.children.map(row => cells(row)[0].textContent), ["🥇 1", "🥇 1", "🥉 3", "—"]);
assert.deepEqual(finalBody.children.map(row => cells(row)[3].textContent), ["8.000", "8.000", "9.000", "SCR"]);

assert.deepEqual(descendants(finalCard, "th").map(cell => cell.textContent), ["Place", "Stacker", "Organization", "Best", "GAP"]);
assert.deepEqual(finalBody.children.map(row => cells(row)[4].textContent), ["--", "--", "+1.000", "--"]);
assert.equal(descendants(finalCard, ".event-state").length, 0);

resetNodes();
const allAroundGroups = node("allAroundGroups");
node("allAroundSummary");
portal.renderAllAround({
  stackers,
  results: [
    result("A", "Final", "Individual", "3-3-3", [5]),
    result("A", "Finals", "Individual", "3-6-3", [6]),
    result("A", "Final", "Individual", "Cycle", [7]),
    result("B", "Final", "Individual", "3-3-3", [5]),
    result("B", "Final", "Individual", "3-6-3", [6]),
    result("B", "Final", "Individual", "Cycle", [7]),
    result("C", "Final", "Individual", "3-3-3", [4]),
    result("C", "Final", "Individual", "3-6-3", [5])
  ]
}, false);
const allAroundRows = descendants(allAroundGroups, "tbody")[0].children;
assert.equal(allAroundRows.length, 2, "Incomplete three-event stackers must be excluded.");
assert.deepEqual(allAroundRows.map(row => cells(row)[0].textContent), ["🥇 1", "🥇 1"]);
assert.deepEqual(allAroundRows.map(row => cells(row)[6].textContent), ["18.000", "18.000"]);

const doubles = [
  { id: "D1", one: "A", two: "B", name: "AB", customDivision: "U12 Doubles" },
  { id: "D2", one: "C", two: "D", name: "CD", customDivision: "U12 Doubles" },
  { id: "D3", one: "E", two: "F", name: "EF", customDivision: "Child / Parent" }
];
resetNodes();
const doublesGroups = node("doublesGroups");
node("doublesSummary");
portal.renderDoubles({
  stackers,
  doubles,
  results: [
    result("D1", "Final", "Doubles", "Doubles", [10]),
    result("D2", "Finals", "Doubles", "Doubles", [10]),
    result("D3", "Final", "Doubles", "Doubles", [], 999)
  ]
}, false);
assert.equal(doublesGroups.children.length, 2, "Configured Doubles divisions must remain separate.");
const doublesBodies = descendants(doublesGroups, "tbody");
const rankedDoublesBody = doublesBodies.find(body => body.children.length === 2);
const scrDoublesBody = doublesBodies.find(body => body.children.length === 1);
assert.deepEqual(rankedDoublesBody.children.map(row => cells(row)[0].textContent), ["🥇 1", "🥇 1"]);
assert.equal(cells(scrDoublesBody.children[0])[4].textContent, "SCR");

const relays = [
  { id: "R1", name: "Relay A", division: "U12 Relay", members: ["A", "D"], org: "Org A" },
  { id: "R2", name: "Relay B", division: "U12 Relay", members: ["B", "E"], org: "Org B" }
];
resetNodes();
const relayGroups = node("relayGroups");
node("relaySummary");
portal.renderRelay({
  stackers,
  relays,
  results: [
    result("R1", "Final", "Timed Relay", "3-6-3 Relay", [20]),
    result("R2", "Finals", "Timed Relay", "3-6-3 Relay", [21])
  ]
}, false);
const relayRows = descendants(relayGroups, "tbody")[0].children;
assert.deepEqual(relayRows.map(row => cells(row)[0].textContent), ["🥇 1", "🥈 2"]);

resetNodes();
const medalRows = node("medalRows", "tbody");
const medalTable = node("medalTable", "table");
node("medalsEmpty");
node("medalsSummary");
portal.renderMedals({
  stackers,
  doubles: [
    doubles[0],
    doubles[1],
    { id: "D4", one: "E", two: "F", name: "EF", customDivision: "U12 Doubles" }
  ],
  relays,
  results: [
    result("A", "Final", "Individual", "Cycle", [5]),
    result("A", "Final", "Individual", "Cycle", [5.2]),
    result("B", "Final", "Individual", "Cycle", [5]),
    result("C", "Final", "Individual", "Cycle", [6]),
    result("D1", "Final", "Doubles", "Doubles", [10]),
    result("D2", "Final", "Doubles", "Doubles", [11]),
    result("D4", "Final", "Doubles", "Doubles", [], 999),
    result("R1", "Final", "Timed Relay", "Relay", [20]),
    result("R2", "Final", "Timed Relay", "Relay", [21]),
    result("D", "Preliminary", "Individual", "Cycle", [4])
  ]
}, false);
assert.equal(medalTable.hidden, false);
const medalValues = medalRows.children.map(row => cells(row).map(cell => cell.textContent));
assert.deepEqual(medalValues, [
  ["1", "Org A", "2", "0", "0", "2"],
  ["2", "Org B", "1", "1", "0", "2"],
  ["3", "Org A / Org B", "1", "0", "0", "1"],
  ["4", "Org A / Org C", "0", "1", "0", "1"],
  ["5", "Org C", "0", "0", "1", "1"]
], "Medals must deduplicate saves, respect ties/SCR, and sort Gold-Silver-Bronze.");

console.log("Public results portal representative-data tests passed.");
