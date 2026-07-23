(function (global) {
  "use strict";

  const requiredAllAroundEvents = ["3-3-3", "3-6-3", "cycle"];
  const ResultEngine = global.StackMeetBestResult || (() => {
    const statusOrder = { valid: 0, scratch: 1, invalid: 2, missing: 3 };
    const numericAttempts = attempts => (Array.isArray(attempts) ? attempts : [])
      .map(value => value === "" || value === null || value === undefined ? NaN : Number(value))
      .filter(Number.isFinite);
    const validAttempts = attempts => numericAttempts(attempts).filter(value => value > 0 && value < 999);
    const isScratchAttempt = value => Number(value) === 999;
    const calculateBestResult = input => {
      const result = Array.isArray(input) ? { attempts: input } : (input || {});
      const values = numericAttempts(result.attempts);
      const valid = validAttempts(values);
      const bestTime = valid.length ? Math.min(...valid) : null;
      if (bestTime !== null) return { status: "valid", bestTime, bestValidTime: bestTime, eligibleForRanking: true };
      if (!values.length) return { status: "missing", bestTime: null, bestValidTime: null, eligibleForRanking: false };
      if (values.every(isScratchAttempt) || Number(result.penalty) >= 999) return { status: "scratch", bestTime: null, bestValidTime: null, eligibleForRanking: false };
      return { status: "invalid", bestTime: null, bestValidTime: null, eligibleForRanking: false };
    };
    return { statusOrder, finiteAttempts: numericAttempts, validAttempts, calculateBestResult };
  })();
  const statusOrder = ResultEngine.statusOrder;

  function normalizedEvent(event) { return String(event || "").toLowerCase() === "cycle" ? "cycle" : String(event || ""); }
  function finiteAttempts(result) { return ResultEngine.finiteAttempts(result?.attempts || []); }
  function classifyResult(result) {
    return ResultEngine.calculateBestResult(result);
  }
  function finalTieKey(result) {
    const classification = classifyResult(result);
    if (!classification.eligibleForRanking) return [Infinity, Infinity, Infinity];
    const times = ResultEngine.validAttempts(result?.attempts || []).sort((a, b) => a - b);
    return [times[0] ?? Infinity, times[1] ?? Infinity, times[2] ?? Infinity];
  }
  function compareKeys(left, right) {
    for (let index = 0; index < 3; index += 1) {
      if (left[index] === right[index]) continue;
      return left[index] - right[index];
    }
    return 0;
  }
  function stableDisplay(left, right) { return String(left.name || left.participant || "").localeCompare(String(right.name || right.participant || ""), undefined, { numeric: true, sensitivity: "base" }); }
  function rankFinalRows(rows) {
    const sorted = [...rows].sort((left, right) => {
      const keyComparison = compareKeys(left.tieKey || finalTieKey(left.result), right.tieKey || finalTieKey(right.result));
      return keyComparison || stableDisplay(left, right);
    });
    let rank = 0, previous = null;
    return sorted.map((row, index) => {
      const key = row.tieKey || finalTieKey(row.result);
      if (!previous || compareKeys(key, previous) !== 0) rank = index + 1;
      previous = key;
      return { ...row, rank, tie: index > 0 && compareKeys(key, sorted[index - 1].tieKey || finalTieKey(sorted[index - 1].result)) === 0 };
    });
  }
  function participantMeta(state, type, participant) {
    const stacker = id => state.stackers.find(item => item.id === id);
    const memberIds = type === "Doubles" ? doubleMembers(state.doubles.find(item => item.id === participant)) : type === "Timed Relay" ? relayMembers(state.relays.find(item => item.id === participant)) : [participant];
    const members = memberIds.map(stacker).filter(Boolean);
    const direct = stacker(participant);
    const team = type === "Doubles" ? state.doubles.find(item => item.id === participant) : type === "Timed Relay" ? state.relays.find(item => item.id === participant) : null;
    const name = direct?.name || team?.name || (members.length ? members.map(item => item.name || item.id).join(" / ") : participant);
    return { participant, type, name, members, gender: direct?.gender || "", division: direct?.division || team?.division || "", special: direct?.special || (members.some(item => item.special === "Yes") ? "Yes" : "No"), org: direct?.org || team?.org || members[0]?.org || "", country: direct?.country || team?.country || members[0]?.country || "", region: direct?.region || team?.region || members[0]?.region || "" };
  }
  function doubleMembers(team) { return team ? [team.one, team.two].filter(Boolean).filter(id => !String(id).startsWith("parent:")) : []; }
  function relayMembers(team) { return team?.members || []; }
  function appliesFilters(row, filters) {
    if (filters.participantType && filters.participantType !== "all" && row.type !== filters.participantType) return false;
    if (filters.division && filters.division !== "all" && row.division !== filters.division) return false;
    if (filters.event && filters.event !== "all" && normalizedEvent(row.event) !== normalizedEvent(filters.event)) return false;
    if (filters.category === "normal" && row.special === "Yes") return false;
    if (filters.category === "special" && row.special !== "Yes") return false;
    if (filters.gender === "M" && row.gender !== "M") return false;
    if (filters.gender === "F" && row.gender !== "F") return false;
    return ["org", "country", "region"].every(key => !filters[key] || row[key] === filters[key]);
  }
  function finalResultRows(state, filters = {}) {
    return stageResultRows(state, "Finals", filters);
  }
  // The Finals DTO is deliberately stage-aware so Preliminary reports use the
  // same classification, participant metadata, filters and tie handling.
  function stageResultRows(state, stage, filters = {}) {
    return state.results.filter(result => String(result.stage || "").toLowerCase() === String(stage || "").toLowerCase()).map(result => {
      const meta = participantMeta(state, result.type, result.participant);
      const classification = classifyResult(result);
      return { ...meta, result, event: result.event, ...classification, tieKey: finalTieKey(result), attempts: finiteAttempts(result), resultStatus: classification.status };
    }).filter(row => appliesFilters(row, filters));
  }
  function allAroundRows(state, filters = {}) {
    return stageAllAroundRows(state, "Finals", filters);
  }
  function stageAllAroundRows(state, stage, filters = {}) {
    const byParticipant = new Map();
    state.results.filter(result => String(result.stage || "").toLowerCase() === String(stage || "").toLowerCase() && result.type === "Individual" && requiredAllAroundEvents.includes(normalizedEvent(result.event))).forEach(result => {
      const key = result.participant;
      if (!byParticipant.has(key)) byParticipant.set(key, {});
      const event = normalizedEvent(result.event), classification = classifyResult(result), old = byParticipant.get(key)[event];
      if (!old || (classification.status === "valid" && classification.bestValidTime < old.bestValidTime)) byParticipant.get(key)[event] = classification;
    });
    return state.stackers.map(stacker => [stacker.id, byParticipant.get(stacker.id) || {}]).map(([participant, events]) => {
      const meta = participantMeta(state, "Individual", participant);
      const eligible = requiredAllAroundEvents.every(event => events[event]?.status === "valid");
      const total = eligible ? requiredAllAroundEvents.reduce((sum, event) => sum + events[event].bestValidTime, 0) : null;
      return { ...meta, event: "All-Around", events, resultStatus: eligible ? "valid" : "ineligible", bestValidTime: total, tieKey: [total ?? Infinity, Infinity, Infinity], type: "Individual" };
    }).filter(row => appliesFilters(row, { ...filters, participantType: "Individual" }));
  }
  function placementRows(state, filters = {}) {
    return stagePlacementRows(state, "Finals", filters);
  }
  function stagePlacementRows(state, stage, filters = {}) {
    const groups = new Map();
    stageResultRows(state, stage, filters).forEach(row => {
      const key = [row.type, row.division, normalizedEvent(row.event)].join("|");
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(row);
    });
    return [...groups.values()].flatMap(group => {
      const valid = group.filter(row => row.resultStatus === "valid");
      const ranks = new Map(rankFinalRows(valid).map(row => [row.participant, row]));
      return group.map(row => ({ ...row, ...(ranks.get(row.participant) || { rank: null, tie: false }) }));
    });
  }
  function organizationCredits(state, filters = {}) {
    const placements = placementRows(state, filters).filter(row => Number.isFinite(row.rank));
    const organizations = new Map();
    placements.forEach(row => {
      const credited = new Set((row.type === "Individual" ? row.members : row.members).map(member => member.org).filter(Boolean));
      credited.forEach(org => {
        if (!organizations.has(org)) organizations.set(org, { organization: org, placements: [], participantIds: new Set(), individualEntries: new Set(), doublesTeams: new Set(), relayTeams: new Set() });
        const bucket = organizations.get(org);
        bucket.placements.push({ ...row, representedOrganization: org });
        row.members.filter(member => member.org === org).forEach(member => bucket.participantIds.add(member.id));
        if (row.type === "Individual") bucket.individualEntries.add(row.participant);
        if (row.type === "Doubles") bucket.doublesTeams.add(row.participant);
        if (row.type === "Timed Relay") bucket.relayTeams.add(row.participant);
      });
    });
    const rows = [...organizations.values()].map(bucket => {
      const counts = {}; bucket.placements.forEach(row => { counts[row.rank] = (counts[row.rank] || 0) + 1; });
      return { ...bucket, counts, totalPlacements: bucket.placements.length, participatingStackers: bucket.participantIds.size, individualEntries: bucket.individualEntries.size, doublesTeams: bucket.doublesTeams.size, relayTeams: bucket.relayTeams.size };
    });
    const sorted = rows.sort((left, right) => {
      const max = Math.max(...rows.map(row => Math.max(5, ...Object.keys(row.counts).map(Number))));
      for (let place = 1; place <= max; place += 1) { const difference = (right.counts[place] || 0) - (left.counts[place] || 0); if (difference) return difference; }
      return String(left.organization).localeCompare(String(right.organization), undefined, { sensitivity: "base" });
    });
    let rank = 0, previous = null;
    return sorted.map((row, index) => {
      const signature = Object.keys(row.counts).sort((a, b) => Number(a) - Number(b)).map(place => `${place}:${row.counts[place]}`).join("|");
      if (signature !== previous) rank = index + 1; previous = signature;
      return { ...row, rank };
    });
  }
  function qualificationSnapshot(state, sheet, options = {}) {
    const rows = [...sheet.prelimRows].map(row => ({ ...row, tieKey: [row.prelimTime, Infinity, Infinity] })).sort((a, b) => a.prelimTime - b.prelimTime || stableDisplay(a, b));
    let rank = 0, previous = null;
    const ranked = rows.map((row, index) => { if (row.prelimTime !== previous) rank = index + 1; previous = row.prelimTime; return { ...row, preliminaryRank: rank }; });
    const limit = Number(options.limit || sheet.advanceLimit || ranked.length);
    const boundary = ranked[Math.min(limit, ranked.length) - 1];
    const cutTie = boundary && ranked.some((row, index) => index >= limit && row.prelimTime === boundary.prelimTime);
    const selected = cutTie ? [] : ranked.slice(0, limit);
    return { id: options.id, competitionKey: options.competitionKey || "local", participantType: sheet.type, division: sheet.division, event: sheet.event, ruleVersion: "final-qualification-v1", sourcePreliminaryResults: ranked.map(row => ({ resultId: row.result?.id || "", participantId: row.participant, bestValidTime: row.prelimTime })), selectedQualifiers: selected.map((row, index) => ({ participantId: row.participant, preliminaryRank: row.preliminaryRank, finalSeed: index + 1, finalSheetId: sheet.id, heat: "" })), tieException: cutTie ? { required: true, decision: "", rationale: "Equal preliminary time crosses the configured qualification cutoff. An explicit approved exception is required." } : { required: false, decision: "configured-limit", rationale: "Configured qualification limit applied without a cutoff tie." }, generatedAtUtc: new Date().toISOString(), generatedBy: options.generatedBy || "", approvedAtUtc: "", approvedBy: "", status: "Draft", reconstructed: false };
  }
  global.StackMeetFinalsReports = { requiredAllAroundEvents, normalizedEvent, classifyResult, finalTieKey, compareKeys, rankFinalRows, participantMeta, appliesFilters, finalResultRows, stageResultRows, allAroundRows, stageAllAroundRows, placementRows, stagePlacementRows, organizationCredits, qualificationSnapshot, statusOrder };
})(window);
