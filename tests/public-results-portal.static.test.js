const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");

const program = read("backend/StackMeet.Api/Program.cs");
const stateController = read("backend/StackMeet.Api/Controllers/CompetitionStateController.cs");
const publicController = read("backend/StackMeet.Api/Controllers/PublicResultsController.cs");
const hub = read("backend/StackMeet.Api/Hubs/ResultsHub.cs");
const html = read("backend/StackMeet.Api/wwwroot/results/index.html");
const client = read("backend/StackMeet.Api/wwwroot/results/results.js");

assert.match(program, /AddSignalR\(\)/, "SignalR must be registered.");
assert.match(program, /MapHub<ResultsHub>\("\/hubs\/results"\)/, "The results hub must be mapped.");
assert.match(program, /\{competitionId\}\/Results/, "The permanent competition results route must be mapped.");
assert.match(program, /!path\.StartsWithSegments\("\/api\/public"\)/, "Public read-only endpoints must bypass staff authentication.");

assert.match(stateController, /ResultsUpdated/, "Successful state saves must broadcast a live update.");
assert.match(stateController, /ResultsHub\.GroupName\(normalizedKey\)/, "Updates must be scoped to one competition.");
assert.match(hub, /AddToGroupAsync/, "Viewers must join a competition-specific group.");

assert.match(publicController, /AsNoTracking\(\)/, "Public queries must be read-only.");
assert.match(publicController, /ArchivedAt is not null/, "Archived competitions must not be public.");
assert.doesNotMatch(publicController, /email\s*=/i, "Public responses must not expose email addresses.");
assert.doesNotMatch(publicController, /phone\s*=/i, "Public responses must not expose phone numbers.");
assert.doesNotMatch(publicController, /paid\s*=/i, "Public responses must not expose payment state.");
assert.doesNotMatch(publicController, /checkedIn\s*=/i, "Public responses must not expose check-in state.");
assert.doesNotMatch(publicController, /birthDate\s*=/i, "Public responses must not expose birth dates.");
assert.match(publicController, /Text\(item, "division"\)/, "Computed competition divisions must be included in the safe public stacker projection.");
assert.match(publicController, /stateStackers\.Length > 0/, "Saved competition divisions must take priority over incomplete SQL custom divisions.");

assert.match(html, /Live Results/, "The provisional disclaimer must be visible.");
assert.match(client, /ResultsUpdated/, "The browser must refresh when SignalR publishes an update.");
assert.match(client, /cache:\s*"no-store"/, "Public results must not display a stale cached response.");
assert.match(html, /id="preliminaryGroups"/, "The Preliminary page must have a results container.");
assert.match(client, /renderPreliminary\(payload, official\)/, "The Preliminary route must render live standings.");
assert.match(client, /isPreliminaryStage/, "Preliminary results must be filtered from other stages.");
assert.match(client, /isIndividualType/, "Doubles and relay results must remain in their dedicated sections.");
assert.match(client, /Open \/ Unassigned/, "Results without a configured division must remain visible.");
assert.match(client, /localeCompare/, "Division and event groups must use stable natural sorting.");

console.log("Public results portal static safety tests passed.");
