(function () {
  const keyName = "stackmeet-admin-key-v1";
  let competitions = [];
  let users = [];

  const $ = id => document.getElementById(id);
  const message = text => { $("adminMessage").textContent = text || ""; };
  const adminKey = () => sessionStorage.getItem(keyName) || $("adminKey").value;
  const headers = () => ({ "Content-Type": "application/json", Accept: "application/json", "X-StackMeet-Admin-Key": adminKey() });

  function setAdminPage(page) {
    const selected = page || "email";
    document.querySelectorAll("[data-admin-page]").forEach(section => section.classList.toggle("active", section.dataset.adminPage === selected));
    document.querySelectorAll("[data-admin-page-target]").forEach(button => button.classList.toggle("active", button.dataset.adminPageTarget === selected));
    if (location.hash !== `#${selected}`) history.replaceState(null, "", `#${selected}`);
  }

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
    drawCompetitionOptions();
  }

  async function loadUsers() {
    users = await request("/api/admin/users");
    drawUserRows();
  }

  async function loadEmailSettings() {
    const settings = await request("/api/admin/email-settings");
    $("emailFromName").value = settings.fromName || "StackMeet";
    $("emailFromAddress").value = settings.fromAddress || "";
    $("emailSmtpHost").value = settings.smtpHost || "smtp-relay.brevo.com";
    $("emailSmtpPort").value = settings.smtpPort || 587;
    $("emailUsername").value = settings.username || "";
    $("emailPassword").value = "";
    $("emailUseTls").checked = settings.useTls !== false;
    $("emailSettingsStatus").textContent = settings.canStoreProtectedSecrets
      ? `SMTP password storage is encrypted. ${settings.hasPassword ? "A password is saved." : "No password is saved yet."}`
      : "Set Security:SettingsEncryptionKey before saving an SMTP password.";
  }

  async function loadAdminData() {
    await loadCompetitions();
    await Promise.all([loadUsers(), loadEmailSettings()]);
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

  function drawCompetitionOptions() {
    const options = ['<option value="">No competition assignment</option>'].concat(competitions.map(item => `<option value="${item.id}">${esc(item.competitionKey)} — ${esc(item.competitionName)}</option>`));
    $("inviteCompetition").innerHTML = options.join("");
  }

  function drawUserRows() {
    if (!users.length) {
      $("userAdminRows").innerHTML = '<tr><td colspan="4" class="muted">No users found.</td></tr>';
      return;
    }

    $("userAdminRows").innerHTML = users.map(user => {
      const access = user.isSystemAdmin
        ? "System Admin"
        : (user.competitionAccess || []).map(item => `${item.competitionKey}: ${item.role}`).join("<br />") || "No assigned competition";
      const status = `${user.isActive ? "Active" : "Pending"}${user.emailConfirmed ? "" : " / Unconfirmed"}`;
      return `
        <tr>
          <td><strong>${esc(user.email)}</strong><br /><span class="muted">${esc(user.displayName)}</span></td>
          <td>${esc(status)}</td>
          <td>${access}</td>
          <td><button class="ghost compact-button" data-access-user="${user.id}" type="button">Assign</button> <button class="ghost compact-button" data-reset-user="${user.id}" type="button">Reset</button></td>
        </tr>`;
    }).join("");
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
      await loadAdminData();
      $("adminKey").value = "";
    } catch (error) {
      sessionStorage.removeItem(keyName);
      throw error;
    }
  }

  async function saveEmailSettings(event) {
    event.preventDefault();
    await request("/api/admin/email-settings", {
      method: "PUT",
      body: JSON.stringify({
        fromName: $("emailFromName").value.trim(),
        fromAddress: $("emailFromAddress").value.trim(),
        smtpHost: $("emailSmtpHost").value.trim(),
        smtpPort: Number($("emailSmtpPort").value || 587),
        useTls: $("emailUseTls").checked,
        username: $("emailUsername").value.trim(),
        password: $("emailPassword").value || null
      })
    });
    await loadEmailSettings();
    message("Email setup saved.");
  }

  async function sendTestEmail() {
    const toEmail = prompt("Send test email to:");
    if (!toEmail) return;
    const result = await request("/api/admin/email-settings/test", { method: "POST", body: JSON.stringify({ toEmail }) });
    message(result.message || "Test email sent.");
  }

  async function inviteUser(event) {
    event.preventDefault();
    const competitionId = Number($("inviteCompetition").value || 0);
    const competitionAccess = competitionId
      ? [{ competitionId, role: $("inviteRole").value, isActive: true }]
      : [];
    const result = await request("/api/admin/users/invite", {
      method: "POST",
      body: JSON.stringify({
        email: $("inviteEmail").value.trim(),
        displayName: $("inviteDisplayName").value.trim(),
        isSystemAdmin: $("inviteSystemAdmin").checked,
        competitionAccess
      })
    });
    $("inviteUserForm").reset();
    await loadUsers();
    message(result.message || "Activation email sent.");
  }

  async function sendPasswordReset(userId) {
    const user = users.find(item => String(item.id) === String(userId));
    if (!user) return;
    if (!confirm(`Send password reset link to ${user.email}?`)) return;
    const result = await request(`/api/admin/users/${encodeURIComponent(userId)}/password-reset`, { method: "POST" });
    message(result.message || "Password reset email sent.");
  }

  async function assignAccess(userId) {
    const competitionId = Number($("inviteCompetition").value || 0);
    if (!competitionId) return message("Choose a competition in User Management before assigning access.");
    const result = await request(`/api/admin/users/${encodeURIComponent(userId)}/competition-access`, {
      method: "POST",
      body: JSON.stringify({ competitionId, role: $("inviteRole").value, isActive: true })
    });
    users = users.map(item => item.id === result.id ? result : item);
    drawUserRows();
    message("User access updated.");
  }

  function esc(value) {
    return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");
  }

  $("saveAdminKey").addEventListener("click", () => activateAdminKey().catch(error => message(error.message)));
  $("refreshAdmin").addEventListener("click", () => loadAdminData().catch(error => message(error.message)));
  $("newCompetition").addEventListener("click", () => fillForm(null));
  document.querySelectorAll("[data-admin-page-target]").forEach(button => button.addEventListener("click", () => setAdminPage(button.dataset.adminPageTarget)));
  $("emailSettingsForm").addEventListener("submit", event => saveEmailSettings(event).catch(error => message(error.message)));
  $("testEmail").addEventListener("click", () => sendTestEmail().catch(error => message(error.message)));
  $("inviteUserForm").addEventListener("submit", event => inviteUser(event).catch(error => message(error.message)));
  $("competitionAdminForm").addEventListener("submit", event => saveCompetition(event).catch(error => message(error.message)));
  $("competitionAdminRows").addEventListener("click", event => {
    const button = event.target.closest("[data-key]");
    if (!button) return;
    fillForm(competitions.find(item => item.competitionKey === button.dataset.key));
  });
  $("userAdminRows").addEventListener("click", event => {
    const accessButton = event.target.closest("[data-access-user]");
    if (accessButton) {
      assignAccess(accessButton.dataset.accessUser).catch(error => message(error.message));
      return;
    }
    const button = event.target.closest("[data-reset-user]");
    if (!button) return;
    sendPasswordReset(button.dataset.resetUser).catch(error => message(error.message));
  });
  document.querySelectorAll("[data-admin-action]").forEach(button => button.addEventListener("click", () => adminAction(button.dataset.adminAction).catch(error => message(error.message))));
  setAdminPage(location.hash.replace("#", "") || "email");
  fillForm(null);
  if (sessionStorage.getItem(keyName)) {
    loadAdminData().catch(error => message(error.message));
  } else {
    message("Enter the admin key to load competitions.");
  }
})();
