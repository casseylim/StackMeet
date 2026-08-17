(function (root, factory) {
  const api = factory(root && root.StackMeetCompetitionPackage);
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetCompetitionPackageAssembler = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function (packageApi) {
  "use strict";

  function required(value, name) {
    if (value == null || value === "") throw new TypeError(`${name} is required.`);
    return value;
  }

  async function downloadOnlinePackage({ competitionKey, competitionId, loadState, listStackers, listResults, manifest = {}, exportedAt } = {}) {
    required(competitionKey, "A competition key");
    required(competitionId, "A competition id");
    if (typeof loadState !== "function") throw new TypeError("loadState must be a function.");
    if (typeof listStackers !== "function") throw new TypeError("listStackers must be a function.");
    if (!packageApi) throw new Error("Competition package support is not loaded.");

    const [state, stackers, resultPayload] = await Promise.all([loadState(competitionKey), listStackers(competitionId), typeof listResults === "function" ? listResults(competitionId) : null]);
    if (!state || typeof state !== "object") throw new Error("The online competition state is empty or invalid.");
    if (!Array.isArray(stackers)) throw new Error("The online Individual Stackers response is invalid.");

    const pkg = packageApi.createPackage(state, { ...manifest, competitionKey: String(competitionKey), competitionId, resultsRevision: resultPayload?.revision ?? manifest.resultsRevision ?? null }, exportedAt, { stackers, results: resultPayload?.results });
    return packageApi.withContentHash(pkg);
  }

  return Object.freeze({ downloadOnlinePackage });
});
