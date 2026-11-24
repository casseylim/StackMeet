/** Public persistence interface for StackMeet competition state. */
class Repository {
  constructor(competitionKey) {
    const ApiProvider = typeof window !== "undefined"
      ? window.StackMeetStorage.ApiProvider
      : require("./ApiProvider.js");
    this.ApiProvider = ApiProvider;
    this.setCompetitionKey(competitionKey || this.#defaultCompetitionKey());
  }

  setCompetitionKey(competitionKey) {
    this.provider = new this.ApiProvider(competitionKey || this.#defaultCompetitionKey());
  }

  #defaultCompetitionKey() {
    return typeof window !== "undefined"
      ? window.StackMeetAuth?.competitionId?.() || window.COMPETITION_KEY || "DEFAULT"
      : "DEFAULT";
  }

  // Transitional JSON boundary. SQL owns roster and results; keep runtime state intact.
  static withoutLegacyStackers(data) {
    const legacy = structuredClone(data);
    legacy.stackers = [];
    return legacy;
  }

  static legacyStateForSave(data) {
    const legacy = structuredClone(data);
    legacy.stackers = [];
    legacy.results = [];
    return legacy;
  }

  // Preserve the existing additive merge and tombstone semantics. Revision acceptance
  // remains the provider's responsibility; this function does not perform any I/O.
  static mergeConcurrentState(latestState, localState, deletedRecords) {
    deletedRecords = deletedRecords || {};
    const merged = { ...latestState, ...localState };
    ["doubles", "relays", "notifications", "auditLogs"].forEach(key => {
      if (!Array.isArray(latestState?.[key]) || !Array.isArray(localState?.[key])) return;
      const records = new Map(latestState[key].map(item => [String(item.id), item]));
      const deleted = deletedRecords[key] instanceof Set ? deletedRecords[key] : new Set();
      deleted.forEach(id => records.delete(String(id)));
      localState[key].forEach(item => records.set(String(item.id), item));
      merged[key] = [...records.values()];
    });
    return merged;
  }

  load() { return this.provider.load(); }
  reloadLatestCompetition() { return this.provider.reloadLatest(); }
  save(state) { return this.provider.save(state); }
  reset() { throw new Error("Repository.reset() is not implemented."); }
  importXml(xml) { void xml; throw new Error("Repository.importXml(xml) is not implemented."); }
  exportXml(state) { void state; throw new Error("Repository.exportXml(state) is not implemented."); }
  validate(state) { void state; throw new Error("Repository.validate(state) is not implemented."); }
}
if (typeof window !== "undefined") {
  window.StackMeetStorage = window.StackMeetStorage || {};
  window.StackMeetStorage.Repository = Repository;
}
if (typeof module !== "undefined" && module.exports) module.exports = Repository;