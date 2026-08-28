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
      const value = storage()?.getItem(key);
      return value ? i18n.normalizeLanguageCode(value) : null;
    },
    setPreferredLanguage(code) {
      const normalized = i18n.normalizeLanguageCode(code);
      storage()?.setItem(key, normalized);
      return normalized;
    },
    clearPreferredLanguage() { storage()?.removeItem(key); }
  };
});
