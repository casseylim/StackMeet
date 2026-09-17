(() => {
  "use strict";

  const parts = location.pathname.split("/").filter(Boolean);
  const resultsIndex = parts.findIndex(part => part.toLowerCase() === "results");
  const competitionId = resultsIndex > 0 ? decodeURIComponent(parts[resultsIndex - 1]) : "";
  if (!competitionId) return;

  const endpoint = `/api/public/competitions/${encodeURIComponent(competitionId)}/profile-links`;
  const resultsEndpoint = `/api/public/competitions/${encodeURIComponent(competitionId)}/results`;
  const approvedProfilePath = /^\/Stackers\/NDT-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{7}$/;
  let profileLinks = new Map();
  let stackerNames = new Map();
  let doublesMembers = new Map();
  let relayMembers = new Map();
  let decorateScheduled = false;

  const style = document.createElement("style");
  style.textContent = `
    .stacker-profile-link {
      color: inherit;
      font-weight: 700;
      text-decoration-line: underline;
      text-decoration-thickness: 1px;
      text-underline-offset: 3px;
      text-decoration-color: currentColor;
    }
    .stacker-profile-link:hover,
    .stacker-profile-link:focus-visible {
      text-decoration-thickness: 2px;
    }
    .stacker-profile-link:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 3px;
      border-radius: 3px;
    }
  `;
  document.head.append(style);

  function scheduleDecorate() {
    if (decorateScheduled) return;
    decorateScheduled = true;
    requestAnimationFrame(() => {
      decorateScheduled = false;
      decorateVisibleStackerNames();
    });
  }

  function directChild(element, selector) {
    return Array.from(element.children).find(child => child.matches(selector)) || null;
  }

  function plainNameNode(name) {
    const strong = document.createElement("strong");
    strong.textContent = name;
    return strong;
  }

  function linkedNameNode(name, profileUrl) {
    const link = document.createElement("a");
    link.className = "stacker-profile-link";
    link.href = profileUrl;
    link.textContent = name;
    return link;
  }

  function decorateCell(cell) {
    const participantNode = directChild(cell, "span");
    if (!participantNode) return;

    const participant = String(participantNode.textContent || "").trim();
    const profileUrl = profileLinks.get(participant) || "";
    const existingLink = directChild(cell, "a.stacker-profile-link");
    const existingStrong = directChild(cell, "strong");
    const nameNode = existingLink || existingStrong;
    if (!nameNode) return;

    const name = String(nameNode.textContent || "").trim();
    if (!name) return;

    if (profileUrl && approvedProfilePath.test(profileUrl)) {
      if (existingLink) {
        if (existingLink.getAttribute("href") !== profileUrl) existingLink.setAttribute("href", profileUrl);
        return;
      }
      existingStrong.replaceWith(linkedNameNode(name, profileUrl));
      return;
    }

    if (existingLink) existingLink.replaceWith(plainNameNode(name));
  }

  function memberIds(team, relay) {
    if (!team || typeof team !== "object") return [];
    const raw = relay && Array.isArray(team.members) && team.members.length
      ? team.members
      : relay
        ? [team.one, team.two, team.three, team.four, team.five, team.six]
        : [team.one, team.two];
    return raw
      .map(value => String(value || "").trim())
      .filter(Boolean);
  }

  function buildTeamMembership(payload) {
    const stackers = Array.isArray(payload?.stackers) ? payload.stackers : [];
    const doubles = Array.isArray(payload?.doubles) ? payload.doubles : [];
    const relays = Array.isArray(payload?.relays) ? payload.relays : [];

    stackerNames = new Map(stackers
      .map(stacker => [String(stacker?.id || "").trim(), String(stacker?.name || "").trim()])
      .filter(([participant, name]) => participant && name));
    doublesMembers = new Map(doubles
      .map(team => [String(team?.id || "").trim(), memberIds(team, false)])
      .filter(([teamId]) => teamId));
    relayMembers = new Map(relays
      .map(team => [String(team?.id || "").trim(), memberIds(team, true)])
      .filter(([teamId]) => teamId));
  }

  function memberNode(participant, name) {
    const wrapper = document.createElement("span");
    wrapper.className = "team-member-profile";
    wrapper.dataset.participant = participant;
    const profileUrl = profileLinks.get(participant) || "";
    wrapper.append(profileUrl && approvedProfilePath.test(profileUrl)
      ? linkedNameNode(name, profileUrl)
      : plainNameNode(name));
    return wrapper;
  }

  function flattenDecoratedMemberCell(cell, separator) {
    const memberNodes = Array.from(cell.querySelectorAll(":scope > .team-member-profile"));
    if (!memberNodes.length) return;
    const names = memberNodes
      .map(node => String(node.textContent || "").trim())
      .filter(Boolean);
    cell.replaceChildren(document.createTextNode(names.join(separator)));
    delete cell.dataset.profileMembersSignature;
  }

  function decorateTeamRow(row, membership, separator) {
    const teamCell = row.querySelector(":scope > .team-cell");
    const memberCell = row.querySelector(":scope > .member-cell");
    const teamIdNode = teamCell ? directChild(teamCell, "span") : null;
    if (!memberCell || !teamIdNode) return;

    const teamId = String(teamIdNode.textContent || "").trim();
    const participants = membership.get(teamId) || [];
    if (!participants.length) {
      flattenDecoratedMemberCell(memberCell, separator);
      return;
    }

    const members = participants
      .map(participant => ({
        participant,
        name: stackerNames.get(participant) || "",
        profileUrl: profileLinks.get(participant) || ""
      }))
      .filter(member => member.name);
    if (!members.length) {
      flattenDecoratedMemberCell(memberCell, separator);
      return;
    }

    const signature = members
      .map(member => `${member.participant}\u0000${member.name}\u0000${member.profileUrl}`)
      .join("\u0001");
    if (memberCell.dataset.profileMembersSignature === signature) return;

    const nodes = [];
    members.forEach((member, index) => {
      if (index) nodes.push(document.createTextNode(separator));
      nodes.push(memberNode(member.participant, member.name));
    });
    memberCell.replaceChildren(...nodes);
    memberCell.dataset.profileMembersSignature = signature;
  }

  function decorateTeamMembers() {
    document.querySelectorAll(".doubles-table tbody tr")
      .forEach(row => decorateTeamRow(row, doublesMembers, " & "));
    document.querySelectorAll(".relay-table tbody tr")
      .forEach(row => decorateTeamRow(row, relayMembers, ", "));
  }

  function decorateVisibleStackerNames() {
    document.querySelectorAll(".stacker-cell").forEach(decorateCell);
    decorateTeamMembers();
  }

  async function refreshTeamMembership() {
    try {
      const response = await fetch(resultsEndpoint, {
        headers: { Accept: "application/json" },
        credentials: "omit",
        cache: "no-store"
      });
      if (!response.ok) throw new Error("results-unavailable");
      buildTeamMembership(await response.json());
    } catch {
      // Team membership is optional presentation metadata for profile links only.
      // Clear it on failure so previously linked member names fail closed to plain text.
      stackerNames = new Map();
      doublesMembers = new Map();
      relayMembers = new Map();
    }
  }

  async function refreshProfileLinks() {
    try {
      const response = await fetch(endpoint, {
        headers: { Accept: "application/json" },
        credentials: "omit",
        cache: "no-store"
      });

      if (!response.ok) {
        profileLinks = new Map();
      } else {
        const payload = await response.json();
        const rows = Array.isArray(payload?.links) ? payload.links : [];
        profileLinks = new Map(rows
          .filter(row =>
            typeof row?.participant === "string"
            && row.participant.trim().length > 0
            && typeof row?.profileUrl === "string"
            && approvedProfilePath.test(row.profileUrl))
          .map(row => [row.participant.trim(), row.profileUrl]));
      }
    } catch {
      // Fail closed to plain-text names. Results remain available even when the optional
      // career-profile link bridge is temporarily unavailable.
      profileLinks = new Map();
    }

    await refreshTeamMembership();
    scheduleDecorate();
  }

  const observer = new MutationObserver(scheduleDecorate);
  observer.observe(document.body, { childList: true, subtree: true });

  void refreshProfileLinks();
  window.addEventListener("pageshow", () => void refreshProfileLinks());
  window.setInterval(() => void refreshProfileLinks(), 60000);
})();
