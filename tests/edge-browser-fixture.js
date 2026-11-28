"use strict";
// Disposable loopback HTTP fixture. No real accounts, production endpoints,
// or external QR service. API writes are rejected except synthetic login.
const fs = require("node:fs");
const path = require("node:path");
const http = require("node:http");
const root = path.resolve(__dirname, "../backend/StackMeet.Api/wwwroot");
const name = "ENG06D Synthetic Competition";
const competition = { id: 7, competitionId: 7, competitionKey: "ENG06D", competitionCode: "ENG06D", competitionName: name, name, venue: "Fixture", status: "Active", startDate: "2026-08-01", endDate: "2026-08-02" };
const people = [
  { id: 1, stackerCode: "1.1", firstName: "Alpha", lastName: "Fixture", gender: "Female", birthDate: "2014-01-01", country: "Malaysia", club: "Synthetic", paid: "Yes", checkedIn: "Yes" },
  { id: 2, stackerCode: "1.2", firstName: "Incomplete", lastName: "Fixture", gender: "Female", birthDate: "2014-01-02", country: "Malaysia", club: "Synthetic", paid: "Yes", checkedIn: "Yes" }
];
const rows = people.flatMap((person, p) => ["Prelims", "Finals"].flatMap(stage => ["3-3-3", "3-6-3", "Cycle"].filter((_, i) => !(p === 1 && stage === "Finals" && i === 2)).map((event, i) => ({
  id: [person.stackerCode, stage, event].join("-"), participant: person.stackerCode, type: "Individual", stage, event,
  attempts: [stage === "Prelims" ? i + 1 : i + 5 - p], penalty: 0, revision: 2
}))));
const state = { settings: { name, language: "en", start: competition.startDate, end: competition.endDate, advanceIndividuals: 8 }, stackers: [], results: [], doubles: [], relays: [], notifications: [], auditLogs: [] };
const payload = { competition: { ...competition, id: "ENG06D", isOfficial: false }, settings: state.settings, translations: {}, lastUpdatedAt: "2026-08-01T01:00:00Z", stackers: people.map(p => ({ id: p.stackerCode, name: p.firstName + " " + p.lastName, division: "U12 Female", gender: "F", org: p.club })), results: rows, doubles: [], relays: [], divisions: ["U12 Female"] };
const mime = { ".html": "text/html", ".js": "application/javascript", ".css": "text/css", ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon", ".jpg": "image/jpeg", ".woff2": "font/woff2" };
function fixture() {
  const info = { withdrawn: false, empty: false, calls: [], unexpected: [] };
  function json(res, value, status = 200, headers = {}) { res.writeHead(status, { "Content-Type": "application/json", ...headers }); res.end(JSON.stringify(value)); }
  const server = http.createServer((req, res) => {
    const key = new URL(req.url, "http://127.0.0.1").pathname;
    const method = req.method;
    if (key.startsWith("/api/") || key.startsWith("/hubs/")) info.calls.push({ method, path: key });
    if (key === "/api/auth/login" && method === "POST") {
      req.resume();
      return json(res, { token: "fixture-session", userId: 1, displayName: "Synthetic operator", expiresAt: "2099-01-01T00:00:00Z", competitionAccess: [{ competitionId: 7, competitionKey: "ENG06D", competitionName: name, role: "Manager" }] });
    }
    if (key === "/api/public/competitions/ENG06D/results" && method === "GET")
      return info.withdrawn ? json(res, {}, 404) : json(res, info.empty ? { ...payload, results: [] } : payload);
    if (key.startsWith("/hubs/") || /\/negotiate$/.test(key)) return json(res, {}, 404);
    if (key.startsWith("/api/")) {
      if (method !== "GET" || req.headers.authorization !== "Bearer fixture-session") {
        info.unexpected.push(method + " " + key); return json(res, {}, 403);
      }
      if (key === "/api/state/ENG06D") return json(res, state, 200, { ETag: '"4"' });
      if (key === "/api/competitions") return json(res, [competition]);
      if (key === "/api/competitions/finals-ranking-rules") return json(res, []);
      if (key === "/api/competitions/7/activity") return json(res, { capabilities: { supportsLiveResults: true, supportsTeamEntries: true } });
      if (key === "/api/competitions/7/stackers") return json(res, people);
      if (key === "/api/competitions/7/results") return json(res, { revision: 2, results: rows });
      if (key === "/api/competitions/7/assets") return json(res, []);
      info.unexpected.push(method + " " + key); return json(res, {}, 404);
    }
    if (method !== "GET") { info.unexpected.push(method + " " + key); return json(res, {}, 405); }
    const relative = /^\/ENG06D\/Results(?:\/[^/]+)?\/?$/i.test(key) ? "results/index.html" : key === "/" ? "index.html" : key.slice(1);
    const file = path.resolve(root, relative);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { "Content-Type": mime[path.extname(file)] || "application/octet-stream", "Cache-Control": "no-store",
      "Content-Security-Policy": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self'; font-src 'self' data:; object-src 'none'" });
    fs.createReadStream(file).pipe(res);
  });
  return { server, info, name };
}
module.exports = { fixture };
