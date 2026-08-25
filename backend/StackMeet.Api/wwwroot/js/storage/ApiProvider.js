/**
 * Online persistence transport for the existing full competition state.
 */
class ApiProvider {
  constructor(competitionKey, apiBaseUrl = "") {
    if (!competitionKey) throw new TypeError("A competition key is required.");
    this.competitionKey = String(competitionKey);
    this.apiBaseUrl = String(apiBaseUrl).replace(/\/$/, "");
    this.stateEtag = null;
  }

  async load(options = {}) {
    if (this.#useLocalStorage()) {
      const saved = localStorage.getItem(this.#localFileKey());
      return saved ? JSON.parse(saved) : null;
    }

    const response = await fetch(this.#stateUrl(), {
      headers: { Accept: "application/json", ...this.#authHeaders() }
    });
    const acceptRevision = this.stateEtag === null || options.acceptRevision === true;
    if (response.status === 404) {
      if (acceptRevision) this.stateEtag = '"0"';
      return null;
    }
    if (!response.ok) throw await this.#requestError(response);
    if (acceptRevision) this.#captureEtag(response);
    return response.json();
  }

  reloadLatest() {
    return this.load({ acceptRevision: true });
  }

  async save(state) {
    if (this.#useLocalStorage()) {
      localStorage.setItem(this.#localFileKey(), JSON.stringify(state));
      return;
    }

    const response = await fetch(this.#stateUrl(), {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "If-Match": this.stateEtag || '"0"',
        ...this.#authHeaders()
      },
      body: JSON.stringify(state)
    });
    if (!response.ok) throw await this.#requestError(response);
    this.#captureEtag(response);
  }

  #captureEtag(response) {
    const etag = response.headers?.get?.("ETag");
    if (etag) this.stateEtag = etag;
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
    let message = detail;
    let currentRevision = null;
    try {
      const parsed = JSON.parse(detail);
      message = parsed.error || detail;
      currentRevision = parsed.currentRevision ?? null;
    } catch (_) { /* use text detail */ }
    const error = new Error(`StackMeet API request failed (${response.status})${message ? `: ${message}` : ""}`);
    error.status = response.status;
    error.currentRevision = currentRevision;
    error.etag = response.headers?.get?.("ETag") || null;
    return error;
  }
}

if (typeof window !== "undefined") {
  window.StackMeetStorage = window.StackMeetStorage || {};
  window.StackMeetStorage.ApiProvider = ApiProvider;
}
if (typeof module !== "undefined" && module.exports) module.exports = ApiProvider;