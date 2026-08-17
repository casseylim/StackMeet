/** SQL-native result persistence client. */
class SqlResultApi {
  async list(competitionId) { return this.#request(this.#url(competitionId)); }
  async saveBatch(competitionId, payload) { return this.#request(`${this.#url(competitionId)}/batch`, { method: "POST", body: payload }); }
  #url(id) {
    if (!Number.isInteger(Number(id)) || Number(id) <= 0) throw new TypeError("A SQL-native competition id is required.");
    return `/api/competitions/${encodeURIComponent(id)}/results`;
  }
  async #request(url, options = {}) {
    const response = await fetch(url, { method: options.method || "GET", headers: { Accept: "application/json", ...(window.StackMeetAuth?.authHeaders?.() || {}), ...(options.body ? { "Content-Type": "application/json" } : {}) }, ...(options.body ? { body: JSON.stringify(options.body) } : {}) });
    if (!response.ok) {
      const detail = await response.text(); let message = detail;
      try { message = JSON.parse(detail).error || detail; } catch (_) { /* retain text */ }
      const error = new Error(`Result API request failed (${response.status})${message ? `: ${message}` : ""}`); error.status = response.status; throw error;
    }
    const value = await response.json();
    return { revision: Number(value.revision || 0), results: (value.results || []).map(item => ({ id: item.id, stage: item.stage, type: item.type || item.participantType, participant: item.participant || item.participantCode, event: item.event || item.eventCode, attempts: Array.isArray(item.attempts) ? item.attempts : [], penalty: Number(item.penalty || 0), revision: Number(item.revision || 0) })) };
  }
}
if (typeof window !== "undefined") { window.StackMeetStorage = window.StackMeetStorage || {}; window.StackMeetStorage.ResultApi = SqlResultApi; }
if (typeof module !== "undefined" && module.exports) module.exports = SqlResultApi;
