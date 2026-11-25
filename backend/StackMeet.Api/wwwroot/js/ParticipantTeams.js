(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.StackMeetParticipantTeams = api;
})(typeof window !== "undefined" ? window : null, function () {
  "use strict";

  // Existing operator-side helpers. Server authorization and integrity rules remain authoritative.
  function stackerIdNumber(id) {
    const match = /^(\d+)\.(\d+)$/.exec(String(id || ""));
    return match ? Number(match[1]) * 100000 + Number(match[2]) : Number.MAX_SAFE_INTEGER;
  }

  function compareStackers(a, b, key) {
    if (key === "id") return stackerIdNumber(a.id) - stackerIdNumber(b.id);
    if (key === "age") return Number(a.age || 0) - Number(b.age || 0);
    return String(a[key] || "").localeCompare(String(b[key] || ""), undefined, { numeric: true, sensitivity: "base" });
  }

  function registeredDoubleMemberIds(team) {
    return [team.one, team.two, team.childStackerId, team.parentStackerId].filter(Boolean);
  }

  function relayMemberIds(team) {
    if (Array.isArray(team?.members)) return team.members.filter(Boolean);
    return [team?.one, team?.two, team?.three, team?.four, team?.five, team?.six].filter(Boolean);
  }

  function relayCanCompete(team) {
    return relayMemberIds(team).length >= 4;
  }

  function relayTeamStatus(team) {
    const memberCount = relayMemberIds(team).length;
    if (memberCount === 0) return "Draft";
    if (memberCount < 4) return "Incomplete";
    return "Ready";
  }

  function validateDoubleEntry({ type, status, one, two, parentName }) {
    if (!one) return "Please choose the first stacker.";
    if (type === "normal" && status === "complete" && !two) return "Normal doubles needs two registered stackers.";
    if (type === "child_parent" && !two && !parentName) return "Child/Parent needs a registered parent or external parent name.";
    if (two && one === two) return "A stacker cannot partner with themselves.";
    return "";
  }

  function validateRelayEntry(members, relayName = "", relays, editingRelayId) {
    if (!relayName) return "Relay team name is required.";
    const duplicateName = relays.some(team => team.id !== editingRelayId && String(team.name || "").trim().toLowerCase() === relayName.toLowerCase());
    if (duplicateName) return "Relay team name must be unique.";
    if (members.length > 6) return "Relay team can only keep up to 6 registered stackers.";
    const duplicate = members.find((id, index) => members.indexOf(id) !== index);
    if (duplicate) return "A stacker can only be selected once in this relay team.";
    return "";
  }

  function nextTeamCode(prefix, state) {
    const max = state.stackers
      .concat(state.doubles)
      .concat(state.relays || [])
      .map(item => new RegExp(`^${prefix}\\.(\\d+)$`).exec(item.id)?.[1])
      .filter(Boolean)
      .map(Number)
      .reduce((highest, value) => Math.max(highest, value), 0);
    return `${prefix}.${max + 1}`;
  }

  return Object.freeze({ stackerIdNumber, compareStackers, registeredDoubleMemberIds, relayMemberIds, relayCanCompete, relayTeamStatus, validateDoubleEntry, validateRelayEntry, nextTeamCode });
});
