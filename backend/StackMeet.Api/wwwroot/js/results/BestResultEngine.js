/**
 * Pure result classifier shared by entry, ranking, and reporting workflows.
 * A valid time outranks scratch, invalid, and missing results.
 */
(function (global) {
  "use strict";

  const statusOrder = { valid: 0, scratch: 1, invalid: 2, missing: 3 };

  function numericAttempts(attempts) {
    return (Array.isArray(attempts) ? attempts : [])
      .map(value => value === "" || value === null || value === undefined ? NaN : Number(value))
      .filter(Number.isFinite);
  }

  function validAttempts(attempts) {
    return numericAttempts(attempts).filter(value => value > 0 && value < 999);
  }

  function isScratchAttempt(value) {
    return Number(value) === 999;
  }

  function calculateBestResult(input) {
    const result = Array.isArray(input) ? { attempts: input } : (input || {});
    const values = numericAttempts(result.attempts);
    const valid = validAttempts(values);
    const bestTime = valid.length ? Math.min(...valid) : null;
    if (bestTime !== null) return { status: "valid", bestTime, bestValidTime: bestTime, eligibleForRanking: true };
    if (!values.length) return { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    if (values.every(isScratchAttempt) || Number(result.penalty) >= 999) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    return { status: "invalid", bestTime: null, bestValidTime: null, eligibleForRanking: false };
  }

  function bestTime(input) {
    return calculateBestResult(input).bestTime;
  }

  function appliedPenalty(input) {
    const result = Array.isArray(input) ? {} : (input || {});
    const penalty = Number(result.penalty || 0);
    return penalty > 0 && penalty < 999 ? penalty : 0;
  }

  function rankingTime(input) {
    const summary = calculateBestResult(input);
    return summary.eligibleForRanking ? summary.bestTime + appliedPenalty(input) : Infinity;
  }

  global.StackMeetBestResult = { statusOrder, numericAttempts, finiteAttempts: numericAttempts, validAttempts, isScratchAttempt, calculateBestResult, classifyResult: calculateBestResult, bestTime, appliedPenalty, rankingTime };
  if (typeof module !== "undefined" && module.exports) module.exports = global.StackMeetBestResult;
})(typeof window !== "undefined" ? window : globalThis);
