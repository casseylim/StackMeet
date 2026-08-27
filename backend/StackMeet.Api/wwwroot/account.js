(function () {
  const params = new URLSearchParams(location.search);
  const purpose = params.get("purpose") || "activate";
  const token = params.get("token") || "";
  const $ = id => document.getElementById(id);

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
    if (!token) return message("This account link is missing its token.");
    if (password.length < 8) return message("Password must be at least 8 characters.");
    if (password !== confirm) return message("Passwords do not match.");

    const response = await fetch(endpoint(), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(payload(password))
    });
    if (!response.ok) {
      let error = "Unable to save password.";
      try { error = (await response.json()).error || error; } catch (_) { /* keep default */ }
      return message(error);
    }

    $("accountForm").reset();
    message("Password saved. You can now return to NADITrack and log in.", true);
  }

  $("accountTitle").textContent = purpose === "reset" ? "Reset Password" : "Activate Account";
  $("displayNameLabel").hidden = purpose === "reset";
  $("accountForm").addEventListener("submit", event => submit(event).catch(error => message(error.message)));
})();
