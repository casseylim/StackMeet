(function (root, factory) {
  if (typeof module === "object" && module.exports) module.exports = factory;
  else root.StackMeetCareerAdmin = factory(document, text => root.confirm(text));
})(typeof window === "object" ? window : globalThis, function createCareerAdmin(document, confirmAction) {
  "use strict";
  const $ = id => document.getElementById(id);
  const api = "/api/admin/stacker-identities";
  const actions = { create: "canCreateIdentity", link: "canLinkIdentity", publish: "canPublishProfile", unpublish: "canUnpublishProfile", unlink: "canUnlinkIdentity" };
  let request, capabilities = null, selected = null, reviewedIdentity = null, busy = false, epoch = 0, skip = 0, page = null;
  const status = text => { $("careerStatus").textContent = text; };
  const allowed = key => capabilities?.authenticated === true && capabilities?.isSystemAdmin === true && capabilities?.[key] === true;
  function render() {
    const authorized = capabilities?.authenticated === true && capabilities?.isSystemAdmin === true;
    $("careerTab").hidden = !authorized;
    $("careerControls").hidden = !authorized;
    $("careerReview").hidden = !authorized || !selected;
    const link = selected?.link;
    $("careerDetails").textContent = selected ? JSON.stringify({ stackerId: selected.stackerId, participant: selected.stackerCode,
      displayName: selected.displayName, competition: selected.competition, identity: link || "NO_IDENTITY_LINK",
      proposedIdentity: selected.proposedIdentity, contactCopyNotice: selected.contactCopyNotice }, null, 2) : "";
    $("careerCandidates").textContent = selected ? `Possible identities: ${(selected.matchingCandidates || []).map(c => `${c.nadiTrackId} — ${c.displayName}`).join(", ") || "none"}.` : "";
    $("careerIdentityReview").textContent = reviewedIdentity ? `${reviewedIdentity.nadiTrackId} — ${reviewedIdentity.displayName}; ${reviewedIdentity.isPublicProfile ? "PUBLIC" : "PRIVATE"}` : "No existing identity reviewed.";
    $("careerCreate").disabled = busy || !allowed(actions.create) || !selected || !!link || !selected.createAllowed || !!selected.matchingCandidates?.length;
    $("careerLink").disabled = busy || !allowed(actions.link) || !selected || !!link || !reviewedIdentity;
    $("careerPublish").disabled = busy || !allowed(actions.publish) || !link || link.isPublicProfile;
    $("careerUnpublish").disabled = busy || !allowed(actions.unpublish) || !link?.isPublicProfile;
    $("careerUnlink").disabled = busy || !allowed(actions.unlink) || !link;
    $("careerLookup").disabled = busy || !authorized || !selected || !!link;
    $("careerPrevious").disabled = busy || !authorized || skip === 0;
    $("careerNext").disabled = busy || !authorized || !page || skip + page.items.length >= page.total;
    $("careerPublicUrl").hidden = !authorized || !link?.isPublicProfile;
    $("careerPublicUrl").removeAttribute("href");
    if (authorized && link?.isPublicProfile) $("careerPublicUrl").href = `/Stackers/${encodeURIComponent(link.nadiTrackId)}`;
  }
  function clear() {
    epoch++; capabilities = null; selected = null; reviewedIdentity = null; page = null; skip = 0; busy = false;
    $("careerRows").replaceChildren(); $("careerIdentity").value = ""; $("careerReason").value = "";
    $("careerPageStatus").textContent = ""; status("Verify Career Profile capabilities after signing in."); render();
  }
  async function refresh() {
    clear(); const version = epoch;
    try {
      const result = await request("/api/admin/career-profile/capabilities");
      if (version !== epoch) return;
      capabilities = result; render();
      status(result.authenticated && result.isSystemAdmin ? "Career Profile authority verified. Search one competition to begin review." : "Career Profile administration is not authorized.");
    } catch (error) { if (version === epoch) { clear(); status(error.message || "Career Profile capability verification failed."); } throw error; }
  }
  async function search(reset = true) {
    if (!capabilities?.authenticated || !capabilities?.isSystemAdmin || busy) return;
    const competitionId = Number($("careerCompetition").value);
    if (!Number.isInteger(competitionId) || competitionId <= 0) throw new Error("Enter a valid competition ID.");
    if (reset) skip = 0;
    const version = ++epoch; selected = null; reviewedIdentity = null; render();
    const query = new URLSearchParams({ competitionId, search: $("careerSearch").value.trim(), skip, take: 50 });
    const result = await request(`${api}/stackers?${query}`);
    if (version !== epoch) return;
    page = result; $("careerRows").replaceChildren();
    for (const item of page.items) {
      const row = document.createElement("tr");
      for (const text of [item.stackerCode, item.displayName, item.nadiTrackId || "UNLINKED"]) {
        const cell = document.createElement("td"); cell.textContent = text; row.appendChild(cell);
      }
      const cell = document.createElement("td"), button = document.createElement("button");
      button.type = "button"; button.textContent = `Review #${item.stackerId}`;
      button.addEventListener("click", () => run(() => select(item.stackerId))); cell.appendChild(button); row.appendChild(cell); $("careerRows").appendChild(row);
    }
    $("careerPageStatus").textContent = `${page.total} matching registrations; showing ${page.items.length ? skip + 1 : 0}–${skip + page.items.length}.`;
    render();
  }
  async function select(id) {
    if (busy || !capabilities?.authenticated || !capabilities?.isSystemAdmin) return;
    const version = ++epoch; selected = null; reviewedIdentity = null; $("careerReason").value = ""; $("careerIdentity").value = ""; render();
    const result = await request(`${api}/stackers/${Number(id)}`);
    if (version !== epoch) return;
    selected = result; render(); status("Review the selected registration. Every change requires a reason and confirmation.");
    if (!selected.createAllowed) status("Source name contains a placeholder. Identity creation is blocked; review source data separately. No participant correction is performed here.");
  }
  async function lookup() {
    if (busy || !selected || selected.link || !allowed(actions.link)) return;
    const id = $("careerIdentity").value.trim().toUpperCase(); if (!id) throw new Error("Enter an existing NadiTrackId.");
    const version = ++epoch; reviewedIdentity = null; render();
    const result = await request(`${api}/profiles/${encodeURIComponent(id)}`);
    if (version !== epoch) return;
    reviewedIdentity = result; render();
  }
  async function mutate(action) {
    if (busy || !allowed(actions[action]) || !selected) return;
    const current = selected, identity = reviewedIdentity, version = epoch;
    const reason = $("careerReason").value.trim(); if (!reason) throw new Error("Enter a reason before confirming this operation.");
    let route, method, body, confirmation;
    const label = `${current.displayName}; participant ${current.stackerCode}; CompetitionStacker #${current.stackerId}`;
    if (action === "create" || action === "link") {
      if (current.link) return;
      if (action === "create" && (!current.createAllowed || current.matchingCandidates?.length)) throw new Error("Resolve source-name or duplicate candidates before creating an identity.");
      if (action === "link" && !identity) throw new Error("Review an existing permanent identity first.");
      if (action === "link" && identity.isPublicProfile) throw new Error("Unpublish the target identity in a separate confirmed operation before adding a link.");
      route = `${api}/links`; method = "POST";
      body = { stackerId: current.stackerId, requestedAction: action === "create" ? 2 : 1,
        explicitNadiTrackId: action === "link" ? identity.nadiTrackId : null, selectedNadiTrackId: action === "link" ? identity.nadiTrackId : null,
        candidateConfirmed: action === "link", createNewOverrideConfirmed: false, resolutionNote: reason };
      confirmation = action === "create" ? `Create a permanent identity and link ONLY ${label}?\nProposed identity: ${JSON.stringify(current.proposedIdentity)}\nDefault publication state = PRIVATE.\n${current.contactCopyNotice}\nCompetition results themselves will not be modified.`
        : `Link ONLY ${label} to ${identity.nadiTrackId} — ${identity.displayName}?\nThe permanent identity remains PRIVATE. Competition results themselves will not be modified.`;
    } else {
      if (!current.link) return;
      const id = encodeURIComponent(current.link.nadiTrackId);
      if (action === "publish" || action === "unpublish") {
        route = `${api}/profiles/${id}/publication`; method = "PUT"; body = { isPublicProfile: action === "publish", reason };
        confirmation = `${label}; identity ${current.link.nadiTrackId}.\n` + (action === "publish" ? "This will make the Career Profile publicly accessible." : "This will remove public access but preserve the identity and competition links.");
      } else if (action === "unlink") {
        route = `${api}/links/${current.link.linkId}/unlink`; method = "POST";
        body = { stackerId: current.stackerId, nadiTrackId: current.link.nadiTrackId, reason };
        confirmation = `${label}; identity ${current.link.nadiTrackId}.\nThis removes only the identity relationship. Competition registration and results will remain unchanged.`;
      } else return;
    }
    busy = true; render();
    try {
      if (!confirmAction(`${confirmation}\nReason: ${reason}`)) return;
      // Selection/authorization may have changed while a custom confirmation was open.
      if (version !== epoch || !allowed(actions[action])) return;
      await request(route, { method, body: JSON.stringify(body) });
      if (version !== epoch) return;
      busy = false; await select(current.stackerId); status("Operation completed. Review refreshed state before another action.");
    } catch (error) {
      if (version === epoch) { selected = null; reviewedIdentity = null; render(); }
      throw error;
    } finally { busy = false; render(); }
  }
  async function run(operation) { try { await operation(); } catch (error) { status(error.message || "Career Profile request failed. Refresh before retrying."); } }
  function connect(authenticatedRequest) {
    request = authenticatedRequest;
    $("careerSearchForm").addEventListener("submit", event => { event.preventDefault(); run(() => search()); });
    $("careerPrevious").addEventListener("click", () => { if (!busy) { skip = Math.max(0, skip - 50); run(() => search(false)); } });
    $("careerNext").addEventListener("click", () => { if (!busy) { skip += 50; run(() => search(false)); } });
    $("careerLookup").addEventListener("click", () => run(lookup));
    $("careerIdentity").addEventListener("input", () => { reviewedIdentity = null; epoch++; render(); });
    for (const action of Object.keys(actions)) $("career" + action[0].toUpperCase() + action.slice(1)).addEventListener("click", () => run(() => mutate(action)));
    clear();
  }
  return { connect, clear, refresh, search, select, lookup, mutate };
});
