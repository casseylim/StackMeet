"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const ApiProvider = require("../backend/StackMeet.Api/wwwroot/js/storage/ApiProvider.js");
const Repository = require("../backend/StackMeet.Api/wwwroot/js/storage/Repository.js");

const root = path.resolve(__dirname, "..");
const read = relative => fs.readFileSync(path.join(root, relative), "utf8");
const model = read("backend/StackMeet.Api/Models/CompetitionState.cs");
const controller = read("backend/StackMeet.Api/Controllers/CompetitionStateController.cs");
const providerSource = read("backend/StackMeet.Api/wwwroot/js/storage/ApiProvider.js");
const repositorySource = read("backend/StackMeet.Api/wwwroot/js/storage/Repository.js");

assert.match(model, /long StateRevision/, "CompetitionState must persist a bigint-style revision.");
assert.match(controller, /Request\.Headers\["If-Match"\]/, "State saves must require If-Match.");
assert.match(controller, /Status428PreconditionRequired/, "Missing state revisions must be rejected.");
assert.match(controller, /UPDLOCK, HOLDLOCK/, "State writes must lock the current row transactionally.");
assert.match(controller, /state\.StateRevision != expectedRevision/, "Existing state writes must compare the expected revision.");
assert.match(controller, /state\.StateRevision\+\+/, "Matching state writes must increment the revision exactly once.");
assert.match(controller, /StateRevision = 1/, "First state creation must start at revision 1.");
assert.match(controller, /StateConflict\(state\.StateRevision\)/, "Stale state writes must return a conflict.");
assert.match(controller, /SetEtag\(state\.StateRevision\)/, "State GET must expose the current ETag.");
assert.match(controller, /revision = committedRevision/, "SignalR state changes must use the committed state revision.");
assert.match(providerSource, /"If-Match": this\.stateEtag \|\| '\"0\"'/, "Browser state saves must send If-Match.");
assert.match(providerSource, /options\.acceptRevision === true/, "Ordinary loads must not silently accept a newer revision after initial load.");
assert.match(repositorySource, /reloadLatestCompetition\(\) \{ return this\.provider\.reloadLatest\(\); \}/, "Explicit remote refresh must accept the latest revision.");

const makeHeaders = etag => ({ get: name => String(name).toLowerCase() === "etag" ? etag : null });
const response = ({ status, etag = null, body = null }) => ({
  ok: status >= 200 && status < 300,
  status,
  headers: makeHeaders(etag),
  json: async () => body,
  text: async () => body == null ? "" : JSON.stringify(body)
});

(async () => {
  const calls = [];
  let getCount = 0;
  global.fetch = async (url, options = {}) => {
    calls.push({ url: String(url), options });
    if ((options.method || "GET") === "GET") {
      getCount += 1;
      if (getCount === 1) return response({ status: 200, etag: '"4"', body: { settings: { name: "A" } } });
      return response({ status: 200, etag: '"5"', body: { settings: { name: "B" } } });
    }
    const ifMatch = options.headers?.["If-Match"];
    if (ifMatch === '"4"') return response({ status: 409, etag: '"5"', body: { error: "Competition state changed on another computer. Refresh the latest data before saving.", currentRevision: 5 } });
    if (ifMatch === '"5"') return response({ status: 204, etag: '"6"' });
    throw new Error(`Unexpected If-Match ${ifMatch}`);
  };

  const provider = new ApiProvider("ABC");
  assert.deepEqual(await provider.load(), { settings: { name: "A" } });
  assert.equal(provider.stateEtag, '"4"');

  assert.deepEqual(await provider.load(), { settings: { name: "B" } });
  assert.equal(provider.stateEtag, '"4"', "ordinary load must not accept a remote revision behind the editor's back");

  await assert.rejects(
    provider.save({ settings: { name: "Local edit" } }),
    error => error.status === 409 && error.currentRevision === 5 && error.etag === '"5"'
  );
  assert.equal(provider.stateEtag, '"4"', "a rejected save must retain the user's accepted base revision");

  assert.deepEqual(await provider.reloadLatest(), { settings: { name: "B" } });
  assert.equal(provider.stateEtag, '"5"');
  await provider.save({ settings: { name: "After refresh" } });
  assert.equal(provider.stateEtag, '"6"');
  assert.equal(calls.at(-1).options.headers["If-Match"], '"5"');

  const priorLocation = global.location;
  const priorLocalStorage = global.localStorage;
  const localValues = new Map();
  let fileFetches = 0;
  global.location = { protocol: "file:" };
  global.localStorage = {
    getItem: key => localValues.has(key) ? localValues.get(key) : null,
    setItem: (key, value) => localValues.set(key, value)
  };
  global.fetch = async () => { fileFetches += 1; throw new Error("file mode must not fetch"); };
  try {
    const fileProvider = new ApiProvider("LOCAL");
    await fileProvider.save({ settings: { name: "Offline" } });
    assert.deepEqual(await fileProvider.load(), { settings: { name: "Offline" } });
    assert.equal(fileFetches, 0, "file/localStorage mode must remain network-free");
  } finally {
    if (priorLocation === undefined) delete global.location; else global.location = priorLocation;
    if (priorLocalStorage === undefined) delete global.localStorage; else global.localStorage = priorLocalStorage;
  }

  const repository = new Repository("ABC");
  assert.equal(typeof repository.reloadLatestCompetition, "function");
  console.log("CompetitionState optimistic concurrency tests passed.");
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
