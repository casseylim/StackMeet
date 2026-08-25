/** SQL-native result persistence client. */
class SqlResultApi {
  #cache = new Map();

  async list(competitionId) {
    const key = this.#competitionKey(competitionId);
    const payload = await this.#request(this.#url(competitionId));
    const snapshot = this.#clone(payload);
    this.#cache.set(key, snapshot);
    return this.#clone(snapshot);
  }

  async saveBatch(competitionId, payload) {
    const key = this.#competitionKey(competitionId);
    const delta = await this.#request(`${this.#url(competitionId)}/batch`, { method: "POST", body: payload });
    const cached = this.#cache.get(key);
    if (!cached) return this.list(competitionId);

    const changedByKey = new Map((delta.results || []).map(item => [this.#logicalKey(item), item]));
    const deletedKeys = new Set((payload.deletes || []).map(item => this.#logicalKey(item)));
    const merged = cached.results.filter(item => {
      const logicalKey = this.#logicalKey(item);
      return !changedByKey.has(logicalKey) && !deletedKeys.has(logicalKey);
    });
    merged.push(...(delta.results || []));

    const snapshot = { revision: delta.revision, results: merged };
    this.#cache.set(key, this.#clone(snapshot));
    return this.#clone(snapshot);
  }

  #competitionKey(id) {
    const value = Number(id);
    if (!Number.isInteger(value) || value <= 0) throw new TypeError("A SQL-native competition id is required.");
    return value;
  }

  #logicalKey(item) {
    return [item.stage, item.type || item.participantType, item.participant || item.participantCode, item.event || item.eventCode]
      .map(value => String(value || "").trim().toUpperCase())
      .join("\u001f");
  }

  #clone(payload) {
    return {
      revision: Number(payload.revision || 0),
      results: (payload.results || []).map(item => ({ ...item, attempts: Array.isArray(item.attempts) ? [...item.attempts] : [] }))
    };
  }

  #url(id) {
    return `/api/competitions/${encodeURIComponent(this.#competitionKey(id))}/results`;
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
