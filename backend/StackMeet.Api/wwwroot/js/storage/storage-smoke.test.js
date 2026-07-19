"use strict";
const assert = require("assert");
const Repository = require("./Repository.js");

const saved = new Map();
global.fetch = async (url, options = {}) => {
  const key = String(url);
  if (options.method === "POST") {
    saved.set(key, JSON.parse(options.body));
    return { ok: true, status: 204, text: async () => "" };
  }
  if (!saved.has(key)) return { ok: false, status: 404, text: async () => "" };
  return { ok: true, status: 200, json: async () => saved.get(key), text: async () => "" };
};

const repository = new Repository();
const state = {
  settings: { name: "Smoke Test", prelims: "1" },
  stackers: [{ id: "1.1", name: "Test Stacker" }],
  doubles: [], relays: [], results: []
};

(async () => {
  assert.strictEqual(await repository.load(), null);
  await repository.save(state);
  assert.deepStrictEqual(await repository.load(), state);
  console.log("Storage smoke tests passed.");
})();
