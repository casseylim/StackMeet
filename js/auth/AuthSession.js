/**
 * Browser session boundary for competition login, expiry, and authenticated API headers.
 */
(function () {
  const sessionKey = "stackmeet-auth-session-v1";

  function readSession() {
    try {
      const parsed = JSON.parse(sessionStorage.getItem(sessionKey) || "null");
      if (!parsed?.token || !parsed?.competitionId) return null;
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

  function competitionId() {
    return readSession()?.competitionId || window.COMPETITION_KEY || "DEFAULT";
  }

  async function login(form) {
    if (isLocalFileMode()) {
      return saveSession({
        token: "local-file-test-token",
        competitionId: form.competitionId || window.COMPETITION_KEY || "DEFAULT",
        displayName: form.displayName || "Tournament desk",
        expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
        localFileTest: true
      });
    }

    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        competitionId: form.competitionId,
        password: form.password,
        displayName: form.displayName || "StackMeet User"
      })
    });
    if (!response.ok) {
      let message = "Login failed.";
      try { message = (await response.json()).error || message; } catch (_) { /* keep default */ }
      throw new Error(message);
    }
    return saveSession(await response.json());
  }

  function updateChrome() {
    const session = readSession();
    document.body.classList.toggle("auth-pending", !session);
    document.getElementById("sessionCompetition")?.replaceChildren(document.createTextNode(session ? `Competition ${session.competitionId}` : ""));
  }

  async function requireLogin() {
    let session = readSession();
    updateChrome();
    if (session) return session;

    const form = document.getElementById("loginForm");
    const error = document.getElementById("loginError");
    const competitionInput = document.getElementById("loginCompetitionId");
    if (competitionInput && !competitionInput.value) competitionInput.value = window.COMPETITION_KEY || "DEFAULT";
    if (error) error.textContent = "";

    return new Promise(resolve => {
      form?.addEventListener("submit", async event => {
        event.preventDefault();
        if (error) error.textContent = "";
        const button = form.querySelector("button[type='submit']");
        if (button) button.disabled = true;
        try {
          session = await login({
            competitionId: document.getElementById("loginCompetitionId")?.value.trim(),
            password: document.getElementById("loginPassword")?.value,
            displayName: document.getElementById("loginDisplayName")?.value.trim()
          });
          updateChrome();
          resolve(session);
        } catch (loginError) {
          if (error) error.textContent = loginError.message;
        } finally {
          if (button) button.disabled = false;
        }
      });
    });
  }

  function logout() {
    clearSession();
    location.reload();
  }

  window.StackMeetAuth = { authHeaders, clearSession, competitionId, logout, readSession, requireLogin };
  document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("logoutBtn")?.addEventListener("click", logout);
    updateChrome();
  });
})();
