(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetScoringEntry = api;
})(typeof window !== "undefined" ? window : null, function () {
  "use strict";

  // Existing entry contracts. Do not change competition policy or result authority here.
  function normalizePrelimRounds(value) {
    return Number(value) >= 1 ? "1" : "0";
  }

  function normalizeResults(results) {
    return results.map(result => {
      const participant = String(result.participant || "");
      if (participant.startsWith("2.") && result.type !== "Doubles") return { ...result, type: "Doubles" };
      if (participant.startsWith("3.") && !["Timed Relay", "Relay"].includes(result.type)) return { ...result, type: "Timed Relay" };
      if (result.type === "Relay") return { ...result, type: "Timed Relay" };
      return result;
    });
  }

  function normalizePrelimEntryId(rawId) {
    const input = String(rawId || "").trim();
    const dotted = /^([123])\.(\d+)$/.exec(input);
    if (dotted) return `${dotted[1]}.${Number(dotted[2])}`;
    const compact = /^([123])(\d+)$/.exec(input);
    if (compact) return `${compact[1]}.${Number(compact[2])}`;
    return "";
  }

  function parseCompetitionTime(rawValue) {
    const raw = String(rawValue ?? "").trim();
    if (!raw) return { kind: "blank", value: null };
    if (raw === "999") return { kind: "scratch", value: 999 };
    let seconds;
    const minuteMatch = /^(\d+):([0-5]?\d)(?:\.(\d{1,3}))?$/.exec(raw);
    if (minuteMatch) {
      const fraction = (minuteMatch[3] || "").padEnd(3, "0");
      seconds = Number(minuteMatch[1]) * 60 + Number(minuteMatch[2]) + Number(`0.${fraction || "0"}`);
    } else if (/^\d+$/.test(raw)) {
      seconds = Number(raw) / 1000;
    } else if (/^\d+\.\d{1,3}$/.test(raw)) {
      seconds = Number(raw);
    } else {
      return { kind: "invalid", value: null };
    }
    if (!Number.isFinite(seconds) || seconds <= 0 || seconds > 600) return { kind: "invalid", value: null };
    return { kind: "time", value: Math.round(seconds * 1000) / 1000 };
  }

  function prelimResultInputValue(result, calculateBestResult) {
    if (!result) return "";
    const summary = calculateBestResult(result);
    if (summary.status === "scratch") return "999";
    return Number.isFinite(summary.bestTime) ? summary.bestTime.toFixed(3) : "999";
  }

  function finalResultInputValues(result) {
    if (!result) return ["", "", ""];
    return [0, 1, 2].map(index => {
      const value = result.attempts?.[index];
      if (!Number.isFinite(value)) return "";
      return value >= 999 ? "999" : value.toFixed(3);
    });
  }

  function finalResultAttempts(result) {
    return (result?.attempts || []).filter(value => Number.isFinite(value));
  }

  return Object.freeze({ normalizePrelimRounds, normalizeResults, normalizePrelimEntryId, parseCompetitionTime, prelimResultInputValue, finalResultInputValues, finalResultAttempts });
});
