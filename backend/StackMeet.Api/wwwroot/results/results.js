(() => {
  "use strict";

  const parts = location.pathname.split("/").filter(Boolean);
  const resultsIndex = parts.findIndex(part => part.toLowerCase() === "results");
  const competitionId = resultsIndex > 0 ? decodeURIComponent(parts[resultsIndex - 1]) : "";
  const section = resultsIndex >= 0 && parts[resultsIndex + 1] ? parts[resultsIndex + 1] : "Dashboard";
  const resultsRoot = competitionId ? `/${encodeURIComponent(competitionId)}/Results` : "";
  const endpoint = `/api/public/competitions/${encodeURIComponent(competitionId)}/results`;
  let lastVersion = "";
  let refreshInFlight = false;

  const el = id => document.getElementById(id);
  const text = (id, value) => { const node = el(id); if (node) node.textContent = value; };
  const show = (id, visible) => { const node = el(id); if (node) node.hidden = !visible; };

  document.querySelectorAll(".section-nav a").forEach(link => {
    const targetSection = link.dataset.section || "Dashboard";
    const suffix = targetSection.toLowerCase() === "dashboard"
      ? ""
      : `/${encodeURIComponent(targetSection)}`;
    link.href = `${resultsRoot}${suffix}`;
    link.classList.toggle("active", targetSection.toLowerCase() === section.toLowerCase());
  });

  const backLink = document.querySelector(".back-link");
  if (backLink) backLink.href = resultsRoot;

  if (!competitionId) {
    renderError("The competition ID is missing from this results link.");
    return;
  }

  el("retryButton")?.addEventListener("click", () => refresh(true));
  void refresh(true);
  void connectLiveUpdates();
  window.setInterval(() => void refresh(false), 15000);

  async function refresh(showLoader) {
    if (refreshInFlight) return;
    refreshInFlight = true;
    if (showLoader) {
      show("loadingState", true);
      show("errorState", false);
    }

    try {
      const response = await fetch(endpoint, {
        headers: { Accept: "application/json" },
        cache: "no-store"
      });
      if (!response.ok) {
        throw new Error(response.status === 404
          ? "This competition does not exist, is archived, or has no published results yet."
          : "The results service is temporarily unavailable.");
      }

      const payload = await response.json();
      const version = String(payload.lastUpdatedAt || "");
      if (showLoader || version !== lastVersion) render(payload);
      lastVersion = version;
      setConnection("live", "Live");
    } catch (error) {
      if (showLoader || !lastVersion) renderError(error.message);
      setConnection("offline", "Reconnecting");
    } finally {
      refreshInFlight = false;
    }
  }

  function render(payload) {
    show("loadingState", false);
    show("errorState", false);

    const competition = payload.competition || {};
    document.title = `${competition.name || "Competition"} · StackMeet Results`;
    text("competitionName", competition.name || payload.settings?.name || "Competition Results");
    text("competitionMeta", [formatDateRange(competition.startDate, competition.endDate), competition.venue].filter(Boolean).join(" · "));
    text("competitionCode", competition.id || competitionId);
    text("lastUpdated", `Updated ${formatClock(payload.lastUpdatedAt)}`);
    text("latestTime", formatClock(payload.lastUpdatedAt));

    const official = competition.isOfficial === true;
    const status = el("resultStatus");
    if (status) {
      status.textContent = official ? "Official results" : "Live results";
      status.className = `result-status ${official ? "official" : "live"}`;
    }
    const disclaimer = el("disclaimer");
    if (disclaimer && official) {
      disclaimer.innerHTML = "<strong>Official Results</strong><span>This competition is closed and the published results are official.</span>";
    }

    const selectedSection = section.toLowerCase();
    show("medals", false);
    if (selectedSection === "preliminary" || selectedSection === "prelims") {
      show("dashboard", false);
      show("finals", false);
      show("allAround", false);
      show("doubles", false);
      show("relay", false);
      show("comingSoon", false);
      show("preliminary", true);
      renderPreliminary(payload, official);
      return;
    }

    if (selectedSection === "finals" || selectedSection === "final") {
      show("dashboard", false);
      show("preliminary", false);
      show("allAround", false);
      show("doubles", false);
      show("relay", false);
      show("comingSoon", false);
      show("finals", true);
      renderFinals(payload, official);
      return;
    }

    if (selectedSection === "allaround" || selectedSection === "all-around") {
      show("dashboard", false);
      show("preliminary", false);
      show("finals", false);
      show("doubles", false);
      show("relay", false);
      show("comingSoon", false);
      show("allAround", true);
      renderAllAround(payload, official);
      return;
    }

    if (selectedSection === "doubles" || selectedSection === "double") {
      show("dashboard", false);
      show("preliminary", false);
      show("finals", false);
      show("allAround", false);
      show("relay", false);
      show("comingSoon", false);
      show("doubles", true);
      renderDoubles(payload, official);
      return;
    }

    if (selectedSection === "relay" || selectedSection === "relays") {
      show("dashboard", false);
      show("preliminary", false);
      show("finals", false);
      show("allAround", false);
      show("doubles", false);
      show("comingSoon", false);
      show("relay", true);
      renderRelay(payload, official);
      return;
    }

    if (selectedSection === "medals" || selectedSection === "medal-table") {
      show("dashboard", false);
      show("preliminary", false);
      show("finals", false);
      show("allAround", false);
      show("doubles", false);
      show("relay", false);
      show("comingSoon", false);
      show("medals", true);
      renderMedals(payload, official);
      return;
    }

    show("preliminary", false);
    show("finals", false);
    show("allAround", false);
    show("doubles", false);
    show("relay", false);
    if (selectedSection !== "dashboard") {
      show("dashboard", false);
      show("comingSoon", true);
      text("sectionTitle", displaySectionName(section));
      return;
    }

    show("comingSoon", false);
    show("dashboard", true);
    renderDashboard(payload);
  }

  function renderDashboard(payload) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const doubles = Array.isArray(payload.doubles) ? payload.doubles : [];
    const relays = Array.isArray(payload.relays) ? payload.relays : [];
    const latest = [...results].reverse();
    const current = latest[0] || null;
    const stageCounts = countBy(results, result => result.stage || "Unspecified");
    const participantIds = new Set(results.map(result => result.participant).filter(Boolean));

    text("resultCount", String(results.length));
    text("participantCount", `${participantIds.size} participant${participantIds.size === 1 ? "" : "s"}`);
    text("currentStage", current?.stage || "Preliminary");
    text("currentEvent", current ? [current.type, current.event].filter(Boolean).join(" · ") : "Waiting for the first result");

    const eligible = Math.max(stackers.length, participantIds.size);
    const percentage = eligible ? Math.min(100, Math.round((participantIds.size / eligible) * 100)) : 0;
    text("progressText", eligible ? `${participantIds.size} / ${eligible} participants` : "0 results");
    const progress = el("progressBar");
    if (progress) progress.style.width = `${percentage}%`;

    renderSectionReadiness(payload, competitionIsOfficial(payload));
    renderLeader(current, results, { stackers, doubles, relays });
    renderLatest(latest.slice(0, 6), { stackers, doubles, relays });
    renderStageStats(stageCounts, results.length);
  }

  function renderSectionReadiness(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const individualPreliminary = results.filter(result =>
      isPreliminaryStage(result.stage) && isIndividualType(result.type));
    const individualFinals = results.filter(result =>
      isFinalStage(result.stage) && isIndividualType(result.type));
    const doublesResults = results.filter(result => isDoublesType(result.type));
    const relayResults = results.filter(result => isRelayType(result.type));
    const allAroundParticipants = new Set();
    const allAroundGroups = new Map();

    results
      .filter(result => isIndividualType(result.type))
      .forEach(result => {
        const event = allAroundEventKey(result.event);
        const best = bestTime(result);
        if (!event || !Number.isFinite(best)) return;
        const participant = String(result.participant || "");
        if (!participant) return;
        const stage = isFinalStage(result.stage) ? "finals" : isPreliminaryStage(result.stage) ? "preliminary" : "";
        if (!stage) return;
        const key = `${participant}::${stage}`;
        if (!allAroundGroups.has(key)) allAroundGroups.set(key, new Set());
        allAroundGroups.get(key).add(event);
      });
    allAroundGroups.forEach((events, key) => {
      if (ALL_AROUND_EVENTS.every(event => events.has(event))) {
        allAroundParticipants.add(key.split("::")[0]);
      }
    });

    const finalPodiumSources = results.filter(result =>
      isFinalStage(result.stage) && Number.isFinite(bestTime(result)));

    const sections = [
      { route: "Preliminary", label: "Preliminary", description: "Division and event rankings", count: individualPreliminary.length, noun: "result" },
      { route: "Finals", label: "Finals", description: "Championship rankings and podiums", count: individualFinals.length, noun: "result" },
      { route: "AllAround", label: "All Around", description: "Combined three-event standings", count: allAroundParticipants.size, noun: "complete stacker" },
      { route: "Doubles", label: "Doubles", description: "Normal and child/parent teams", count: doublesResults.length, noun: "result" },
      { route: "Relay", label: "Relay", description: "Team relay standings", count: relayResults.length, noun: "result" },
      { route: "Medals", label: "Medal Table", description: "Final podiums by organization", count: finalPodiumSources.length, noun: "final entry" }
    ];

    const grid = el("sectionStatusGrid");
    if (!grid) return;
    grid.replaceChildren();

    sections.forEach(item => {
      const available = item.count > 0;
      const state = available ? (official ? "official" : "live") : "not-started";
      const statusLabel = available ? (official ? "Official" : "Live") : "Not started";
      const countLabel = available
        ? `${item.count} ${item.noun}${item.count === 1 ? "" : "s"}`
        : "Waiting for results";
      const link = make("a", "section-status-card");
      link.href = `${resultsRoot}/${item.route}`;
      link.dataset.status = state;
      link.setAttribute("aria-label", `${item.label}: ${statusLabel}. ${countLabel}.`);
      const copy = make("span", "section-status-copy");
      copy.append(make("strong", "", item.label), make("span", "", item.description));
      const meta = make("span", "section-status-meta");
      meta.append(make("span", `section-state ${state}`, statusLabel), make("span", "section-count", countLabel));
      link.append(copy, meta, make("span", "section-status-arrow", "→"));
      grid.append(link);
    });
  }

  function competitionIsOfficial(payload) {
    return payload?.competition?.isOfficial === true;
  }

  function renderLeader(current, results, lookup) {
    const card = el("leaderCard");
    if (!card) return;
    card.replaceChildren();

    if (!current) {
      card.className = "leader-card empty";
      card.append(make("div", "avatar", "—"), makePerson("No result entered yet", "Live standings will appear here."), make("b", "", "—"));
      text("leaderEvent", "Awaiting results");
      return;
    }

    const group = results
      .filter(result => result.stage === current.stage && result.type === current.type && result.event === current.event)
      .map(result => ({ result, best: bestTime(result) }))
      .filter(row => Number.isFinite(row.best))
      .sort((a, b) => a.best - b.best);
    const leader = group[0];
    text("leaderEvent", [current.stage, current.type, current.event].filter(Boolean).join(" · "));

    if (!leader) {
      card.append(make("div", "avatar", "—"), makePerson("No valid time yet", "The current event is awaiting a completed time."), make("b", "", "—"));
      return;
    }

    const participant = participantMeta(leader.result, lookup);
    card.className = "leader-card";
    card.append(
      make("div", "avatar", initials(participant.name)),
      makePerson(participant.name, participant.detail),
      make("b", "", formatTime(leader.best))
    );
  }

  function renderLatest(results, lookup) {
    const list = el("latestResults");
    if (!list) return;
    list.replaceChildren();

    if (!results.length) {
      const item = document.createElement("li");
      item.append(make("span", "latest-rank", "—"), makePerson("No results published", "New saves will appear automatically."), make("span", "latest-time", "—"));
      list.append(item);
      return;
    }

    results.forEach((result, index) => {
      const participant = participantMeta(result, lookup);
      const item = document.createElement("li");
      item.append(
        make("span", "latest-rank", String(index + 1).padStart(2, "0")),
        makePerson(participant.name, [result.stage, result.event, participant.detail].filter(Boolean).join(" · ")),
        make("span", "latest-time", formatTime(bestTime(result)))
      );
      list.append(item);
    });
  }

  function renderPreliminary(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const preliminary = results.filter(result =>
      isPreliminaryStage(result.stage) && isIndividualType(result.type));

    const groups = new Map();
    preliminary.forEach(result => {
      const stacker = stackerById.get(String(result.participant)) || {};
      const division = String(stacker.division || "Open / Unassigned").trim();
      const event = String(result.event || "Event").trim();
      if (!groups.has(division)) groups.set(division, new Map());
      if (!groups.get(division).has(event)) groups.get(division).set(event, []);
      groups.get(division).get(event).push({ result, stacker, best: bestTime(result) });
    });

    const container = el("preliminaryGroups");
    if (!container) return;
    container.replaceChildren();
    const eventCount = [...groups.values()].reduce((sum, events) => sum + events.size, 0);
    text("preliminarySummary", groups.size
      ? `${groups.size} division${groups.size === 1 ? "" : "s"} · ${eventCount} event${eventCount === 1 ? "" : "s"}`
      : "No preliminary results yet");

    if (!groups.size) {
      const empty = make("section", "panel empty-state compact");
      empty.append(
        make("span", "empty-icon clock", "↗"),
        make("h2", "", "Preliminary results are not available yet"),
        make("p", "", "This page will update automatically when officials publish the first preliminary result.")
      );
      container.append(empty);
      return;
    }

    [...groups.entries()]
      .sort(([left], [right]) => naturalCompare(left, right))
      .forEach(([division, events]) => {
        const divisionSection = make("section", "panel preliminary-division");
        const heading = make("div", "division-heading");
        const titleBlock = make("div", "");
        titleBlock.append(make("span", "eyebrow", "Division"), make("h2", "", division));
        const entryCount = [...events.values()].reduce((sum, rows) => sum + rows.length, 0);
        heading.append(titleBlock, make("span", "division-count", `${entryCount} entr${entryCount === 1 ? "y" : "ies"}`));
        divisionSection.append(heading);

        const eventList = make("div", "event-list");
        [...events.entries()]
          .sort(([left], [right]) => naturalCompare(left, right))
          .forEach(([eventName, rows]) => eventList.append(renderPreliminaryEvent(eventName, rows, official)));
        divisionSection.append(eventList);
        container.append(divisionSection);
      });
  }

  function renderPreliminaryEvent(eventName, rows, official) {
    const card = make("article", "results-event");
    const heading = make("div", "event-heading");
    const eventCopy = make("div", "");
    eventCopy.append(make("span", "eyebrow", "Event"), make("h3", "", eventName));
    heading.append(eventCopy, make("span", `event-state ${official ? "official" : "provisional"}`, official ? "Official" : "Provisional"));
    card.append(heading);

    rows.sort((left, right) => {
      const leftValid = Number.isFinite(left.best);
      const rightValid = Number.isFinite(right.best);
      if (leftValid !== rightValid) return leftValid ? -1 : 1;
      if (leftValid && left.best !== right.best) return left.best - right.best;
      return naturalCompare(left.stacker.name || left.result.participant, right.stacker.name || right.result.participant);
    });

    const tableWrap = make("div", "results-table-wrap");
    const table = make("table", "results-table");
    const head = document.createElement("thead");
    const headRow = document.createElement("tr");
    ["Rank", "Stacker", "Organization", "Best", "Status"].forEach(label => headRow.append(make("th", "", label)));
    head.append(headRow);
    table.append(head);

    const body = document.createElement("tbody");
    let previousBest = Number.NaN;
    let previousRank = 0;
    rows.forEach((row, index) => {
      const valid = Number.isFinite(row.best);
      const rank = valid && row.best === previousBest ? previousRank : index + 1;
      if (valid) {
        previousBest = row.best;
        previousRank = rank;
      }

      const tr = document.createElement("tr");
      const rankCell = make("td", "rank-cell", valid ? String(rank) : "—");
      const nameCell = make("td", "stacker-cell");
      nameCell.append(
        make("strong", "", row.stacker.name || row.result.participant || "Stacker"),
        make("span", "", row.result.participant || "")
      );
      tr.append(
        rankCell,
        nameCell,
        make("td", "organization-cell", row.stacker.org || "Independent"),
        make("td", `time-cell ${valid ? "" : "scr"}`, formatTime(row.best)),
        make("td", "status-cell", official ? "Official" : "Provisional")
      );
      body.append(tr);
    });
    table.append(body);
    tableWrap.append(table);
    card.append(tableWrap);
    return card;
  }

  function renderFinals(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const finals = results.filter(result =>
      isFinalStage(result.stage) && isIndividualType(result.type));

    const groups = new Map();
    finals.forEach(result => {
      const stacker = stackerById.get(String(result.participant)) || {};
      const division = String(stacker.division || "Open / Unassigned").trim();
      const event = String(result.event || "Event").trim();
      if (!groups.has(division)) groups.set(division, new Map());
      if (!groups.get(division).has(event)) groups.get(division).set(event, []);
      groups.get(division).get(event).push({ result, stacker, best: bestTime(result) });
    });

    const container = el("finalsGroups");
    if (!container) return;
    container.replaceChildren();
    const eventCount = [...groups.values()].reduce((sum, events) => sum + events.size, 0);
    text("finalsSummary", groups.size
      ? `${groups.size} division${groups.size === 1 ? "" : "s"} · ${eventCount} event${eventCount === 1 ? "" : "s"}`
      : "No final results yet");

    if (!groups.size) {
      const empty = make("section", "panel empty-state compact");
      empty.append(
        make("span", "empty-icon clock", "↗"),
        make("h2", "", "Final results are not available yet"),
        make("p", "", "This page will update automatically when officials publish the first final result.")
      );
      container.append(empty);
      return;
    }

    [...groups.entries()]
      .sort(([left], [right]) => naturalCompare(left, right))
      .forEach(([division, events]) => {
        const divisionSection = make("section", "panel preliminary-division finals-division");
        const heading = make("div", "division-heading finals-heading");
        const titleBlock = make("div", "");
        titleBlock.append(make("span", "eyebrow", "Division"), make("h2", "", division));
        const entryCount = [...events.values()].reduce((sum, rows) => sum + rows.length, 0);
        heading.append(titleBlock, make("span", "division-count", `${entryCount} entr${entryCount === 1 ? "y" : "ies"}`));
        divisionSection.append(heading);

        const eventList = make("div", "event-list");
        [...events.entries()]
          .sort(([left], [right]) => naturalCompare(left, right))
          .forEach(([eventName, rows]) => eventList.append(renderFinalEvent(eventName, rows, official)));
        divisionSection.append(eventList);
        container.append(divisionSection);
      });
  }

  function renderFinalEvent(eventName, rows, official) {
    const card = make("article", "results-event final-event");
    const heading = make("div", "event-heading");
    const eventCopy = make("div", "");
    eventCopy.append(make("span", "eyebrow", "Final event"), make("h3", "", eventName));
    heading.append(eventCopy, make("span", `event-state ${official ? "official" : "provisional"}`, official ? "Official" : "Provisional"));
    card.append(heading);

    rows.sort((left, right) => {
      const leftValid = Number.isFinite(left.best);
      const rightValid = Number.isFinite(right.best);
      if (leftValid !== rightValid) return leftValid ? -1 : 1;
      if (leftValid && left.best !== right.best) return left.best - right.best;
      return naturalCompare(left.stacker.name || left.result.participant, right.stacker.name || right.result.participant);
    });

    const tableWrap = make("div", "results-table-wrap");
    const table = make("table", "results-table finals-table");
    const head = document.createElement("thead");
    const headRow = document.createElement("tr");
    ["Place", "Stacker", "Organization", "Best", "Status"].forEach(label => headRow.append(make("th", "", label)));
    head.append(headRow);
    table.append(head);

    const body = document.createElement("tbody");
    let previousBest = Number.NaN;
    let previousRank = 0;
    rows.forEach((row, index) => {
      const valid = Number.isFinite(row.best);
      const rank = valid && row.best === previousBest ? previousRank : index + 1;
      if (valid) {
        previousBest = row.best;
        previousRank = rank;
      }

      const tr = document.createElement("tr");
      if (valid && rank <= 3) tr.className = `medal-row medal-${rank}`;
      const rankCell = make("td", `rank-cell ${valid && rank <= 3 ? "medal-rank" : ""}`, valid ? medalPlace(rank) : "—");
      const nameCell = make("td", "stacker-cell");
      nameCell.append(
        make("strong", "", row.stacker.name || row.result.participant || "Stacker"),
        make("span", "", row.result.participant || "")
      );
      tr.append(
        rankCell,
        nameCell,
        make("td", "organization-cell", row.stacker.org || "Independent"),
        make("td", `time-cell ${valid ? "" : "scr"}`, formatTime(row.best)),
        make("td", "status-cell", official ? "Official" : "Provisional")
      );
      body.append(tr);
    });
    table.append(body);
    tableWrap.append(table);
    card.append(tableWrap);
    return card;
  }

  function renderAllAround(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const stages = [
      { key: "finals", label: "Final", matches: isFinalStage },
      { key: "preliminary", label: "Preliminary", matches: isPreliminaryStage }
    ];
    const rowsByStageAndDivision = new Map();

    results
      .filter(result => isIndividualType(result.type))
      .forEach(result => {
        const stage = stages.find(item => item.matches(result.stage));
        const eventKey = allAroundEventKey(result.event);
        const best = bestTime(result);
        if (!stage || !eventKey || !Number.isFinite(best)) return;

        const stacker = stackerById.get(String(result.participant)) || {};
        const participantId = String(result.participant || "");
        const division = String(stacker.division || "Open / Unassigned").trim();
        const stageDivisionKey = `${stage.key}\u0000${division}`;
        if (!rowsByStageAndDivision.has(stageDivisionKey)) {
          rowsByStageAndDivision.set(stageDivisionKey, { stage, division, participants: new Map() });
        }

        const participants = rowsByStageAndDivision.get(stageDivisionKey).participants;
        if (!participants.has(participantId)) {
          participants.set(participantId, { participantId, stacker, times: {} });
        }
        const row = participants.get(participantId);
        if (!Number.isFinite(row.times[eventKey]) || best < row.times[eventKey]) row.times[eventKey] = best;
      });

    const completeGroups = [...rowsByStageAndDivision.values()]
      .map(group => ({
        ...group,
        rows: [...group.participants.values()]
          .filter(row => ALL_AROUND_EVENTS.every(event => Number.isFinite(row.times[event.key])))
          .map(row => ({
            ...row,
            total: ALL_AROUND_EVENTS.reduce((sum, event) => sum + row.times[event.key], 0)
          }))
      }))
      .filter(group => group.rows.length);

    const divisions = new Map();
    completeGroups.forEach(group => {
      if (!divisions.has(group.division)) divisions.set(group.division, []);
      divisions.get(group.division).push(group);
    });

    const selectedGroups = [...divisions.values()].map(groups =>
      groups.find(group => group.stage.key === "finals") || groups[0]);

    const container = el("allAroundGroups");
    if (!container) return;
    container.replaceChildren();
    const stackerCount = selectedGroups.reduce((sum, group) => sum + group.rows.length, 0);
    text("allAroundSummary", selectedGroups.length
      ? `${selectedGroups.length} division${selectedGroups.length === 1 ? "" : "s"} · ${stackerCount} complete stacker${stackerCount === 1 ? "" : "s"}`
      : "Waiting for three-event totals");

    if (!selectedGroups.length) {
      const empty = make("section", "panel empty-state compact");
      empty.append(
        make("span", "empty-icon clock", "↗"),
        make("h2", "", "All-Around standings are not available yet"),
        make("p", "", "A stacker appears after valid 3-3-3, 3-6-3, and Cycle results have all been published for the same stage.")
      );
      container.append(empty);
      return;
    }

    selectedGroups
      .sort((left, right) => naturalCompare(left.division, right.division))
      .forEach(group => container.append(renderAllAroundDivision(group, official)));
  }

  const ALL_AROUND_EVENTS = [
    { key: "333", label: "3-3-3" },
    { key: "363", label: "3-6-3" },
    { key: "cycle", label: "Cycle" }
  ];

  function renderAllAroundDivision(group, official) {
    const section = make("section", "panel preliminary-division allaround-division");
    const heading = make("div", "division-heading allaround-heading");
    const titleBlock = make("div", "");
    titleBlock.append(make("span", "eyebrow", `${group.stage.label} all-around`), make("h2", "", group.division));
    heading.append(titleBlock, make("span", "division-count", `${group.rows.length} complete`));
    section.append(heading);

    group.rows.sort((left, right) =>
      left.total - right.total ||
      naturalCompare(left.stacker.name || left.participantId, right.stacker.name || right.participantId));

    const wrap = make("div", "results-table-wrap allaround-table-wrap");
    const table = make("table", "results-table finals-table allaround-table");
    const head = document.createElement("thead");
    const headRow = document.createElement("tr");
    ["Place", "Stacker", "Organization", "3-3-3", "3-6-3", "Cycle", "Total", "Status"]
      .forEach(label => headRow.append(make("th", "", label)));
    head.append(headRow);
    table.append(head);

    const body = document.createElement("tbody");
    let previousTotal = Number.NaN;
    let previousRank = 0;
    group.rows.forEach((row, index) => {
      const rank = row.total === previousTotal ? previousRank : index + 1;
      previousTotal = row.total;
      previousRank = rank;

      const tr = document.createElement("tr");
      if (rank <= 3) tr.className = `medal-row medal-${rank}`;
      const nameCell = make("td", "stacker-cell");
      nameCell.append(
        make("strong", "", row.stacker.name || row.participantId || "Stacker"),
        make("span", "", row.participantId)
      );
      tr.append(
        make("td", `rank-cell ${rank <= 3 ? "medal-rank" : ""}`, medalPlace(rank)),
        nameCell,
        make("td", "organization-cell", row.stacker.org || "Independent"),
        allAroundTimeCell("3-3-3", row.times["333"], "event-time-333"),
        allAroundTimeCell("3-6-3", row.times["363"], "event-time-363"),
        allAroundTimeCell("Cycle", row.times.cycle, "event-time-cycle"),
        allAroundTimeCell("Total", row.total, "allaround-total"),
        make("td", "status-cell", official ? "Official" : "Provisional")
      );
      body.append(tr);
    });
    table.append(body);
    wrap.append(table);
    section.append(wrap);
    return section;
  }

  function allAroundTimeCell(label, value, className) {
    const cell = make("td", `time-cell ${className}`, formatTime(value));
    cell.dataset.label = label;
    return cell;
  }

  function allAroundEventKey(value) {
    const normalized = String(value || "").toLowerCase().replace(/[^a-z0-9]/g, "");
    if (normalized === "333") return "333";
    if (normalized === "363") return "363";
    if (normalized === "cycle" || normalized === "thecycle") return "cycle";
    return "";
  }

  function renderMedals(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const doubles = Array.isArray(payload.doubles) ? payload.doubles : [];
    const relays = Array.isArray(payload.relays) ? payload.relays : [];
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const doublesById = new Map(doubles.map(team => [String(team.id), team]));
    const relaysById = new Map(relays.map(team => [String(team.id), team]));
    const groups = new Map();

    results.filter(result => isFinalStage(result.stage)).forEach(result => {
      const best = bestTime(result);
      if (!Number.isFinite(best)) return;

      const participantId = String(result.participant || "");
      let type = "";
      let division = "Open / Unassigned";
      if (isIndividualType(result.type)) {
        type = "Individual";
        division = String(stackerById.get(participantId)?.division || division).trim();
      } else if (isDoublesType(result.type)) {
        type = "Doubles";
        const team = doublesById.get(participantId) || {};
        division = String(team.customDivision || team.division || team.type || division).trim();
      } else if (isRelayType(result.type)) {
        type = "Relay";
        const team = relaysById.get(participantId) || {};
        division = String(team.timedRelayDivision || team.division || division).trim();
      } else {
        return;
      }

      const eventName = String(result.event || type).trim();
      const groupKey = [type, division, eventName].join("\u0000");
      if (!groups.has(groupKey)) groups.set(groupKey, new Map());
      const participants = groups.get(groupKey);
      const current = participants.get(participantId);
      if (!current || best < current.best) {
        participants.set(participantId, {
          participantId,
          best,
          organization: medalOrganization(result, { stackerById, doublesById, relaysById })
        });
      }
    });

    const medalsByOrganization = new Map();
    groups.forEach(participants => {
      const rows = [...participants.values()].sort((left, right) =>
        left.best - right.best || naturalCompare(left.participantId, right.participantId));
      let previousBest = Number.NaN;
      let previousRank = 0;
      rows.forEach((row, index) => {
        const rank = row.best === previousBest ? previousRank : index + 1;
        previousBest = row.best;
        previousRank = rank;
        if (rank > 3) return;
        const organization = row.organization || "Independent";
        if (!medalsByOrganization.has(organization)) {
          medalsByOrganization.set(organization, { organization, gold: 0, silver: 0, bronze: 0 });
        }
        const medal = rank === 1 ? "gold" : rank === 2 ? "silver" : "bronze";
        medalsByOrganization.get(organization)[medal] += 1;
      });
    });

    const rows = [...medalsByOrganization.values()]
      .map(row => ({ ...row, total: row.gold + row.silver + row.bronze }))
      .sort((left, right) =>
        right.gold - left.gold ||
        right.silver - left.silver ||
        right.bronze - left.bronze ||
        naturalCompare(left.organization, right.organization));

    const body = el("medalRows");
    const table = document.querySelector(".medal-table");
    const empty = el("medalsEmpty");
    if (!body || !table || !empty) return;
    body.replaceChildren();
    table.hidden = rows.length === 0;
    empty.hidden = rows.length !== 0;

    const medalTotal = rows.reduce((sum, row) => sum + row.total, 0);
    text("medalsSummary", rows.length
      ? `${rows.length} organization${rows.length === 1 ? "" : "s"} · ${medalTotal} medal${medalTotal === 1 ? "" : "s"} · ${official ? "Official" : "Provisional"}`
      : "No final medals yet");

    let previousSignature = "";
    let previousRank = 0;
    rows.forEach((row, index) => {
      const signature = `${row.gold}:${row.silver}:${row.bronze}`;
      const rank = signature === previousSignature ? previousRank : index + 1;
      previousSignature = signature;
      previousRank = rank;
      const tr = document.createElement("tr");
      tr.append(
        make("td", "rank-cell", String(rank)),
        make("td", "medal-organization", row.organization),
        medalCountCell("Gold", row.gold, "gold"),
        medalCountCell("Silver", row.silver, "silver"),
        medalCountCell("Bronze", row.bronze, "bronze"),
        medalCountCell("Total", row.total, "total")
      );
      body.append(tr);
    });
  }

  function medalOrganization(result, lookup) {
    const participantId = String(result.participant || "");
    if (isIndividualType(result.type)) {
      return lookup.stackerById.get(participantId)?.org || "Independent";
    }

    const team = isDoublesType(result.type)
      ? lookup.doublesById.get(participantId) || {}
      : lookup.relaysById.get(participantId) || {};
    const memberIds = isDoublesType(result.type)
      ? [team.one, team.two]
      : (Array.isArray(team.members) ? team.members : []);
    const organizations = [...new Set(memberIds
      .map(id => lookup.stackerById.get(String(id))?.org)
      .filter(Boolean))]
      .sort(naturalCompare);
    return organizations.join(" / ") || team.org || team.region || team.country || "Independent";
  }

  function medalCountCell(label, value, type) {
    const cell = make("td", `medal-count medal-count-${type}`, String(value));
    cell.dataset.label = label;
    return cell;
  }

  function renderDoubles(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const doublesTeams = Array.isArray(payload.doubles) ? payload.doubles : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const teamById = new Map(doublesTeams.map(team => [String(team.id), team]));
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const groups = new Map();

    results
      .filter(result => isDoublesType(result.type))
      .forEach(result => {
        const team = teamById.get(String(result.participant)) || {};
        const stage = doublesStage(result.stage);
        const division = String(team.customDivision || team.division || team.type || "Open / Unassigned").trim();
        const event = String(result.event || "Doubles").trim();
        const key = `${stage.key}\u0000${division}`;
        if (!groups.has(key)) groups.set(key, { stage, division, events: new Map() });
        const events = groups.get(key).events;
        if (!events.has(event)) events.set(event, []);
        events.get(event).push({
          result,
          team,
          meta: doublesTeamMeta(team, stackerById, result.participant),
          best: bestTime(result)
        });
      });

    const orderedGroups = [...groups.values()].sort((left, right) =>
      left.stage.order - right.stage.order ||
      naturalCompare(left.division, right.division));

    const container = el("doublesGroups");
    if (!container) return;
    container.replaceChildren();
    const entryCount = orderedGroups.reduce(
      (sum, group) => sum + [...group.events.values()].reduce((count, rows) => count + rows.length, 0),
      0);
    text("doublesSummary", orderedGroups.length
      ? `${orderedGroups.length} stage/division group${orderedGroups.length === 1 ? "" : "s"} · ${entryCount} entr${entryCount === 1 ? "y" : "ies"}`
      : "No doubles results yet");

    if (!orderedGroups.length) {
      const empty = make("section", "panel empty-state compact");
      empty.append(
        make("span", "empty-icon clock", "↗"),
        make("h2", "", "Doubles results are not available yet"),
        make("p", "", "Normal and child/parent Doubles standings will appear automatically after officials publish results.")
      );
      container.append(empty);
      return;
    }

    orderedGroups.forEach(group => container.append(renderDoublesGroup(group, official)));
  }

  function renderDoublesGroup(group, official) {
    const section = make("section", "panel preliminary-division doubles-division");
    const heading = make("div", "division-heading doubles-heading");
    const titleBlock = make("div", "");
    titleBlock.append(
      make("span", "eyebrow", `${group.stage.label} doubles`),
      make("h2", "", group.division)
    );
    const entryCount = [...group.events.values()].reduce((sum, rows) => sum + rows.length, 0);
    heading.append(titleBlock, make("span", "division-count", `${entryCount} entr${entryCount === 1 ? "y" : "ies"}`));
    section.append(heading);

    const eventList = make("div", "event-list");
    [...group.events.entries()]
      .sort(([left], [right]) => naturalCompare(left, right))
      .forEach(([eventName, rows]) =>
        eventList.append(renderDoublesEvent(eventName, rows, group.stage.isFinal, official)));
    section.append(eventList);
    return section;
  }

  function renderDoublesEvent(eventName, rows, isFinal, official) {
    const card = make("article", "results-event doubles-event");
    const heading = make("div", "event-heading");
    const eventCopy = make("div", "");
    eventCopy.append(make("span", "eyebrow", "Doubles event"), make("h3", "", eventName));
    heading.append(
      eventCopy,
      make("span", `event-state ${official ? "official" : "provisional"}`, official ? "Official" : "Provisional")
    );
    card.append(heading);

    rows.sort((left, right) => {
      const leftValid = Number.isFinite(left.best);
      const rightValid = Number.isFinite(right.best);
      if (leftValid !== rightValid) return leftValid ? -1 : 1;
      if (leftValid && left.best !== right.best) return left.best - right.best;
      return naturalCompare(left.meta.name, right.meta.name);
    });

    const wrap = make("div", "results-table-wrap doubles-table-wrap");
    const table = make("table", "results-table finals-table doubles-table");
    const head = document.createElement("thead");
    const headRow = document.createElement("tr");
    ["Place", "Team", "Stackers", "Organization", "Best", "Status"]
      .forEach(label => headRow.append(make("th", "", label)));
    head.append(headRow);
    table.append(head);

    const body = document.createElement("tbody");
    let previousBest = Number.NaN;
    let previousRank = 0;
    rows.forEach((row, index) => {
      const valid = Number.isFinite(row.best);
      const rank = valid && row.best === previousBest ? previousRank : index + 1;
      if (valid) {
        previousBest = row.best;
        previousRank = rank;
      }

      const tr = document.createElement("tr");
      if (isFinal && valid && rank <= 3) tr.className = `medal-row medal-${rank}`;
      const teamCell = make("td", "team-cell");
      teamCell.append(
        make("strong", "", row.meta.name),
        make("span", "", row.team.id || row.result.participant || "")
      );
      const memberCell = make("td", "member-cell", row.meta.members);
      memberCell.dataset.label = "Stackers";
      tr.append(
        make("td", `rank-cell ${isFinal && valid && rank <= 3 ? "medal-rank" : ""}`,
          valid ? (isFinal ? medalPlace(rank) : String(rank)) : "—"),
        teamCell,
        memberCell,
        make("td", "organization-cell", row.meta.organization),
        make("td", `time-cell doubles-time ${valid ? "" : "scr"}`, formatTime(row.best)),
        make("td", "status-cell", official ? "Official" : "Provisional")
      );
      body.append(tr);
    });
    table.append(body);
    wrap.append(table);
    card.append(wrap);
    return card;
  }

  function doublesTeamMeta(team, stackerById, fallbackId) {
    const members = [team.one, team.two]
      .map(id => stackerById.get(String(id)) || {})
      .filter(stacker => stacker.name);
    const names = members.map(stacker => stacker.name);
    const organizations = [...new Set(members.map(stacker => stacker.org).filter(Boolean))];
    return {
      name: team.name || names.join(" & ") || fallbackId || "Doubles Team",
      members: names.join(" & ") || "Team members pending",
      organization: organizations.join(" / ") || team.region || team.country || "Independent"
    };
  }

  function doublesStage(value) {
    if (isFinalStage(value)) return { key: "finals", label: "Final", order: 0, isFinal: true };
    if (isPreliminaryStage(value)) return { key: "preliminary", label: "Preliminary", order: 1, isFinal: false };
    const label = String(value || "Results").trim() || "Results";
    return { key: label.toLowerCase(), label, order: 2, isFinal: false };
  }

  function isDoublesType(value) {
    return String(value || "").trim().toLowerCase().includes("double");
  }

  function renderRelay(payload, official) {
    const results = Array.isArray(payload.results) ? payload.results : [];
    const relayTeams = Array.isArray(payload.relays) ? payload.relays : [];
    const stackers = Array.isArray(payload.stackers) ? payload.stackers : [];
    const teamById = new Map(relayTeams.map(team => [String(team.id), team]));
    const stackerById = new Map(stackers.map(stacker => [String(stacker.id), stacker]));
    const groups = new Map();

    results
      .filter(result => isRelayType(result.type))
      .forEach(result => {
        const team = teamById.get(String(result.participant)) || {};
        const stage = relayStage(result.stage);
        const division = String(team.timedRelayDivision || team.division || "Open / Unassigned").trim();
        const event = String(result.event || "Timed Relay").trim();
        const key = `${stage.key}\u0000${division}`;
        if (!groups.has(key)) groups.set(key, { stage, division, events: new Map() });
        const events = groups.get(key).events;
        if (!events.has(event)) events.set(event, []);
        events.get(event).push({
          result,
          team,
          meta: relayTeamMeta(team, stackerById, result.participant),
          best: bestTime(result)
        });
      });

    const orderedGroups = [...groups.values()].sort((left, right) =>
      left.stage.order - right.stage.order ||
      naturalCompare(left.division, right.division));
    const container = el("relayGroups");
    if (!container) return;
    container.replaceChildren();
    const entryCount = orderedGroups.reduce(
      (sum, group) => sum + [...group.events.values()].reduce((count, rows) => count + rows.length, 0),
      0);
    text("relaySummary", orderedGroups.length
      ? `${orderedGroups.length} stage/division group${orderedGroups.length === 1 ? "" : "s"} · ${entryCount} entr${entryCount === 1 ? "y" : "ies"}`
      : "No relay results yet");

    if (!orderedGroups.length) {
      const empty = make("section", "panel empty-state compact");
      empty.append(
        make("span", "empty-icon clock", "↗"),
        make("h2", "", "Relay results are not available yet"),
        make("p", "", "Relay standings will appear automatically after officials publish the first team result.")
      );
      container.append(empty);
      return;
    }

    orderedGroups.forEach(group => container.append(renderRelayGroup(group, official)));
  }

  function renderRelayGroup(group, official) {
    const section = make("section", "panel preliminary-division relay-division");
    const heading = make("div", "division-heading relay-heading");
    const titleBlock = make("div", "");
    titleBlock.append(
      make("span", "eyebrow", `${group.stage.label} relay`),
      make("h2", "", group.division)
    );
    const entryCount = [...group.events.values()].reduce((sum, rows) => sum + rows.length, 0);
    heading.append(titleBlock, make("span", "division-count", `${entryCount} entr${entryCount === 1 ? "y" : "ies"}`));
    section.append(heading);

    const eventList = make("div", "event-list");
    [...group.events.entries()]
      .sort(([left], [right]) => naturalCompare(left, right))
      .forEach(([eventName, rows]) =>
        eventList.append(renderRelayEvent(eventName, rows, group.stage.isFinal, official)));
    section.append(eventList);
    return section;
  }

  function renderRelayEvent(eventName, rows, isFinal, official) {
    const card = make("article", "results-event relay-event");
    const heading = make("div", "event-heading");
    const eventCopy = make("div", "");
    eventCopy.append(make("span", "eyebrow", "Relay event"), make("h3", "", eventName));
    heading.append(
      eventCopy,
      make("span", `event-state ${official ? "official" : "provisional"}`, official ? "Official" : "Provisional")
    );
    card.append(heading);

    rows.sort((left, right) => {
      const leftValid = Number.isFinite(left.best);
      const rightValid = Number.isFinite(right.best);
      if (leftValid !== rightValid) return leftValid ? -1 : 1;
      if (leftValid && left.best !== right.best) return left.best - right.best;
      return naturalCompare(left.meta.name, right.meta.name);
    });

    const wrap = make("div", "results-table-wrap relay-table-wrap");
    const table = make("table", "results-table finals-table relay-table");
    const head = document.createElement("thead");
    const headRow = document.createElement("tr");
    ["Place", "Team", "Members", "Organization", "Best", "Status"]
      .forEach(label => headRow.append(make("th", "", label)));
    head.append(headRow);
    table.append(head);

    const body = document.createElement("tbody");
    let previousBest = Number.NaN;
    let previousRank = 0;
    rows.forEach((row, index) => {
      const valid = Number.isFinite(row.best);
      const rank = valid && row.best === previousBest ? previousRank : index + 1;
      if (valid) {
        previousBest = row.best;
        previousRank = rank;
      }

      const tr = document.createElement("tr");
      if (isFinal && valid && rank <= 3) tr.className = `medal-row medal-${rank}`;
      const teamCell = make("td", "team-cell");
      teamCell.append(
        make("strong", "", row.meta.name),
        make("span", "", row.team.id || row.result.participant || "")
      );
      const memberCell = make("td", "member-cell relay-members", row.meta.members);
      memberCell.dataset.label = "Members";
      tr.append(
        make("td", `rank-cell ${isFinal && valid && rank <= 3 ? "medal-rank" : ""}`,
          valid ? (isFinal ? medalPlace(rank) : String(rank)) : "—"),
        teamCell,
        memberCell,
        make("td", "organization-cell", row.meta.organization),
        make("td", `time-cell relay-time ${valid ? "" : "scr"}`, formatTime(row.best)),
        make("td", "status-cell", official ? "Official" : "Provisional")
      );
      body.append(tr);
    });
    table.append(body);
    wrap.append(table);
    card.append(wrap);
    return card;
  }

  function relayTeamMeta(team, stackerById, fallbackId) {
    const ids = Array.isArray(team.members) && team.members.length
      ? team.members
      : [team.one, team.two, team.three, team.four, team.five, team.six].filter(Boolean);
    const members = ids
      .map(id => stackerById.get(String(id)) || {})
      .filter(stacker => stacker.name);
    const names = members.map(stacker => stacker.name);
    const organizations = [...new Set(members.map(stacker => stacker.org).filter(Boolean))];
    return {
      name: team.name || fallbackId || "Relay Team",
      members: names.join(", ") || "Team members pending",
      organization: team.org || organizations.join(" / ") || team.region || team.country || "Independent"
    };
  }

  function relayStage(value) {
    if (isFinalStage(value)) return { key: "finals", label: "Final", order: 0, isFinal: true };
    if (isPreliminaryStage(value)) return { key: "preliminary", label: "Preliminary", order: 1, isFinal: false };
    const label = String(value || "Results").trim() || "Results";
    return { key: label.toLowerCase(), label, order: 2, isFinal: false };
  }

  function isRelayType(value) {
    return String(value || "").trim().toLowerCase().includes("relay");
  }

  function medalPlace(rank) {
    if (rank === 1) return "🥇 1";
    if (rank === 2) return "🥈 2";
    if (rank === 3) return "🥉 3";
    return String(rank);
  }

  function isFinalStage(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return normalized === "finals" || normalized === "final";
  }

  function isPreliminaryStage(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return normalized === "prelims" || normalized === "preliminary" || normalized === "prelim";
  }

  function isIndividualType(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return !normalized.includes("double") && !normalized.includes("relay");
  }

  function naturalCompare(left, right) {
    return String(left || "").localeCompare(String(right || ""), undefined, { numeric: true, sensitivity: "base" });
  }

  function renderStageStats(counts, total) {
    const container = el("stageStats");
    if (!container) return;
    container.replaceChildren();
    const preferred = ["Prelims", "Finals", "SOC"];
    const stages = [...new Set([...preferred, ...Object.keys(counts)])].filter(stage => preferred.includes(stage) || counts[stage]);
    stages.forEach(stage => {
      const count = counts[stage] || 0;
      const card = make("div", "stage-stat");
      card.append(make("span", "", stage === "SOC" ? "Stack of Champions" : stage), make("strong", "", String(count)), make("span", "", total ? `${Math.round(count / total * 100)}% of entered results` : "No results yet"));
      container.append(card);
    });
  }

  function participantMeta(result, lookup) {
    const type = String(result.type || "").toLowerCase();
    if (type === "doubles") {
      const team = lookup.doubles.find(item => item.id === result.participant) || {};
      const one = lookup.stackers.find(item => item.id === team.one)?.name;
      const two = lookup.stackers.find(item => item.id === team.two)?.name;
      return {
        name: team.name || [one, two].filter(Boolean).join(" & ") || result.participant || "Doubles Team",
        detail: team.customDivision || team.division || "Doubles"
      };
    }

    if (type.includes("relay")) {
      const team = lookup.relays.find(item => item.id === result.participant) || {};
      return {
        name: team.name || result.participant || "Relay Team",
        detail: team.timedRelayDivision || team.division || "Relay"
      };
    }

    const stacker = lookup.stackers.find(item => item.id === result.participant) || {};
    return {
      name: stacker.name || result.participant || "Stacker",
      detail: [stacker.division, stacker.org].filter(Boolean).join(" · ")
    };
  }

  function bestTime(result) {
    const attempts = Array.isArray(result.attempts)
      ? result.attempts.map(Number).filter(value => Number.isFinite(value) && value >= 0 && value < 999)
      : [];
    if (!attempts.length || Number(result.penalty) >= 999) return Number.NaN;
    const penalty = Number(result.penalty);
    return Math.min(...attempts) + (Number.isFinite(penalty) && penalty > 0 ? penalty : 0);
  }

  async function connectLiveUpdates() {
    if (!window.signalR) {
      setConnection("offline", "Auto refresh");
      return;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/results")
      .withAutomaticReconnect([0, 1500, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on("ResultsUpdated", () => void refresh(false));
    connection.onreconnecting(() => setConnection("connecting", "Reconnecting"));
    connection.onreconnected(async () => {
      await connection.invoke("JoinCompetition", competitionId);
      setConnection("live", "Live");
      void refresh(false);
    });
    connection.onclose(() => setConnection("offline", "Auto refresh"));

    try {
      await connection.start();
      await connection.invoke("JoinCompetition", competitionId);
      setConnection("live", "Live");
    } catch {
      setConnection("offline", "Auto refresh");
      window.setTimeout(connectLiveUpdates, 10000);
    }
  }

  function renderError(message) {
    show("loadingState", false);
    show("dashboard", false);
    show("preliminary", false);
    show("finals", false);
    show("allAround", false);
    show("doubles", false);
    show("relay", false);
    show("comingSoon", false);
    show("errorState", true);
    text("errorMessage", message);
  }

  function setConnection(state, label) {
    const node = el("connectionStatus");
    if (!node) return;
    node.dataset.state = state;
    const labelNode = node.querySelector("span:last-child");
    if (labelNode) labelNode.textContent = label;
  }

  function countBy(items, keySelector) {
    return items.reduce((counts, item) => {
      const key = keySelector(item);
      counts[key] = (counts[key] || 0) + 1;
      return counts;
    }, {});
  }

  function make(tag, className, value) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (value !== undefined) node.textContent = value;
    return node;
  }

  function makePerson(name, detail) {
    const wrapper = make("div", "latest-name");
    wrapper.append(make("strong", "", name), make("span", "", detail));
    return wrapper;
  }

  function initials(name) {
    return String(name || "").split(/\s+/).filter(Boolean).slice(0, 2).map(word => word[0]).join("").toUpperCase() || "—";
  }

  function formatTime(value) {
    return Number.isFinite(value) ? value.toFixed(3) : "SCR";
  }

  function formatClock(value) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "just now" : new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
  }

  function formatDateRange(start, end) {
    const startDate = dateOnly(start);
    const endDate = dateOnly(end);
    if (!startDate) return "";
    const format = new Intl.DateTimeFormat(undefined, { day: "numeric", month: "short", year: "numeric" });
    if (!endDate || startDate.getTime() === endDate.getTime()) return format.format(startDate);
    return `${format.format(startDate)} – ${format.format(endDate)}`;
  }

  function dateOnly(value) {
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value));
    return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
  }

  function displaySectionName(value) {
    return String(value).replace(/([a-z])([A-Z])/g, "$1 $2").replace(/-/g, " ");
  }
})();
