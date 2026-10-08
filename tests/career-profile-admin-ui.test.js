const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const createAdmin = require("../backend/StackMeet.Api/wwwroot/career-admin.js");
class Element {
  constructor() { this.value = ""; this.hidden = false; this.disabled = false; this.children = []; this.listeners = {}; this.textContent = ""; }
  addEventListener(type, handler) { this.listeners[type] = handler; }
  appendChild(child) { this.children.push(child); }
  replaceChildren() { this.children = []; }
  removeAttribute(name) { delete this[name]; }
}
function fixture() {
  const nodes = new Map();
  const document = { getElementById(id) { if (!nodes.has(id)) nodes.set(id, new Element()); return nodes.get(id); }, createElement() { return new Element(); } };
  const $ = id => document.getElementById(id);
  const calls = [], confirmations = [];
  const capabilities = { authenticated: true, isSystemAdmin: true, canCreateIdentity: true, canLinkIdentity: true, canPublishProfile: true, canUnpublishProfile: true, canUnlinkIdentity: true };
  const state = { accept: true, capabilities, link: null, identity: { nadiTrackId: "NDT-2345678", displayName: "Synthetic Person", isPublicProfile: false }, createAllowed: true, candidates: [], reject: false };
  const admin = createAdmin(document, text => { confirmations.push(text); return state.accept; });
  const request = async (url, options = {}) => {
    calls.push({ url, ...options });
    if (state.reject) throw new Error("Forbidden");
    if (url.endsWith("/capabilities")) return state.capabilities;
    if (options.method) {
      const body = JSON.parse(options.body);
      if (url.endsWith("/links")) state.link = { linkId: 7, nadiTrackId: state.identity.nadiTrackId, isPublicProfile: false, displayName: state.identity.displayName };
      else if (url.endsWith("/publication")) state.link.isPublicProfile = body.isPublicProfile;
      else if (url.endsWith("/unlink")) state.link = null;
      return {};
    }
    if (url.includes("/stackers?")) return { total: 1, items: [{ stackerId: 62, stackerCode: "1.1", displayName: "Synthetic <script>name</script>", nadiTrackId: state.link?.nadiTrackId }] };
    if (url.endsWith("/stackers/62")) return { stackerId: 62, stackerCode: "1.1", displayName: "Synthetic Person", competition: { status: "Closed" },
      proposedIdentity: { firstName: "Synthetic", lastName: "Person", isPublicProfile: false }, contactCopyNotice: "Contact values preserved.",
      link: state.link ? { ...state.link } : null, createAllowed: state.createAllowed, matchingCandidates: state.candidates };
    if (url.includes("/profiles/")) return { ...state.identity };
    throw new Error("Unexpected route " + url);
  };
  admin.connect(request);
  return { admin, state, $, calls, confirmations, request };
}
(async () => {
  const f = fixture(), writes = () => f.calls.filter(c => c.method);
  assert.equal(f.$("careerTab").hidden, true, "capabilities required before showing management");
  await f.admin.mutate("create"); assert.equal(writes().length, 0);
  await f.admin.refresh(); assert.equal(f.$("careerTab").hidden, false);
  f.$("careerCompetition").value = "18"; await f.admin.search();
  assert.match(f.$("careerRows").children[0].children[1].textContent, /<script>/, "untrusted names are text, not markup");
  await f.admin.select(62); f.$("careerReason").value = "Synthetic controlled test";
  f.state.accept = false; await f.admin.mutate("create"); assert.equal(writes().length, 0, "cancelled confirmation never writes");
  assert.match(f.confirmations[0], /CompetitionStacker #62/); assert.match(f.confirmations[0], /Default publication state = PRIVATE/);
  f.state.accept = true; await f.admin.mutate("create");
  assert.equal(JSON.parse(writes()[0].body).requestedAction, 2); assert.equal(f.state.link.isPublicProfile, false);
  assert.equal(f.$("careerPublicUrl").hidden, true, "private identity has no public link");
  f.$("careerReason").value = "Publish after separate review"; await f.admin.mutate("publish");
  assert.match(f.confirmations.at(-1), /This will make the Career Profile publicly accessible\./);
  assert.equal(f.$("careerPublicUrl").hidden, false); assert.equal(f.$("careerPublicUrl").href, "/Stackers/NDT-2345678");
  f.$("careerReason").value = "Remove public access"; await f.admin.mutate("unpublish");
  assert.match(f.confirmations.at(-1), /This will remove public access but preserve the identity and competition links\./);
  assert.equal(f.$("careerPublicUrl").hidden, true); assert.equal(f.state.link.isPublicProfile, false);
  f.$("careerReason").value = "Remove only relationship"; await f.admin.mutate("unlink");
  assert.match(f.confirmations.at(-1), /This removes only the identity relationship\. Competition registration and results will remain unchanged\./);
  assert.deepEqual(JSON.parse(writes().at(-1).body), { stackerId: 62, nadiTrackId: "NDT-2345678", reason: "Remove only relationship" });
  f.$("careerIdentity").value = "NDT-2345678"; await f.admin.lookup(); f.$("careerReason").value = "Link reviewed existing person";
  await f.admin.mutate("link"); assert.equal(JSON.parse(writes().at(-1).body).requestedAction, 1);
  assert.match(f.confirmations.at(-1), /Competition results themselves will not be modified/);
  assert.equal(f.state.link.isPublicProfile, false, "link remains private");
  const noReason = writes().length; await assert.rejects(f.admin.mutate("publish"), /reason/); assert.equal(writes().length, noReason);
  f.admin.clear(); assert.equal(f.$("careerTab").hidden, true); assert.equal(f.$("careerPublicUrl").hidden, true);
  f.state.capabilities = { authenticated: true, isSystemAdmin: false }; await f.admin.refresh();
  assert.equal(f.$("careerControls").hidden, true); await f.admin.select(62); await f.admin.mutate("create"); assert.equal(writes().length, noReason);
  f.state.reject = true; await assert.rejects(f.admin.refresh(), /Forbidden/); assert.equal(f.$("careerControls").hidden, true);

  const placeholder = fixture(); await placeholder.admin.refresh(); placeholder.state.createAllowed = false; await placeholder.admin.select(62);
  placeholder.$("careerReason").value = "Blocked source"; assert.equal(placeholder.$("careerCreate").disabled, true);
  await assert.rejects(placeholder.admin.mutate("create"), /source-name/); assert.equal(placeholder.calls.filter(c => c.method).length, 0);

  const duplicate = fixture(); await duplicate.admin.refresh(); duplicate.state.candidates = [{ nadiTrackId: "NDT-2345678", displayName: "Candidate" }];
  await duplicate.admin.select(62); duplicate.$("careerReason").value = "No duplicate creation";
  await assert.rejects(duplicate.admin.mutate("create"), /duplicate/); assert.equal(duplicate.calls.filter(c => c.method).length, 0);

  const race = fixture(); let release;
  race.admin.connect(async (url, options) => { if (options?.method) await new Promise(resolve => { release = resolve; }); return race.request(url, options); });
  await race.admin.refresh(); await race.admin.select(62); race.$("careerReason").value = "Single in-flight mutation";
  const pending = race.admin.mutate("create"); await race.admin.mutate("create");
  assert.equal(race.confirmations.length, 1, "double click cannot send a second mutation"); release(); await pending;
  assert.equal(race.calls.filter(c => c.method).length, 1);

  const stale = fixture(); let releaseCapabilities;
  stale.admin.connect(url => url.endsWith("/capabilities") ? new Promise(resolve => { releaseCapabilities = resolve; }) : stale.request(url));
  const loading = stale.admin.refresh(); stale.admin.clear(); releaseCapabilities(stale.state.capabilities); await loading;
  assert.equal(stale.$("careerTab").hidden, true, "late capability response cannot restore logged-out controls");

  const html = fs.readFileSync(path.join(__dirname, "../backend/StackMeet.Api/wwwroot/admin.html"), "utf8");
  for (const id of ["careerTab", "careerControls", "careerSearchForm", "careerCreate", "careerLink", "careerPublish", "careerUnpublish", "careerUnlink"]) assert.match(html, new RegExp(`id="${id}"`));
  assert.ok(html.indexOf('src="career-admin.js') < html.indexOf('src="admin.js'), "career module loads before authenticated bridge");
  console.log("CAREER_PROFILE_ADMIN_UI_TESTS=PASS: capability gating, safe rendering, all confirmations, private defaults, link/unlink, denied authorization, double clicks and stale responses");
})().catch(error => { console.error(error); process.exitCode = 1; });
