(function (global) {
  "use strict";

  const LEGACY_FINALS_V1 = "legacy-finals-v1";
  const GOVERNED_FINALS_V2 = "governed-finals-v2";
  const SUPPORTED_VERSIONS = Object.freeze([LEGACY_FINALS_V1, GOVERNED_FINALS_V2]);

  const policyDescriptors = Object.freeze({
    [LEGACY_FINALS_V1]: Object.freeze({
      version: LEGACY_FINALS_V1,
      status: "frozen",
      implicitWhenUnversioned: true,
      activation: "existing-operator-behavior",
      penaltyOrdering: "raw-valid-attempts",
      scratchPenaltyOverridesValidAttempts: false,
      historicalPlacementPublication: "blocked"
    }),
    [GOVERNED_FINALS_V2]: Object.freeze({
      version: GOVERNED_FINALS_V2,
      status: "defined-not-activated",
      implicitWhenUnversioned: false,
      activation: "explicit-only",
      penaltyOrdering: "official-best-then-second-third-valid-attempts",
      scratchPenaltyOverridesValidAttempts: true,
      historicalPlacementPublication: "blocked-until-persisted-snapshot"
    })
  });

  const ResultEngine = global.StackMeetBestResult || null;

  function numericAttempts(input) {
    const attempts = Array.isArray(input) ? input : input?.attempts;
    return (Array.isArray(attempts) ? attempts : [])
      .map(value => value === "" || value === null || value === undefined ? NaN : Number(value))
      .filter(Number.isFinite);
  }

  function validAttempts(input) {
    return numericAttempts(input).filter(value => value > 0 && value < 999);
  }

  function finitePenalty(input) {
    const result = Array.isArray(input) ? {} : (input || {});
    const penalty = Number(result.penalty || 0);
    return penalty > 0 && penalty < 999 ? penalty : 0;
  }

  function normalizeRuleVersion(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return SUPPORTED_VERSIONS.includes(normalized) ? normalized : null;
  }

  function resolveRuleVersion(value) {
    if (value === null || value === undefined || String(value).trim() === "") return LEGACY_FINALS_V1;
    return normalizeRuleVersion(value);
  }

  function requireRuleVersion(value) {
    const version = resolveRuleVersion(value);
    if (!version) throw new Error(`Unsupported Finals ranking rule version: ${String(value || "").trim() || "<blank>"}`);
    return version;
  }

  function policyFor(value) {
    const version = requireRuleVersion(value);
    return policyDescriptors[version];
  }

  function legacyClassify(result) {
    if (ResultEngine?.calculateBestResult) return ResultEngine.calculateBestResult(result);
    const values = numericAttempts(result);
    const valid = values.filter(value => value > 0 && value < 999);
    const bestTime = valid.length ? Math.min(...valid) : null;
    if (bestTime !== null) return { status: "valid", bestTime, bestValidTime: bestTime, eligibleForRanking: true };
    if (!values.length) return { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    if (values.every(value => value === 999) || Number(result?.penalty) >= 999) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    return { status: "invalid", bestTime: null, bestValidTime: null, eligibleForRanking: false };
  }

  function governedClassify(result) {
    const values = numericAttempts(result);
    if (Number(result?.penalty) >= 999) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    const valid = values.filter(value => value > 0 && value < 999);
    const bestTime = valid.length ? Math.min(...valid) : null;
    if (bestTime !== null) return { status: "valid", bestTime, bestValidTime: bestTime, eligibleForRanking: true };
    if (!values.length) return { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    if (values.every(value => value === 999)) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    return { status: "invalid", bestTime: null, bestValidTime: null, eligibleForRanking: false };
  }

  function classificationFor(result, ruleVersion) {
    const version = requireRuleVersion(ruleVersion);
    return version === GOVERNED_FINALS_V2 ? governedClassify(result) : legacyClassify(result);
  }

  function legacyTieKey(result) {
    const classification = legacyClassify(result);
    if (!classification.eligibleForRanking) return [Infinity, Infinity, Infinity];
    const times = validAttempts(result).sort((a, b) => a - b);
    return [times[0] ?? Infinity, times[1] ?? Infinity, times[2] ?? Infinity];
  }

  function governedTieKey(result) {
    const classification = governedClassify(result);
    if (!classification.eligibleForRanking) return [Infinity, Infinity, Infinity];
    const times = validAttempts(result).sort((a, b) => a - b);
    return [
      (times[0] ?? Infinity) + finitePenalty(result),
      times[1] ?? Infinity,
      times[2] ?? Infinity
    ];
  }

  function tieKeyFor(result, ruleVersion) {
    const version = requireRuleVersion(ruleVersion);
    return version === GOVERNED_FINALS_V2 ? governedTieKey(result) : legacyTieKey(result);
  }

  function compareKeys(left, right) {
    for (let index = 0; index < 3; index += 1) {
      if (left[index] === right[index]) continue;
      return left[index] - right[index];
    }
    return 0;
  }

  function stableDisplay(left, right) {
    return String(left.name || left.participant || "").localeCompare(
      String(right.name || right.participant || ""),
      undefined,
      { numeric: true, sensitivity: "base" }
    );
  }

  function rankEligibleRows(rows, ruleVersion) {
    const version = requireRuleVersion(ruleVersion);
    const eligible = (Array.isArray(rows) ? rows : [])
      .map(row => ({
        ...row,
        resultStatus: classificationFor(row.result, version).status,
        tieKey: tieKeyFor(row.result, version)
      }))
      .filter(row => classificationFor(row.result, version).eligibleForRanking)
      .sort((left, right) => compareKeys(left.tieKey, right.tieKey) || stableDisplay(left, right));

    let rank = 0;
    let previous = null;
    return eligible.map((row, index) => {
      if (!previous || compareKeys(row.tieKey, previous) !== 0) rank = index + 1;
      const tie = index > 0 && compareKeys(row.tieKey, eligible[index - 1].tieKey) === 0;
      previous = row.tieKey;
      return { ...row, rank, tie, ruleVersion: version };
    });
  }

  const participantTypes = Object.freeze({
    individual: "Individual",
    doubles: "Doubles",
    "timed relay": "Timed Relay",
    relay: "Timed Relay"
  });
  const events = Object.freeze({
    "3-3-3": "3-3-3",
    "3-6-3": "3-6-3",
    cycle: "Cycle"
  });
  const categories = Object.freeze(["normal", "special", "mixed"]);
  const genders = Object.freeze(["all", "M", "F"]);

  function publicationScope(input) {
    const scope = input || {};
    const errors = [];
    const participantType = participantTypes[String(scope.participantType || "").trim().toLowerCase()] || null;
    const division = String(scope.division || "").trim();
    const event = events[String(scope.event || "").trim().toLowerCase()] || null;
    const category = String(scope.category || "").trim().toLowerCase();
    const rawGender = String(scope.gender || "").trim();
    const gender = rawGender.toLowerCase() === "all" ? "all" : rawGender.toUpperCase();

    if (!participantType) errors.push("participantType must be Individual, Doubles or Timed Relay.");
    if (!division || division.toLowerCase() === "all") errors.push("division must be one explicit competition-snapshot division.");
    if (!event) errors.push("event must be 3-3-3, 3-6-3 or Cycle.");
    if (!categories.includes(category)) errors.push("category must be normal, special or mixed.");
    if (!genders.includes(gender)) errors.push("gender must be all, M or F.");

    if (errors.length) return { valid: false, errors, scope: null };
    return {
      valid: true,
      errors: [],
      scope: Object.freeze({ participantType, division, event, category, gender })
    };
  }

  function publicationScopeKey(input) {
    const checked = publicationScope(input);
    if (!checked.valid) throw new Error(`Invalid Finals placement publication scope: ${checked.errors.join(" ")}`);
    const scope = checked.scope;
    return [scope.participantType, scope.division, scope.event, scope.category, scope.gender].join("|");
  }

  const api = Object.freeze({
    LEGACY_FINALS_V1,
    GOVERNED_FINALS_V2,
    supportedVersions: () => [...SUPPORTED_VERSIONS],
    policyDescriptors,
    normalizeRuleVersion,
    resolveRuleVersion,
    requireRuleVersion,
    policyFor,
    numericAttempts,
    validAttempts,
    finitePenalty,
    legacyClassify,
    governedClassify,
    classificationFor,
    legacyTieKey,
    governedTieKey,
    tieKeyFor,
    compareKeys,
    rankEligibleRows,
    publicationScope,
    publicationScopeKey
  });

  global.StackMeetFinalsRankingPolicy = api;
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})(typeof window !== "undefined" ? window : globalThis);
