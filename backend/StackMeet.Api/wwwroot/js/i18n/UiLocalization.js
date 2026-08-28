(function (root, factory) {
  const api = factory(root);
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  root.StackMeetUiLocalization = api;
})(typeof window !== "undefined" ? window : globalThis, function (root) {
  function language() {
    return root.StackMeetLanguagePreference?.getPreferredLanguage?.() || "en";
  }

  function t(key, code = language()) {
    return root.StackMeetI18n?.translate(key, code, {}, root.StackMeetI18nLocales || {}) || String(key ?? "");
  }

  function tf(template, values, code = language()) {
    const translated = t(template, code);
    return root.StackMeetI18n?.format(translated, values) || translated;
  }

  function translateKnownMessage(value, fallback = "Request failed.", values) {
    const raw = String(value || "");
    const lockout = raw.match(/^Too many failed password attempts\. Try again in about (\d+) minutes\.$/);
    if (lockout) return tf("Too many failed password attempts. Try again in about {minutes} minutes.", { minutes: lockout[1] });
    const known = new Set([
      "Login failed.", "Unable to request a password reset.", "If this email is registered, a password reset link has been sent.",
      "Unable to load competitions.", "Choose an assigned competition.", "No competition access is assigned to this account.",
      "Enter your email address first.", "This account link is missing its token.", "Password must be at least 8 characters.",
      "Passwords do not match.", "Unable to save password.", "Password saved. You can now return to NADITrack and log in.",
      "Email confirmation is required before login.", "This password reset link has expired. Please request a new one.",
      "This password reset link is invalid or has already been used. Please request a new one.", "Account no longer exists.",
      "Reset email could not be sent. Contact your system admin.", "Invalid email or password.",
      "This account is not a Global System Admin."
    ]);
    if (known.has(raw) || Object.prototype.hasOwnProperty.call(root.StackMeetI18nLocales?.en || {}, raw)) return t(raw);
    return values ? tf(fallback, values) : t(fallback);
  }

  function apply(rootElement) {
    if (!rootElement) return;
    const documentObject = rootElement.ownerDocument || root.document;
    const elements = [rootElement, ...rootElement.querySelectorAll("*")];
    elements.forEach(element => {
      const skip = element.closest?.("[data-domain], [data-no-translate], .no-auto-translate");
      if (skip) return;
      [["data-i18n-placeholder", "placeholder"], ["data-i18n-aria-label", "aria-label"], ["data-i18n-title", "title"], ["data-i18n-alt", "alt"]].forEach(([marker, attribute]) => {
        const key = element.getAttribute?.(marker);
        if (key !== null && key !== undefined) element.setAttribute(attribute, t(key));
      });
      const key = element.getAttribute?.("data-i18n");
      if (key !== null && key !== undefined) {
        const ownText = [...(element.childNodes || [])].find(node => node.nodeType === 3);
        if (ownText) ownText.nodeValue = t(key);
        else if (!element.children?.length) element.textContent = t(key);
      }
      if (element.tagName === "OPTION" && !element.hasAttribute("data-domain-option")) {
        element.textContent = t(element.getAttribute("data-i18n") || element.textContent.trim());
      }
    });
    if (documentObject) root.StackMeetI18n?.setDocumentLanguage(language(), documentObject);
  }

  function setLanguage(code, rootElement) {
    const normalized = root.StackMeetLanguagePreference?.setPreferredLanguage?.(code) || code;
    root.StackMeetI18n?.setDocumentLanguage(normalized, root.document);
    apply(rootElement || root.document?.body);
    return normalized;
  }

  function roleLabel(role) {
    const labels = { CompetitionManager: "Competition Manager", DataEntry: "Data Entry", Viewer: "Viewer", SystemAdmin: "System Admin", "Global Admin": "Global Admin" };
    return t(labels[role] || role || "Competition Manager");
  }

  return { language, t, tf, translateKnownMessage, apply, setLanguage, roleLabel };
});
