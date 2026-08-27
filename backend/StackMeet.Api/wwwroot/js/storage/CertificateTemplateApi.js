/* Certificate template API and the single controlled catalogue used by the UI. */
const CertificateTemplateCatalogue = Object.freeze({
  types: Object.freeze([
    Object.freeze({ value: "Participation", label: "Participation", supported: true }),
    Object.freeze({ value: "Achievement", label: "Achievement", supported: false }),
    Object.freeze({ value: "Official", label: "Official", supported: false })
  ]),
  placeholders: Object.freeze([
    "ParticipantName", "CompetitionName", "CompetitionDate", "CompetitionStartDate", "CompetitionEndDate",
    "Venue", "Organization", "Country", "Region", "Division", "CertificateType", "IssueDate",
    "Event", "Placement", "Result"
  ])
});

class CertificateTemplateApi {
  static maxBytes = 8 * 1024 * 1024;

  static validateFile(file) {
    const errors = [];
    const name = String(file?.name || "");
    if (!name.toLowerCase().endsWith(".docx")) errors.push("Select a DOCX file.");
    if (file?.type && file.type !== "application/vnd.openxmlformats-officedocument.wordprocessingml.document") errors.push("The selected file is not a DOCX document.");
    if (Number(file?.size || 0) > CertificateTemplateApi.maxBytes) errors.push("DOCX files must be 8 MB or smaller.");
    if (!file) errors.push("Choose a DOCX template first.");
    return errors;
  }

  async list(competitionId) { return this.#request(this.#url(competitionId)); }
  async upload(competitionId, { certificateType, name, file }) {
    const errors = CertificateTemplateApi.validateFile(file);
    if (errors.length) { const error = new Error(errors.join(" ")); error.validationErrors = errors; throw error; }
    const body = new FormData();
    body.append("certificateType", certificateType);
    body.append("name", name);
    body.append("file", file, file.name);
    return this.#request(this.#url(competitionId), { method: "POST", body });
  }
  async activate(competitionId, templateId) { return this.#request(`${this.#url(competitionId)}/${encodeURIComponent(templateId)}/activate`, { method: "POST" }); }
  async preview(competitionId, templateId, participantCode) {
    return this.#request(`${this.#url(competitionId)}/${encodeURIComponent(templateId)}/preview`, { method: "POST", body: { participantCode }, binary: true });
  }
  #url(competitionId) { return `/api/competitions/${encodeURIComponent(competitionId)}/certificate-templates`; }
  async #request(url, options = {}) {
    const headers = { Accept: options.binary ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/json", ...(window.StackMeetAuth?.authHeaders?.() || {}) };
    if (options.body && !(options.body instanceof FormData)) headers["Content-Type"] = "application/json";
    const response = await fetch(url, { method: options.method || "GET", headers, ...(options.body ? { body: options.body instanceof FormData ? options.body : JSON.stringify(options.body) } : {}) });
    if (!response.ok) { let message = "Certificate request failed."; const text = await response.text(); try { message = JSON.parse(text).error || message; } catch (_) {} const error = new Error(`${message} (${response.status})`); error.status = response.status; throw error; }
    return options.binary ? response.blob() : response.json();
  }
}

window.StackMeetStorage = window.StackMeetStorage || {};
window.StackMeetStorage.CertificateTemplateApi = CertificateTemplateApi;
window.StackMeetStorage.CertificateTemplateCatalogue = CertificateTemplateCatalogue;
if (typeof module !== "undefined" && module.exports) module.exports = { CertificateTemplateApi, CertificateTemplateCatalogue };
