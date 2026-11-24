/** SQL-native result persistence client. */
class SqlResultApi {
  #cache = new Map();
  #generation = new Map();
  #minimumRevision = new Map();
  #blocked = new Set();
  #writing = new Set();

  async list(competitionId) {
    const key = this.#competitionKey(competitionId);
    const generation = this.#generation.get(key);
    const snapshot = await this.#request(this.#url(key));
    const cached = this.#cache.get(key);
    if (generation !== this.#generation.get(key) || snapshot.revision < (this.#minimumRevision.get(key) ?? 0)) {
      if (cached && !this.#blocked.has(key)) return this.#clone(cached);
      throw new Error("Result snapshot was superseded. Refresh again.");
    }
    if (cached && cached.revision > snapshot.revision) return this.#clone(cached);
    this.#cache.set(key, snapshot);
    return this.#clone(snapshot);
  }

  async review(competitionId) {
    return this.list(competitionId);
  }

  acceptReview(competitionId, revision) {
    const key = this.#competitionKey(competitionId);
    if (this.#cache.get(key)?.revision !== revision) return false;
    this.#blocked.delete(key);
    return true;
  }

  async saveBatch(competitionId, payload) {
    const key = this.#competitionKey(competitionId);
    if (this.#writing.has(key)) throw this.#outcome("write-in-progress");
    this.#writing.add(key);
    try {
      return await this.#saveBatch(key, payload);
    } finally {
      this.#writing.delete(key);
    }
  }

  async #saveBatch(key, payload) {
    if (this.#blocked.has(key)) {
      throw this.#outcome("review-required");
    }

    let delta;
    try {
      delta = await this.#request(`${this.#url(key)}/batch`, { method: "POST", body: payload });
    } catch (error) {
      if (error.status >= 400 && error.status < 500 && error.status !== 408) {
        error.outcome = "rejected";
        throw error;
      }
      if (error.commitConfirmed) {
        this.#invalidate(key);
        throw this.#outcome("committed-refresh-failed");
      }
      this.#invalidate(key);
      throw this.#outcome("unknown");
    }

    const cached = this.#cache.get(key);
    this.#minimumRevision.set(key, Math.max(delta.revision, this.#minimumRevision.get(key) ?? 0));
    if (cached && cached.revision >= delta.revision && !this.#blocked.has(key)) return this.#clone(cached);

    if (!cached || delta.revision !== cached.revision + 1 || this.#blocked.has(key)) {
      this.#invalidate(key);
      try {
        const snapshot = await this.list(key);
        this.acceptReview(key, snapshot.revision);
        return snapshot;
      } catch {
        throw this.#outcome("committed-refresh-failed");
      }
    }

    const changedByKey = new Map(delta.results.map(item => [this.#logicalKey(item), item]));
    const deletedKeys = new Set((payload.deletes || []).map(item => this.#logicalKey(item)));
    const merged = cached.results.filter(item => {
      const logicalKey = this.#logicalKey(item);
      return !changedByKey.has(logicalKey) && !deletedKeys.has(logicalKey);
    });
    merged.push(...delta.results);

    const snapshot = { revision: delta.revision, results: merged };
    this.#cache.set(key, this.#clone(snapshot));
    return this.#clone(snapshot);
  }

  #invalidate(key) {
    this.#cache.delete(key);
    this.#generation.set(key, (this.#generation.get(key) ?? 0) + 1);
    this.#blocked.add(key);
  }

  #outcome(outcome) {
    return Object.assign(new Error(outcome), { outcome });
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
      revision: payload.revision,
      results: payload.results.map(item => ({ ...item, attempts: [...item.attempts] }))
    };
  }

  #url(id) {
    return `/api/competitions/${encodeURIComponent(this.#competitionKey(id))}/results`;
  }

  async #request(url, options = {}) {
    const response = await fetch(url, { method: options.method || "GET", headers: { Accept: "application/json", ...(window.StackMeetAuth?.authHeaders?.() || {}), ...(options.body ? { "Content-Type": "application/json" } : {}) }, ...(options.body ? { body: JSON.stringify(options.body) } : {}) });
    if (!response.ok) {
      const detail = await response.text().catch(() => "");
      let problem;
      try { problem = JSON.parse(detail); } catch (_) { /* retain HTTP status */ }
      throw Object.assign(new Error(problem?.error || `HTTP ${response.status}`), { status: response.status, code: problem?.code });
    }
    try {
      const value = await response.json();
      if (!Number.isSafeInteger(value.revision) || value.revision < 0 || !Array.isArray(value.results)) {
        throw new Error("Invalid result snapshot.");
      }
      return { revision: value.revision, results: value.results.map(item => ({ id: item.id, stage: item.stage, type: item.type || item.participantType, participant: item.participant || item.participantCode, event: item.event || item.eventCode, attempts: Array.isArray(item.attempts) ? item.attempts : [], penalty: Number(item.penalty || 0), revision: Number(item.revision || 0) })) };
    } catch (error) {
      error.commitConfirmed = options.method === "POST";
      throw error;
    }
  }
}
if (typeof window !== "undefined") { window.StackMeetStorage = window.StackMeetStorage || {}; window.StackMeetStorage.ResultApi = SqlResultApi; }
if (typeof module !== "undefined" && module.exports) module.exports = SqlResultApi;
