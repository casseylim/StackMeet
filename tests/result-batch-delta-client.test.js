"use strict";
const assert = require("assert");

const ResultApiPath = "../backend/StackMeet.Api/wwwroot/js/storage/ResultApi.js";
global.window = {
  StackMeetAuth: { authHeaders: () => ({ Authorization: "Bearer local-test" }) },
  StackMeetStorage: {}
};

const calls = [];
const queuedResponses = [];
global.fetch = async (url, options = {}) => {
  calls.push({ url: String(url), method: options.method || "GET", body: options.body || null });
  assert(queuedResponses.length > 0, `Unexpected fetch: ${url}`);
  const body = queuedResponses.shift();
  return {
    ok: true,
    status: 200,
    json: async () => body,
    text: async () => JSON.stringify(body)
  };
};

const ResultApi = require(ResultApiPath);

const result = (participant, event, revision, attempts) => ({
  id: `${participant}-${event}`,
  stage: "Prelims",
  type: "Individual",
  participant,
  event,
  attempts,
  penalty: 0,
  revision
});

(async () => {
  const api = new ResultApi();
  queuedResponses.push({
    revision: 5,
    results: [
      result("1.1", "3-3-3", 5, [1.111, 1.222, 1.333]),
      result("1.2", "3-6-3", 5, [2.111, 2.222, 2.333])
    ]
  });

  const initial = await api.list(7);
  assert.equal(initial.revision, 5);
  assert.equal(initial.results.length, 2);

  // Mutating the returned snapshot must not corrupt the internal API cache.
  initial.results[0].attempts[0] = 999;

  queuedResponses.push({
    revision: 6,
    results: [result("1.2", "3-6-3", 6, [6.111, 6.222, 6.333])]
  });
  const afterUpsert = await api.saveBatch(7, {
    upserts: [{ stage: "Prelims", type: "Individual", participant: "1.2", event: "3-6-3", attempts: [6.111, 6.222, 6.333], penalty: 0, expectedRevision: 5 }],
    deletes: []
  });

  assert.equal(afterUpsert.revision, 6);
  assert.equal(afterUpsert.results.length, 2);
  assert.deepEqual(afterUpsert.results.find(item => item.participant === "1.1").attempts, [1.111, 1.222, 1.333], "Internal cache must be isolated from caller mutation.");
  assert.deepEqual(afterUpsert.results.find(item => item.participant === "1.2").attempts, [6.111, 6.222, 6.333]);
  assert.equal(calls.length, 2, "Cached batch merge must not issue a post-save full GET.");
  assert.equal(calls[1].method, "POST");

  queuedResponses.push({ revision: 7, results: [] });
  const afterDelete = await api.saveBatch(7, {
    upserts: [],
    deletes: [{ stage: " prelims ", type: "individual", participant: "1.1", event: "3-3-3", expectedRevision: 5 }]
  });
  assert.equal(afterDelete.revision, 7);
  assert.deepEqual(afterDelete.results.map(item => item.participant), ["1.2"], "Successful requested deletes must be removed from the cached full result set.");
  assert.equal(calls.length, 3, "Delete merge must also avoid a post-save full GET when cache is present.");

  // A fresh client without a cached full list may safely fall back to one authoritative GET.
  const uncachedApi = new ResultApi();
  queuedResponses.push(
    { revision: 8, results: [result("1.3", "Cycle", 8, [8.111, 8.222, 8.333])] },
    { revision: 8, results: [result("1.2", "3-6-3", 6, [6.111, 6.222, 6.333]), result("1.3", "Cycle", 8, [8.111, 8.222, 8.333])] }
  );
  const fallback = await uncachedApi.saveBatch(7, {
    upserts: [{ stage: "Prelims", type: "Individual", participant: "1.3", event: "Cycle", attempts: [8.111, 8.222, 8.333], penalty: 0, expectedRevision: null }],
    deletes: []
  });
  assert.equal(fallback.revision, 8);
  assert.equal(fallback.results.length, 2);
  assert.equal(calls.at(-2).method, "POST");
  assert.equal(calls.at(-1).method, "GET");

  console.log("Result batch delta client tests passed.");
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
