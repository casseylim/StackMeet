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

  load() { return this.provider.load(); }
  reloadLatestCompetition() { return this.provider.load(); }
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
