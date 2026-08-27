(() => {
  "use strict";

  // Renders the privacy-filtered public competition directory.
  const status = document.getElementById("directoryStatus");
  const directory = document.getElementById("competitionDirectory");
  const esc = value => String(value ?? "").replace(/[&<>\"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[char]));
  const date = value => value ? new Intl.DateTimeFormat("en-MY", { timeZone: "Asia/Kuala_Lumpur", dateStyle: "medium" }).format(new Date(`${value}T00:00:00+08:00`)) : "";

  fetch("/api/public/competitions", { headers: { Accept: "application/json" }, cache: "no-store" })
    .then(response => { if (!response.ok) throw new Error("The public competition list is unavailable."); return response.json(); })
    .then(items => {
      status.textContent = items.length ? `${items.length} public competition${items.length === 1 ? "" : "s"}` : "No competitions have been published yet.";
      directory.innerHTML = items.map(item => {
        const url = `https://naditrack.com/${encodeURIComponent(item.id)}/Results`;
        return `<article class="summary-card"><span class="card-label">${esc(date(item.startDate))} - ${esc(date(item.endDate))}</span><strong>${esc(item.name)}</strong><span>${esc(item.venue)} · ${esc(item.status)}</span><a class="back-link" href="${url}">View results</a></article>`;
      }).join("");
    })
    .catch(error => { status.textContent = error.message; });
})();
