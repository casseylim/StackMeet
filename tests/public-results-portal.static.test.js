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
const styles = read("backend/StackMeet.Api/wwwroot/results/results.css");

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
assert.match(html, /id="finalsGroups"/, "The Finals page must have a results container.");
assert.match(client, /renderFinals\(payload, official\)/, "The Finals route must render live standings.");
assert.match(client, /isFinalStage/, "Final results must be filtered from other stages.");
assert.match(client, /isIndividualType\(result\.type\)/, "Finals must exclude doubles and relay results.");
assert.match(client, /row\.best === previousBest \? previousRank/, "Tied final times must share the same rank.");
assert.match(client, /medalPlace\(rank\)/, "Final places must show medal indicators.");
assert.match(client, /rank <= 3/, "Only podium places may receive medal styling.");
assert.match(styles, /medal-1/, "Gold-medal rows must have dedicated styling.");
assert.match(client, /const resultsRoot = competitionId/, "Navigation must be rooted at the current competition.");
assert.match(client, /link\.href = `\$\{resultsRoot\}\$\{suffix\}`/, "Section links must use competition-scoped absolute paths.");
assert.match(client, /backLink\.href = resultsRoot/, "The return link must use the competition results root.");
assert.doesNotMatch(html, /href="\.\/Results/, "Relative Results links must not append duplicate URL segments.");
assert.match(html, /results\.js\?v=20260722-6/, "Results JavaScript changes must invalidate the browser cache.");
assert.match(html, /id="allAroundGroups"/, "The All-Around page must have a standings container.");
assert.match(client, /renderAllAround\(payload, official\)/, "The All-Around route must render live standings.");
assert.match(client, /ALL_AROUND_EVENTS/, "All-Around must require the three configured individual events.");
assert.match(client, /allAroundEventKey/, "Event labels must be normalized before calculating totals.");
assert.match(client, /isIndividualType\(result\.type\)/, "Doubles and relay results must not enter All-Around standings.");
assert.match(client, /every\(event => Number\.isFinite/, "A stacker must complete all three events before receiving an All-Around total.");
assert.match(client, /group\.stage\.key === "finals"/, "Complete Final totals must take priority over Preliminary totals within a division.");
assert.match(client, /row\.total === previousTotal \? previousRank/, "Tied All-Around totals must share the same rank.");
assert.match(styles, /grid-template-areas:[\s\S]*"place stacker total"/, "Mobile All-Around rows must use a compact card layout.");
assert.match(html, /id="doublesGroups"/, "The Doubles page must have a standings container.");
assert.match(client, /renderDoubles\(payload, official\)/, "The Doubles route must render live standings.");
assert.match(client, /isDoublesType/, "Only Doubles result types may enter Doubles standings.");
assert.match(client, /team\.customDivision \|\| team\.division/, "Configured and child\/parent Doubles divisions must remain separate.");
assert.match(client, /row\.best === previousBest \? previousRank/, "Tied Doubles times must share the same rank.");
assert.match(client, /isFinal \? medalPlace\(rank\)/, "Final Doubles places must show podium indicators.");
assert.match(client, /team\.one, team\.two/, "Doubles standings must resolve both team members.");
assert.match(styles, /\.doubles-table tbody \{ display: grid; grid-template-columns: 1fr;/, "Mobile Doubles standings must use one column.");
assert.match(html, /results\.css\?v=20260722-6/, "Doubles styles must invalidate the browser cache.");

console.log("Public results portal static safety tests passed.");
