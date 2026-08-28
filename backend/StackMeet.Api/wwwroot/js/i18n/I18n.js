(function (root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  root.StackMeetI18n = api;
})(typeof window !== "undefined" ? window : globalThis, function () {
  const canonical = { en: "en", ms: "ms", zh: "zh-Hans", "zh-hans": "zh-Hans" };

  function normalizeLanguageCode(code) {
    const normalized = String(code ?? "").trim().toLowerCase();
    return canonical[normalized] || "en";
  }

  function supportedLanguages() {
    return ["en", "ms", "zh-Hans"];
  }

  function resolveDictionary(language, translations, builtIns) {
    const code = normalizeLanguageCode(language);
    const dictionary = translations || {};
    const locales = builtIns || {};
    return {
      code,
      customCanonical: dictionary[code] || {},
      customLegacy: code === "zh-Hans" ? (dictionary.zh || {}) : {},
      builtInCanonical: locales[code] || {},
      builtInLegacy: code === "zh-Hans" ? (locales.zh || {}) : {}
    };
  }

  function translate(keyOrText, language, translations, builtIns) {
    const key = String(keyOrText ?? "");
    const dictionaries = resolveDictionary(language, translations, builtIns);
    for (const dictionary of [dictionaries.customCanonical, dictionaries.customLegacy, dictionaries.builtInCanonical, dictionaries.builtInLegacy]) {
      const value = dictionary[key];
      if (typeof value === "string" && value.trim()) return value;
    }
    return key;
  }

  function format(template, values) {
    return String(template ?? "").replace(/\{([a-zA-Z][\w]*)\}/g, (match, name) => {
      const value = values && Object.prototype.hasOwnProperty.call(values, name) ? values[name] : match;
      return value === null || value === undefined ? "" : String(value);
    });
  }

  function setDocumentLanguage(language, documentObject) {
    const document = documentObject || (typeof window !== "undefined" ? window.document : null);
    if (document?.documentElement) document.documentElement.lang = normalizeLanguageCode(language);
    return normalizeLanguageCode(language);
  }

  return { normalizeLanguageCode, supportedLanguages, resolveDictionary, translate, format, setDocumentLanguage };
});
