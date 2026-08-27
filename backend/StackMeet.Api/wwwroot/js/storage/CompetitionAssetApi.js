class CompetitionAssetApi {
  async list(competitionId) { return this.#request(`/api/competitions/${encodeURIComponent(competitionId)}/assets`); }
  async upload(competitionId, type, file) {
    const body = new FormData(); body.append("file", file, file.name);
    return this.#request(`/api/competitions/${encodeURIComponent(competitionId)}/assets/${encodeURIComponent(type)}`, { method: "POST", body });
  }
  async remove(competitionId, type) { return this.#request(`/api/competitions/${encodeURIComponent(competitionId)}/assets/${encodeURIComponent(type)}`, { method: "DELETE", empty: true }); }
  async #request(url, options = {}) {
    const response = await fetch(url, { method: options.method || "GET", headers: { Accept: "application/json", ...(window.StackMeetAuth?.authHeaders?.() || {}) }, ...(options.body ? { body: options.body } : {}) });
    if (!response.ok) { const detail = await response.text(); let message = detail; try { message = JSON.parse(detail).error || detail; } catch (_) {} const error = new Error(`Competition asset request failed (${response.status})${message ? `: ${message}` : ""}`); error.status = response.status; throw error; }
    return options.empty ? undefined : response.json();
  }
}
window.StackMeetStorage = window.StackMeetStorage || {};
window.StackMeetStorage.CompetitionAssetApi = CompetitionAssetApi;
