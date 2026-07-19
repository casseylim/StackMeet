/**
 * Online persistence transport for the existing full competition state.
 */
class ApiProvider {
  constructor(competitionKey, apiBaseUrl = "") {
    if (!competitionKey) throw new TypeError("A competition key is required.");
    this.competitionKey = String(competitionKey);
    this.apiBaseUrl = String(apiBaseUrl).replace(/\/$/, "");
  }

  async load() {
    if (this.#useLocalStorage()) {
      const saved = localStorage.getItem(this.#localFileKey());
      return saved ? JSON.parse(saved) : null;
    }

    const response = await fetch(this.#stateUrl(), {
      headers: { Accept: "application/json", ...this.#authHeaders() }
    });
    if (response.status === 404) return null;
    if (!response.ok) throw await this.#requestError(response);
    return response.json();
  }

  async save(state) {
    if (this.#useLocalStorage()) {
      localStorage.setItem(this.#localFileKey(), JSON.stringify(state));
      return;
    }

    const response = await fetch(this.#stateUrl(), {
      method: "POST",
      headers: { "Content-Type": "application/json", ...this.#authHeaders() },
      body: JSON.stringify(state)
    });
    if (!response.ok) throw await this.#requestError(response);
  }

  #authHeaders() {
    return typeof window !== "undefined" ? window.StackMeetAuth?.authHeaders?.() || {} : {};
  }

  #stateUrl() {
    return `${this.apiBaseUrl}/api/state/${encodeURIComponent(this.competitionKey)}`;
  }

  #useLocalStorage() {
    return typeof location !== "undefined" && location.protocol === "file:";
  }

  #localFileKey() {
    return `stackmeet-file-test-state-${this.competitionKey}`;
  }

  async #requestError(response) {
    const detail = await response.text();
    return new Error(`StackMeet API request failed (${response.status})${detail ? `: ${detail}` : ""}`);
  }
}

if (typeof window !== "undefined") {
  window.StackMeetStorage = window.StackMeetStorage || {};
  window.StackMeetStorage.ApiProvider = ApiProvider;
}
if (typeof module !== "undefined" && module.exports) module.exports = ApiProvider;