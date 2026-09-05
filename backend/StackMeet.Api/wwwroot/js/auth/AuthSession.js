(function () {
  const sessionKey = "stackmeet-auth-session-v1";
  const ui = () => window.StackMeetUiLocalization;
  const t = key => ui()?.t(key) || key;
  const tf = (template, values) => ui()?.tf(template, values) || template;
  const knownMessage = (value, fallback) => ui()?.translateKnownMessage(value, fallback) || t(fallback || "Request failed.");
  const activityRuntime = {
    descriptor: null,
    supports(capability) {
      return this.descriptor?.capabilities?.[capability] === true;
    }
  };
  window.StackMeetActivityRuntime = activityRuntime;

  function clearActivityDescriptor() {
    activityRuntime.descriptor = null;
  }

  // Loads generic activity metadata for the already-authorized SQL competition.
  // This read is optional: legacy/local modes and descriptor failures continue with compatibility behavior.
  async function loadActivityDescriptor(session) {
    clearActivityDescriptor();
    const competitionId = Number(session?.selectedCompetitionSqlId);
    if (!Number.isInteger(competitionId) || competitionId <= 0 || session?.localFileTest) return null;

    try {
      const response = await fetch(`/api/competitions/${encodeURIComponent(competitionId)}/activity`, {
        method: "GET",
        headers: { Accept: "application/json", ...authHeaders() }
      });
      if (!response.ok) throw new Error(`Competition activity request failed (${response.status}).`);
      const descriptor = await response.json();
      activityRuntime.descriptor = descriptor && typeof descriptor === "object" ? descriptor : null;
      return activityRuntime.descriptor;
    } catch (error) {
      console.warn("Competition activity descriptor unavailable; continuing with compatibility behavior.", error);
      return null;
    }
  }

  function initializeLoginLanguage() {
    const login = document.getElementById("loginScreen");
    const control = document.getElementById("operatorLanguage");
    if (!login || !control || !ui()) return;
    control.value = ui().language();
    ui().apply(login);
    document.title = t("NADITrack Login");
    control.addEventListener("change", event => {
      ui().setLanguage(event.target.value, login);
      const error = document.getElementById("loginError");
      if (error) error.textContent = "";
      document.title = t("NADITrack Login");
    });
  }

  // Reads the saved account session even before a competition has been selected.
  function readSession() {
    try {
      const parsed = JSON.parse(sessionStorage.getItem(sessionKey) || "null");
      if (!parsed?.token) return null;
      if (parsed.expiresAt && Date.parse(parsed.expiresAt) <= Date.now()) {
        clearSession();
        return null;
      }
      return parsed;
    } catch (_) {
      clearSession();
      return null;
    }
  }

  function clearSession() {
    sessionStorage.removeItem(sessionKey);
    clearActivityDescriptor();
  }

  function saveSession(session) {
    sessionStorage.setItem(sessionKey, JSON.stringify(session));
    return session;
  }

  function authHeaders() {
    const session = readSession();
    if (!session) return {};
    return session.localFileTest ? {} : { Authorization: `Bearer ${session.token}` };
  }

  function isLocalFileMode() {
    return location.protocol === "file:";
  }

  // Distinguishes old competition-password sessions from account sessions awaiting selection.
  function hasSelectedCompetition(session) {
    if (!session?.token) return false;
    if (!session.userId) return Boolean(session.competitionId);
    return Boolean(session.competitionId && session.selectedCompetitionSqlId);
  }

  function competitionId() {
    const session = readSession();
    return hasSelectedCompetition(session) ? session.competitionId : window.COMPETITION_KEY || "DEFAULT";
  }

  function defaultCompetitionId() {
    return window.COMPETITION_KEY || "DEFAULT";
  }

  // Preserves the account token and assigned competition list without auto-entering a competition.
  function normalizeLoginSession(session) {
    return {
      ...session,
      competitionAccess: Array.isArray(session.competitionAccess) ? session.competitionAccess : []
    };
  }

  // Authenticates by email/password; local file mode keeps a no-server test shortcut.
  async function login(form) {
    if (isLocalFileMode()) {
      return saveSession({
        token: "local-file-test-token",
        competitionId: form.competitionId || defaultCompetitionId(),
        displayName: form.displayName || t("Tournament desk"),
        expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
        localFileTest: true
      });
    }

    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ email: (form.email || "").trim(), password: form.password })
    });
    if (!response.ok) {
      let message = "Login failed.";
      try { message = (await response.json()).error || message; } catch (_) { /* keep default */ }
      throw new Error(message || "Login failed.");
    }
    return saveSession(normalizeLoginSession(await response.json()));
  }

  // Sends a generic self-service reset request without revealing whether the email exists.
  async function requestPasswordReset(email) {
    const response = await fetch("/api/auth/forgot-password", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ email: (email || "").trim() })
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(knownMessage(data.error, "Unable to request a password reset."));
    return knownMessage(data.message, "If this email is registered, a password reset link has been sent.");
  }

  // Loads competition choices from assignments, or all competitions for a global admin.
  async function competitionChoices(session) {
    const assigned = Array.isArray(session?.competitionAccess) ? session.competitionAccess : [];
    if (assigned.length) return assigned;
    if (!session?.isSystemAdmin) return [];

    const response = await fetch("/api/competitions", {
      headers: { Accept: "application/json", ...authHeaders() }
    });
    if (!response.ok) {
      let message = "Unable to load competitions.";
      try { message = (await response.json()).error || message; } catch (_) { /* keep default */ }
      throw new Error(knownMessage(message, "Unable to load competitions."));
    }
    return (await response.json()).map(item => ({
      competitionId: item.id,
      competitionKey: item.competitionKey,
      competitionName: item.competitionName,
      role: "Global Admin"
    }));
  }

  // Stores the selected competition in the session and URL before the app boots.
  function chooseCompetition(session, competitionId) {
    const choices = Array.isArray(session.competitionAccess) ? session.competitionAccess : [];
    const selected = choices.find(item => String(item.competitionId) === String(competitionId));
    if (!selected) throw new Error("Choose an assigned competition.");
    clearActivityDescriptor();
    const nextSession = saveSession({
      ...session,
      competitionId: selected.competitionKey,
      selectedCompetitionSqlId: selected.competitionId,
      selectedCompetitionName: selected.competitionName,
      selectedCompetitionRole: selected.role
    });
    const url = new URL(location.href);
    url.searchParams.set("competitionId", String(selected.competitionId));
    history.replaceState(null, "", url);
    return nextSession;
  }

  // Escapes account-sourced competition labels before placing them into the picker.
  function esc(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  // Populates the second login step with competitions available to this account.
  function renderCompetitionChoices(choices) {
    const select = document.getElementById("loginCompetitionSelect");
    if (!select) return;
    select.innerHTML = choices.map(item =>
      `<option value="${esc(item.competitionId)}" data-domain-option>${esc(item.competitionKey)} - ${esc(item.competitionName)} (${esc(ui()?.roleLabel(item.role) || item.role || t("Competition Manager"))})</option>`
    ).join("");
  }

  // Keeps the login screen visible until both account and competition are selected.
  function updateChrome() {
    const session = readSession();
    const ready = hasSelectedCompetition(session);
    document.body.classList.toggle("auth-pending", !ready);
    document.getElementById("sessionCompetition")?.replaceChildren(document.createTextNode(ready ? `${session.selectedCompetitionName || t("Competition")} (${session.competitionId})` : ""));
  }

  // Blocks app startup until email/password login and competition selection are complete.
  async function requireLogin() {
    initializeLoginLanguage();
    let session = readSession();
    updateChrome();
    if (hasSelectedCompetition(session)) {
      await loadActivityDescriptor(session);
      return session;
    }

    const form = document.getElementById("loginForm");
    const error = document.getElementById("loginError");
    const credentialsStep = document.getElementById("loginCredentialsStep");
    const competitionStep = document.getElementById("loginCompetitionStep");
    const submitButton = form?.querySelector("button[type='submit']");
    const switchAccountButton = document.getElementById("loginSwitchAccount");
    const forgotButton = document.getElementById("forgotPasswordButton");
    const forgotPanel = document.getElementById("forgotPasswordPanel");
    const forgotEmail = document.getElementById("forgotPasswordEmail");
    const sendForgot = document.getElementById("sendForgotPassword");
    if (error) error.textContent = "";
    forgotButton?.addEventListener("click", () => { if (forgotPanel) forgotPanel.hidden = !forgotPanel.hidden; if (forgotEmail) { forgotEmail.value = document.getElementById("loginEmail")?.value.trim() || ""; forgotEmail.focus(); } });
    sendForgot?.addEventListener("click", async () => {
      if (!forgotEmail?.value.trim()) { if (error) error.textContent = t("Enter your email address first."); return; }
      try { if (error) error.textContent = await requestPasswordReset(forgotEmail.value); }
      catch (resetError) { if (error) error.textContent = knownMessage(resetError.message, "Unable to request a password reset."); }
    });

    // Toggles form controls so browser validation only applies to the active login step.
    function setCompetitionStepVisible(visible) {
      if (credentialsStep) credentialsStep.hidden = visible;
      if (competitionStep) competitionStep.hidden = !visible;
      document.getElementById("loginEmail")?.toggleAttribute("disabled", visible);
      document.getElementById("loginPassword")?.toggleAttribute("disabled", visible);
      document.getElementById("loginCompetitionSelect")?.toggleAttribute("disabled", !visible);
      if (submitButton) submitButton.textContent = t(visible ? "Open Competition" : "Log In");
      if (switchAccountButton) switchAccountButton.hidden = !visible;
      if (forgotButton) forgotButton.hidden = visible;
      if (forgotPanel) forgotPanel.hidden = true;
    }

    async function showCompetitionStep(accountSession) {
      const choices = await competitionChoices(accountSession);
      if (!choices.length) throw new Error("No competition access is assigned to this account.");
      accountSession.competitionAccess = choices;
      session = saveSession(accountSession);
      renderCompetitionChoices(choices);
      setCompetitionStepVisible(true);
      document.getElementById("loginCompetitionSelect")?.focus();
    }

    switchAccountButton?.addEventListener("click", () => {
      clearSession();
      session = null;
      setCompetitionStepVisible(false);
      if (error) error.textContent = "";
      document.getElementById("loginEmail")?.focus();
    });

    if (session && !hasSelectedCompetition(session)) {
      await showCompetitionStep(session).catch(loginError => {
        if (error) error.textContent = knownMessage(loginError.message, "Unable to load competitions.");
      });
    }

    return new Promise(resolve => {
      form?.addEventListener("submit", async event => {
        event.preventDefault();
        if (error) error.textContent = "";
        if (submitButton) submitButton.disabled = true;
        try {
          if (!session?.token) {
            session = await login({
              email: document.getElementById("loginEmail")?.value.trim(),
              password: document.getElementById("loginPassword")?.value
            });
            await showCompetitionStep(session);
            return;
          }

          session = chooseCompetition(session, document.getElementById("loginCompetitionSelect")?.value);
          updateChrome();
          await loadActivityDescriptor(session);
          resolve(session);
        } catch (loginError) {
          if (error) error.textContent = knownMessage(loginError.message, "Login failed.");
        } finally {
          if (submitButton) submitButton.disabled = false;
        }
      });
    });
  }

  async function logout() {
    // Notify the API for audit logging, but never block local logout if the request fails.
    try {
      const headers = authHeaders();
      if (headers.Authorization) {
        await fetch("/api/auth/logout", { method: "POST", headers });
      }
    } catch (_) {
      /* logout must still clear the browser session */
    }

    clearSession();
    location.reload();
  }

  window.StackMeetAuth = { authHeaders, clearSession, competitionId, logout, readSession, requireLogin };
  document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("logoutBtn")?.addEventListener("click", logout);
    updateChrome();
  });
})();
