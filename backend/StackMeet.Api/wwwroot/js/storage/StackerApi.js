/** Same-origin client for SQL-native Competition and Individual Stacker records. */
class StackerApi {
  async listCompetitions() {
    const [competitions, rankingRules] = await Promise.all([
      this.#request("/api/competitions"),
      this.#request("/api/competitions/finals-ranking-rules"),
      this.#ensureFinalsRankingPolicy()
    ]);

    if (typeof window !== "undefined") {
      const registry = Object.create(null);
      (Array.isArray(rankingRules) ? rankingRules : []).forEach(item => {
        const competitionId = Number(item?.competitionId);
        if (!Number.isInteger(competitionId) || competitionId <= 0) return;
        registry[String(competitionId)] = item?.ruleVersion;
      });
      window.StackMeetFinalsRankingRules = Object.freeze(registry);
    }

    return competitions;
  }
  async createCompetition(competition) { return this.#request("/api/competitions", { method: "POST", body: competition }); }
  async list(competitionId) { return this.#request(this.#stackersUrl(competitionId)); }
  async get(competitionId, id) { return this.#request(`${this.#stackersUrl(competitionId)}/${encodeURIComponent(id)}`); }
  async create(competitionId, stacker) { return this.#request(this.#stackersUrl(competitionId), { method: "POST", body: stacker }); }
  async update(competitionId, id, stacker) { await this.#request(`${this.#stackersUrl(competitionId)}/${encodeURIComponent(id)}`, { method: "PUT", body: stacker, empty: true }); }
  async delete(competitionId, id) { await this.#request(`${this.#stackersUrl(competitionId)}/${encodeURIComponent(id)}`, { method: "DELETE", empty: true }); }

  #stackersUrl(competitionId) {
    if (!Number.isInteger(Number(competitionId)) || Number(competitionId) <= 0) throw new TypeError("A SQL-native competition id is required.");
    return `/api/competitions/${encodeURIComponent(competitionId)}/stackers`;
  }

  async #ensureFinalsRankingPolicy() {
    if (typeof window === "undefined" || window.StackMeetFinalsRankingPolicy) return;
    if (typeof document === "undefined") throw new Error("Finals ranking policy module is unavailable.");
    if (window.StackMeetFinalsRankingPolicyLoad) return window.StackMeetFinalsRankingPolicyLoad;

    window.StackMeetFinalsRankingPolicyLoad = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = "/js/reports/FinalsRankingPolicy.js?v=stacker-identity-v1-sp4h";
      script.async = true;
      script.dataset.stackMeetFinalsRankingPolicy = "sp4h";
      script.addEventListener("load", () => {
        if (window.StackMeetFinalsRankingPolicy) resolve();
        else reject(new Error("Finals ranking policy module loaded without registering its contract."));
      }, { once: true });
      script.addEventListener("error", () => reject(new Error("Unable to load Finals ranking policy module.")), { once: true });
      document.head.appendChild(script);
    });

    return window.StackMeetFinalsRankingPolicyLoad;
  }

  async #request(url, options = {}) {
    const response = await fetch(url, {
      method: options.method || "GET",
      headers: {
        Accept: "application/json",
        ...(typeof window !== "undefined" ? window.StackMeetAuth?.authHeaders?.() || {} : {}),
        ...(options.body ? { "Content-Type": "application/json" } : {})
      },
      ...(options.body ? { body: JSON.stringify(options.body) } : {})
    });
    if (!response.ok) throw await this.#requestError(response);
    return options.empty ? undefined : response.json();
  }

  async #requestError(response) {
    const detail = await response.text();
    let message = detail;
    try { message = JSON.parse(detail).error || detail; } catch (_) { /* use text detail */ }
    const error = new Error(`StackMeet API request failed (${response.status})${message ? `: ${message}` : ""}`);
    error.status = response.status;
    throw error;
  }
}

if (typeof window !== "undefined") {
  window.StackMeetStorage = window.StackMeetStorage || {};
  window.StackMeetStorage.StackerApi = StackerApi;
}
if (typeof module !== "undefined" && module.exports) module.exports = StackerApi;