(function (root, factory) {
  const api = factory(root);
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  root.StackMeetLanguagePreference = api;
})(typeof window !== "undefined" ? window : globalThis, function (root) {
  const i18n = root.StackMeetI18n;
  const key = "naditrack.uiLanguage";
  const storage = () => {
    try { return root.localStorage; } catch { return null; }
  };
  return {
    getPreferredLanguage() {
      let value = null;
      try { value = storage()?.getItem(key); } catch { return null; }
      return value ? i18n.normalizeLanguageCode(value) : null;
    },
    setPreferredLanguage(code) {
      const normalized = i18n.normalizeLanguageCode(code);
      try { storage()?.setItem(key, normalized); } catch { /* preference storage is optional */ }
      return normalized;
    },
    clearPreferredLanguage() { try { storage()?.removeItem(key); } catch { /* preference storage is optional */ } }
  };
});
