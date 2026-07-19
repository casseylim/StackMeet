(function () {
  const keyName = "stackmeet-admin-key-v1";
  let competitions = [];

  const $ = id => document.getElementById(id);
  const message = text => { $("adminMessage").textContent = text || ""; };
  const adminKey = () => sessionStorage.getItem(keyName) || $("adminKey").value;
  const headers = () => ({ "Content-Type": "application/json", Accept: "application/json", "X-StackMeet-Admin-Key": adminKey() });

  async function request(url, options = {}) {
    const response = await fetch(url, { ...options, headers: { ...headers(), ...(options.headers || {}) } });
    if (!response.ok) {
      if (response.status === 401) sessionStorage.removeItem(keyName);
      let error = `Request failed (${response.status})`;
      try { error = (await response.json()).error || error; } catch (_) { /* keep default */ }
      throw new Error(error);
    }
    return response.status === 204 ? undefined : response.json();
  }

  async function loadCompetitions() {
    message("");
    competitions = await request("/api/admin/competitions");
    drawRows();
  }

  function drawRows() {
    if (!competitions.length) {
      $("competitionAdminRows").innerHTML = '<tr><td colspan="6" class="muted">No competitions found.</td></tr>';
      return;
    }

    $("competitionAdminRows").innerHTML = competitions.map(item => `
      <tr>
        <td><strong>${esc(item.competitionKey)}</strong></td>
        <td>${esc(item.competitionName)}</td>
        <td>${esc(item.status)}</td>
        <td>${item.hasState ? "Ready" : "Missing"}</td>
        <td>${esc(item.updatedAt?.slice(0, 19) || "")}</td>
        <td><button class="ghost compact-button" data-key="${esc(item.competitionKey)}" type="button">Edit</button></td>
      </tr>`).join("");
  }

  function fillForm(item) {
    $("adminFormTitle").textContent = item ? `Edit ${item.competitionKey}` : "Create Competition";
    $("adminOriginalKey").value = item?.competitionKey || "";
    $("adminCompetitionKey").value = item?.competitionKey || "";
    $("adminCompetitionCode").value = item?.competitionCode || "";
    const editing = Boolean(item);
    $("adminCompetitionKey").readOnly = editing;
    $("adminCompetitionCode").readOnly = editing;
    $("immutableKeyHelp").hidden = !editing;
    $("adminCompetitionName").value = item?.competitionName || "";
    $("adminCompetitionVenue").value = item?.venue || "";
    $("adminStartDate").value = item?.startDate || "";
    $("adminEndDate").value = item?.endDate || "";
    $("adminStatus").value = item?.status || "Draft";
    $("adminPassword").value = "";
    $("adminJsonOutput").hidden = true;
  }

  async function saveCompetition(event) {
    event.preventDefault();
    const originalKey = $("adminOriginalKey").value;
    const payload = {
      competitionKey: $("adminCompetitionKey").value.trim(),
      competitionCode: $("adminCompetitionCode").value.trim() || $("adminCompetitionKey").value.trim(),
      competitionName: $("adminCompetitionName").value.trim(),
      venue: $("adminCompetitionVenue").value.trim(),
      startDate: $("adminStartDate").value,
      endDate: $("adminEndDate").value,
      status: $("adminStatus").value,
      password: $("adminPassword").value || null
    };
    if (originalKey) {
      await request(`/api/admin/competitions/${encodeURIComponent(originalKey)}`, { method: "PUT", body: JSON.stringify(payload) });
      if (payload.password) await request(`/api/admin/competitions/${encodeURIComponent(originalKey)}/password`, { method: "POST", body: JSON.stringify({ password: payload.password }) });
    } else {
      await request("/api/admin/competitions", { method: "POST", body: JSON.stringify(payload) });
    }
    await loadCompetitions();
    fillForm(competitions.find(item => item.competitionKey === (originalKey || payload.competitionKey.toUpperCase())));
  }

  async function adminAction(action) {
    const key = $("adminOriginalKey").value;
    if (!key) return message("Select a competition first.");
    if (action === "export") {
      const data = await request(`/api/admin/competitions/${encodeURIComponent(key)}/state/export`);
      $("adminJsonOutput").hidden = false;
      $("adminJsonOutput").textContent = JSON.stringify(data, null, 2);
      return;
    }
    if (action === "init") await request(`/api/admin/competitions/${encodeURIComponent(key)}/state/initialize`, { method: "POST" });
    if (action === "close") await request(`/api/admin/competitions/${encodeURIComponent(key)}/status`, { method: "POST", body: JSON.stringify({ status: "Closed" }) });
    if (action === "archive") await request(`/api/admin/competitions/${encodeURIComponent(key)}/archive`, { method: "POST", body: JSON.stringify({ archivedBy: "admin" }) });
    if (action === "delete") {
      const confirmation = prompt(`Type DELETE ${key} to permanently delete only this unused competition.`);
      if (confirmation !== `DELETE ${key}`) return;
      await request(`/api/admin/competitions/${encodeURIComponent(key)}/delete`, { method: "POST", body: JSON.stringify({ confirmation }) });
      await loadCompetitions();
      fillForm(null);
      message(`Deleted ${key}.`);
      return;
    }
    if (action === "reset") {
      const confirmation = prompt(`Type RESET ${key} to reset only ${key}.`);
      if (confirmation !== `RESET ${key}`) return;
      await request(`/api/admin/competitions/${encodeURIComponent(key)}/state/reset`, { method: "POST", body: JSON.stringify({ confirmation, resultsOnly: false }) });
    }
    await loadCompetitions();
  }

  async function activateAdminKey() {
    const value = $("adminKey").value.trim();
    if (!value) {
      message("Enter the admin key before selecting Use Key.");
      return;
    }

    sessionStorage.setItem(keyName, value);
    try {
      await loadCompetitions();
      $("adminKey").value = "";
    } catch (error) {
      sessionStorage.removeItem(keyName);
      throw error;
    }
  }

  function esc(value) {
    return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");
  }

  $("saveAdminKey").addEventListener("click", () => activateAdminKey().catch(error => message(error.message)));
  $("refreshAdmin").addEventListener("click", () => loadCompetitions().catch(error => message(error.message)));
  $("newCompetition").addEventListener("click", () => fillForm(null));
  $("competitionAdminForm").addEventListener("submit", event => saveCompetition(event).catch(error => message(error.message)));
  $("competitionAdminRows").addEventListener("click", event => {
    const button = event.target.closest("[data-key]");
    if (!button) return;
    fillForm(competitions.find(item => item.competitionKey === button.dataset.key));
  });
  document.querySelectorAll("[data-admin-action]").forEach(button => button.addEventListener("click", () => adminAction(button.dataset.adminAction).catch(error => message(error.message))));
  fillForm(null);
  if (sessionStorage.getItem(keyName)) {
    loadCompetitions().catch(error => message(error.message));
  } else {
    message("Enter the admin key to load competitions.");
  }
})();