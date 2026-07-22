(() => {
  "use strict";

  const parts = location.pathname.split("/").filter(Boolean);
  const resultsIndex = parts.findIndex(part => part.toLowerCase() === "results");
  const competitionId = resultsIndex > 0 ? decodeURIComponent(parts[resultsIndex - 1]) : "";
  const section = resultsIndex >= 0 && parts[resultsIndex + 1] ? parts[resultsIndex + 1] : "Dashboard";
  const endpoint = `/api/public/competitions/${encodeURIComponent(competitionId)}/results`;
  let lastVersion = "";
  let refreshInFlight = false;

  const el = id => document.getElementById(id);
  const text = (id, value) => { const node = el(id); if (node) node.textContent = value; };
  const show = (id, visible) => { const node = el(id); if (node) node.hidden = !visible; };

  document.querySelectorAll(".section-nav a").forEach(link => {
    link.classList.toggle("active", link.dataset.section?.toLowerCase() === section.toLowerCase());
  });

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

    if (section.toLowerCase() !== "dashboard") {
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

    renderLeader(current, results, { stackers, doubles, relays });
    renderLatest(latest.slice(0, 6), { stackers, doubles, relays });
    renderStageStats(stageCounts, results.length);
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
