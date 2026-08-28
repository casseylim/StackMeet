(function (root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  root.StackMeetI18n = api;
})(typeof window !== "undefined" ? window : globalThis, function () {
  const canonical = { en: "en", ms: "ms", zh: "zh-Hans", "zh-hans": "zh-Hans", "zh-Hans": "zh-Hans" };

  function normalizeLanguageCode(code) {
    return canonical[String(code || "en")] || "en";
  }

  function supportedLanguages() {
    return ["en", "ms", "zh-Hans"];
  }

  function translate(keyOrText, language, translations) {
    const key = String(keyOrText ?? "");
    const code = normalizeLanguageCode(language);
    const dictionary = translations || {};
    const custom = dictionary[code] || (code === "zh-Hans" ? dictionary.zh : null) || {};
    const value = custom[key];
    return typeof value === "string" && value.trim() ? value : key;
  }

  function setDocumentLanguage(language, documentObject) {
    const document = documentObject || (typeof window !== "undefined" ? window.document : null);
    if (document?.documentElement) document.documentElement.lang = normalizeLanguageCode(language);
    return normalizeLanguageCode(language);
  }

  return { normalizeLanguageCode, supportedLanguages, translate, setDocumentLanguage };
});
