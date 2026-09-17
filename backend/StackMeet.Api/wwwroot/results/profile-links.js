(() => {
  "use strict";

  const parts = location.pathname.split("/").filter(Boolean);
  const resultsIndex = parts.findIndex(part => part.toLowerCase() === "results");
  const competitionId = resultsIndex > 0 ? decodeURIComponent(parts[resultsIndex - 1]) : "";
  if (!competitionId) return;

  const endpoint = `/api/public/competitions/${encodeURIComponent(competitionId)}/profile-links`;
  const approvedProfilePath = /^\/Stackers\/NDT-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{7}$/;
  let profileLinks = new Map();
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

  function decorateVisibleStackerNames() {
    document.querySelectorAll(".stacker-cell").forEach(decorateCell);
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
        scheduleDecorate();
        return;
      }

      const payload = await response.json();
      const rows = Array.isArray(payload?.links) ? payload.links : [];
      profileLinks = new Map(rows
        .filter(row =>
          typeof row?.participant === "string"
          && row.participant.trim().length > 0
          && typeof row?.profileUrl === "string"
          && approvedProfilePath.test(row.profileUrl))
        .map(row => [row.participant.trim(), row.profileUrl]));
      scheduleDecorate();
    } catch {
      // Fail closed to plain-text names. Results remain available even when the optional
      // career-profile link bridge is temporarily unavailable.
      profileLinks = new Map();
      scheduleDecorate();
    }
  }

  const observer = new MutationObserver(scheduleDecorate);
  observer.observe(document.body, { childList: true, subtree: true });

  void refreshProfileLinks();
  window.addEventListener("pageshow", () => void refreshProfileLinks());
  window.setInterval(() => void refreshProfileLinks(), 60000);
})();
