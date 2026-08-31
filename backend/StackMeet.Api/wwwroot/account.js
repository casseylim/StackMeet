(function () {
  const params = new URLSearchParams(location.search);
  const purpose = params.get("purpose") || "activate";
  const token = params.get("token") || "";
  const $ = id => document.getElementById(id);
  const ui = window.StackMeetUiLocalization;
  const t = key => ui?.t(key) || key;
  const knownMessage = (value, fallback) => ui?.translateKnownMessage(value, fallback) || t(fallback || "Unable to save password.");
  const titleKey = purpose === "reset" ? "Reset Password" : "Activate Account";

  function message(text, ok = false) {
    const target = $("accountMessage");
    target.textContent = text || "";
    target.classList.toggle("success-text", ok);
  }

  function endpoint() {
    return purpose === "reset" ? "/api/auth/reset-password" : "/api/auth/activate";
  }

  function payload(password) {
    return purpose === "reset"
      ? { token, password }
      : { token, password, displayName: $("accountDisplayName").value.trim() || null };
  }

  async function submit(event) {
    event.preventDefault();
    message("");
    const password = $("accountPassword").value;
    const confirm = $("accountConfirmPassword").value;
    if (!token) return message(t("This account link is missing its token."));
    if (password.length < 8) return message(t("Password must be at least 8 characters."));
    if (password !== confirm) return message(t("Passwords do not match."));

    const response = await fetch(endpoint(), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(payload(password))
    });
    if (!response.ok) {
      let error = "Unable to save password.";
      try { error = (await response.json()).error || error; } catch (_) { /* keep default */ }
      return message(knownMessage(error, "Unable to save password."));
    }

    $("accountForm").reset();
    message(t("Password saved. You can now return to NADITrack and log in."), true);
  }

  const accountRoot = document.querySelector(".account-shell");
  const languageControl = $("operatorLanguage");
  if (ui) {
    languageControl.value = ui.language();
    ui.apply(accountRoot);
    languageControl.addEventListener("change", event => {
      ui.setLanguage(event.target.value, accountRoot);
      document.title = t(titleKey);
    });
  }
  $("accountTitle").setAttribute("data-i18n", titleKey);
  $("accountTitle").textContent = t(titleKey);
  document.title = t(titleKey);
  $("displayNameLabel").hidden = purpose === "reset";
  $("accountForm").addEventListener("submit", event => submit(event).catch(error => message(knownMessage(error.message, "Unable to save password."))));
})();
