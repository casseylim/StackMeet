const assert = require("assert");
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const root = path.resolve(__dirname, "..");
const context = { window: {}, console };
context.window.window = context.window;

for (const file of [
  ["results", "BestResultEngine.js"],
  ["reports", "FinalsReportEngine.js"],
  ["reports", "FinalsRankingPolicy.js"]
]) {
  const fullPath = path.join(root, "backend", "StackMeet.Api", "wwwroot", "js", file[0], file[1]);
  vm.runInNewContext(fs.readFileSync(fullPath, "utf8"), context, { filename: file[1] });
}

const best = context.window.StackMeetBestResult;
const finals = context.window.StackMeetFinalsReports;
const policy = context.window.StackMeetFinalsRankingPolicy;
const plain = value => JSON.parse(JSON.stringify(value));
const same = (actual, expected, message) => assert.deepStrictEqual(plain(actual), expected, message);

assert.strictEqual(policy.LEGACY_FINALS_V1, "legacy-finals-v1");
assert.strictEqual(policy.GOVERNED_FINALS_V2, "governed-finals-v2");
same(policy.supportedVersions(), ["legacy-finals-v1", "governed-finals-v2"]);
assert.strictEqual(policy.resolveRuleVersion(undefined), policy.LEGACY_FINALS_V1, "unversioned competitions must remain legacy v1");
assert.strictEqual(policy.resolveRuleVersion(""), policy.LEGACY_FINALS_V1, "blank rule version must remain legacy v1");
assert.strictEqual(policy.resolveRuleVersion("GOVERNED-FINALS-V2"), policy.GOVERNED_FINALS_V2);
assert.strictEqual(policy.resolveRuleVersion("future-finals-v3"), null, "unknown versions must fail closed instead of downgrading silently");
assert.throws(() => policy.requireRuleVersion("future-finals-v3"), /Unsupported Finals ranking rule version/);
assert.strictEqual(policy.policyFor(policy.LEGACY_FINALS_V1).status, "frozen");
assert.strictEqual(policy.policyFor(policy.GOVERNED_FINALS_V2).status, "defined-not-activated");
assert.strictEqual(policy.policyFor(policy.GOVERNED_FINALS_V2).implicitWhenUnversioned, false);

// Legacy v1 must remain byte-for-behavior compatible with the original Finals report tie semantics.
{
  const result = { attempts: [5.4, 5.1, 5.3], penalty: 0.5 };
  same(policy.legacyTieKey(result), plain(finals.finalTieKey(result)), "legacy policy must preserve current raw-attempt tie key");
  same(policy.legacyClassify(result), plain(finals.classifyResult(result)), "legacy classification must preserve current Finals behavior");
}

// SP-4E compatibility finding stays frozen in v1: finite penalty affects shared rankingTime, not the legacy Finals tie key.
{
  const penalized = { attempts: [5.0, 5.2, 5.3], penalty: 0.5 };
  const unpenalized = { attempts: [5.1, 5.2, 5.3], penalty: 0 };
  assert.strictEqual(best.rankingTime(penalized), 5.5);
  same(policy.legacyTieKey(penalized), [5.0, 5.2, 5.3]);
  assert.ok(policy.compareKeys(policy.legacyTieKey(penalized), policy.legacyTieKey(unpenalized)) < 0);
}

// Governed v2 makes the official best time the primary comparator while retaining second/third raw attempts as tie-breakers.
{
  const penalized = { attempts: [5.0, 5.2, 5.3], penalty: 0.5 };
  const unpenalized = { attempts: [5.1, 5.2, 5.3], penalty: 0 };
  same(policy.governedTieKey(penalized), [5.5, 5.2, 5.3]);
  same(policy.governedTieKey(unpenalized), [5.1, 5.2, 5.3]);
  assert.ok(policy.compareKeys(policy.governedTieKey(penalized), policy.governedTieKey(unpenalized)) > 0, "governed v2 must honor the finite penalty in primary ordering");

  const equalOfficialA = { attempts: [5.0, 5.3, 5.5], penalty: 0.2 };
  const equalOfficialB = { attempts: [5.2, 5.25, 5.6], penalty: 0 };
  same(policy.governedTieKey(equalOfficialA), [5.2, 5.3, 5.5]);
  same(policy.governedTieKey(equalOfficialB), [5.2, 5.25, 5.6]);
  assert.ok(policy.compareKeys(policy.governedTieKey(equalOfficialB), policy.governedTieKey(equalOfficialA)) < 0, "second-best valid attempt must break an equal official-best tie");
}

// A scratch penalty overrides otherwise-valid attempts in governed v2, while legacy v1 remains unchanged.
{
  const scratchPenalty = { attempts: [5.0, 5.2, 5.3], penalty: 999 };
  assert.strictEqual(policy.legacyClassify(scratchPenalty).status, "valid");
  assert.strictEqual(policy.governedClassify(scratchPenalty).status, "scratch");
  assert.strictEqual(policy.governedClassify(scratchPenalty).eligibleForRanking, false);
  const key = policy.governedTieKey(scratchPenalty);
  assert.ok(key.every(value => value === Infinity));
}

// Governed v2 keeps 999 attempts out of timing but still allows a separate valid attempt when there is no scratch penalty.
{
  const mixed = { attempts: [999, 5.25, 999], penalty: 0 };
  assert.strictEqual(policy.governedClassify(mixed).status, "valid");
  same(policy.governedTieKey(mixed), [5.25, null, null]);
}

// Versioned rank assignment preserves competition ranking gaps and true ties.
{
  const rows = policy.rankEligibleRows([
    { participant: "1.2", name: "Beta", result: { attempts: [6.0, 6.2, 6.4], penalty: 0 } },
    { participant: "1.1", name: "Alpha", result: { attempts: [6.0, 6.2, 6.4], penalty: 0 } },
    { participant: "1.3", name: "Gamma", result: { attempts: [6.1, 6.2, 6.4], penalty: 0 } },
    { participant: "1.4", name: "Scratch", result: { attempts: [5.0], penalty: 999 } }
  ], policy.GOVERNED_FINALS_V2);
  same(rows.map(row => [row.participant, row.rank, row.tie, row.ruleVersion]), [
    ["1.1", 1, false, "governed-finals-v2"],
    ["1.2", 1, true, "governed-finals-v2"],
    ["1.3", 3, false, "governed-finals-v2"]
  ]);
}

// A publishable placement scope must be explicit. Bare ranks are forbidden by the governance contract.
{
  const valid = policy.publicationScope({
    participantType: "Individual",
    division: "12U",
    event: "Cycle",
    category: "mixed",
    gender: "all"
  });
  assert.strictEqual(valid.valid, true);
  same(valid.scope, { participantType: "Individual", division: "12U", event: "Cycle", category: "mixed", gender: "all" });
  assert.strictEqual(policy.publicationScopeKey(valid.scope), "Individual|12U|Cycle|mixed|all");

  assert.strictEqual(policy.publicationScope({ participantType: "Individual", division: "all", event: "Cycle", category: "mixed", gender: "all" }).valid, false);
  assert.strictEqual(policy.publicationScope({ participantType: "Individual", division: "12U", event: "Cycle", category: "", gender: "all" }).valid, false);
  assert.strictEqual(policy.publicationScope({ participantType: "Individual", division: "12U", event: "Cycle", category: "mixed", gender: "" }).valid, false);
}

// SP-4F owns the versioned contract. Later phases may consume it, but app.js must not hard-code
// governed-v2 or create a second browser-side policy implementation.
{
  const app = fs.readFileSync(path.join(root, "backend", "StackMeet.Api", "wwwroot", "app.js"), "utf8");
  assert.ok(!app.includes("governed-finals-v2"), "operator app must not hard-code governed v2 semantics");
  assert.ok(!app.includes("StackMeetFinalsRankingPolicy"), "app.js remains policy-agnostic; the report engine owns policy consumption");
}

const architecture = fs.readFileSync(path.join(root, "docs", "architecture", "STACKER_IDENTITY_SP4F.md"), "utf8");
assert.match(architecture, /legacy-finals-v1/);
assert.match(architecture, /governed-finals-v2/);
assert.match(architecture, /defined but not activated/i);
assert.match(architecture, /competition-state participant division snapshot/i);
assert.match(architecture, /must never be overwritten/i);
assert.match(architecture, /no production deployment/i);

console.log("SP-4F versioned Finals ranking governance tests passed.");
