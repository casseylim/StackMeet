(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetCompetitionPackage = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  const FORMAT = "StackMeet.CompetitionPackage";
  const VERSION = 1;

  function clone(value) { return value == null ? value : JSON.parse(JSON.stringify(value)); }

  function canonicalize(value) {
    if (Array.isArray(value)) return `[${value.map(canonicalize).join(",")}]`;
    if (value && typeof value === "object") return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonicalize(value[key])}`).join(",")}}`;
    return JSON.stringify(value);
  }

  async function sha256Hex(value) {
    let cryptoApi = globalThis.crypto;
    if (!cryptoApi?.subtle && typeof require === "function") cryptoApi = require("crypto").webcrypto;
    if (!cryptoApi?.subtle) throw new Error("Web Crypto is required to hash a competition package.");
    const bytes = new TextEncoder().encode(value);
    const digest = await cryptoApi.subtle.digest("SHA-256", bytes);
    return [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, "0")).join("");
  }

  async function withContentHash(pkg) {
    const copy = clone(pkg);
    copy.manifest.contentHash = "";
    copy.manifest.contentHash = await sha256Hex(canonicalize(copy));
    return copy;
  }

  async function verifyContentHash(pkg) {
    if (!pkg?.manifest?.contentHash) return false;
    const expected = String(pkg.manifest.contentHash).toLowerCase();
    const copy = clone(pkg);
    copy.manifest.contentHash = "";
    return expected === await sha256Hex(canonicalize(copy));
  }

  function packageToState(pkg) {
    assertPackage(pkg);
    return {
      settings: clone(pkg.competition.settings),
      events: clone(pkg.competition.events),
      divisionSettings: clone(pkg.competition.divisionSettings),
      divisions: clone(pkg.competition.divisions),
      stackers: clone(pkg.participants.stackers),
      doubles: clone(pkg.participants.doubles),
      relays: clone(pkg.participants.relays),
      results: clone(pkg.competitionData.results),
      finalQualificationSnapshots: clone(pkg.competitionData.finalQualificationSnapshots),
      awards: clone(pkg.competitionData.awards),
      notifications: clone(pkg.competitionData.notifications)
    };
  }

  function createPackage(state, manifest = {}, exportedAt = new Date().toISOString(), authoritativeParticipants = {}) {
    const source = state || {};
    const stackers = clone(authoritativeParticipants.stackers ?? source.stackers ?? []);
    const doubles = clone(authoritativeParticipants.doubles ?? source.doubles ?? []);
    const relays = clone(authoritativeParticipants.relays ?? source.relays ?? []);
    const results = clone(source.results || []);
    return {
      manifest: {
        packageFormat: FORMAT, packageVersion: VERSION,
        competitionKey: String(manifest.competitionKey || "").trim(),
        competitionId: manifest.competitionId ?? null, competitionCode: manifest.competitionCode ?? null,
        exportedAt, exportedBy: manifest.exportedBy ?? null,
        sourceMode: manifest.sourceMode === "offline" ? "offline" : "online",
        sourceRevision: manifest.sourceRevision ?? null, contentHash: manifest.contentHash || ""
      },
      competition: {
        metadata: clone(manifest.competitionMetadata || {}), settings: clone(source.settings || {}),
        events: clone(source.events || {}), divisionSettings: clone(source.divisionSettings || {}), divisions: clone(source.divisions || [])
      },
      participants: { stackers, doubles, relays },
      competitionData: {
        results, finalQualificationSnapshots: clone(source.finalQualificationSnapshots || []),
        finals: clone(source.finals || []), awards: clone(source.awards || {}),
        notifications: clone(source.notifications || []), audit: clone(source.audit || [])
      },
      metadata: { stackerCount: stackers.length, doublesCount: doubles.length, relayCount: relays.length, resultCount: results.length, warnings: [] }
    };
  }

  function validatePackage(pkg) {
    const errors = [];
    if (!pkg || typeof pkg !== "object") return ["Package must be an object."];
    const manifest = pkg.manifest;
    if (!manifest || manifest.packageFormat !== FORMAT) errors.push("Unsupported package format.");
    if (manifest?.packageVersion !== VERSION) errors.push("Unsupported package version.");
    if (!String(manifest?.competitionKey || "").trim()) errors.push("Competition key is required.");
    if (!["online", "offline"].includes(manifest?.sourceMode)) errors.push("Source mode must be online or offline.");
    for (const section of ["competition", "participants", "competitionData", "metadata"]) if (!pkg[section] || typeof pkg[section] !== "object") errors.push(`Missing package section: ${section}.`);
    if (!Array.isArray(pkg.participants?.stackers)) errors.push("Stackers must be an array.");
    if (!Array.isArray(pkg.participants?.doubles)) errors.push("Doubles must be an array.");
    if (!Array.isArray(pkg.participants?.relays)) errors.push("Relays must be an array.");
    if (!Array.isArray(pkg.competitionData?.results)) errors.push("Results must be an array.");
    if (!Array.isArray(pkg.competitionData?.finalQualificationSnapshots)) errors.push("Qualification snapshots must be an array.");
    if (!Array.isArray(pkg.competitionData?.finals)) errors.push("Finals must be an array.");
    if (!Array.isArray(pkg.metadata?.warnings)) errors.push("Warnings must be an array.");
    for (const team of [...(pkg.participants?.doubles || []), ...(pkg.participants?.relays || [])]) {
      const division = String(team?.division || team?.timedRelayDivision || "").trim();
      if (!team?.customDivision && /\s+[MF]$/i.test(division)) errors.push(`Team ${team?.id || ""} has an invalid gendered division.`);
    }
    if (pkg.metadata && Number.isInteger(pkg.metadata.stackerCount) && pkg.metadata.stackerCount !== (pkg.participants?.stackers || []).length) errors.push("Stacker count does not match package contents.");
    if (pkg.metadata && Number.isInteger(pkg.metadata.doublesCount) && pkg.metadata.doublesCount !== (pkg.participants?.doubles || []).length) errors.push("Doubles count does not match package contents.");
    if (pkg.metadata && Number.isInteger(pkg.metadata.relayCount) && pkg.metadata.relayCount !== (pkg.participants?.relays || []).length) errors.push("Relay count does not match package contents.");
    if (pkg.metadata && Number.isInteger(pkg.metadata.resultCount) && pkg.metadata.resultCount !== (pkg.competitionData?.results || []).length) errors.push("Result count does not match package contents.");
    return errors;
  }

  function assertPackage(pkg, destinationKey = "") {
    const errors = validatePackage(pkg);
    if (destinationKey && String(pkg?.manifest?.competitionKey).toUpperCase() !== String(destinationKey).toUpperCase()) errors.push("Package competition does not match destination competition.");
    if (errors.length) throw new Error(errors.join(" "));
    return pkg;
  }

  return Object.freeze({ FORMAT, VERSION, createPackage, packageToState, validatePackage, assertPackage, canonicalize, sha256Hex, withContentHash, verifyContentHash });
});
