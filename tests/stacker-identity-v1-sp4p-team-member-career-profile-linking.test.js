'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

const browser = read('backend/StackMeet.Api/wwwroot/results/profile-links.js');
const results = read('backend/StackMeet.Api/wwwroot/results/results.js');

assert.match(browser, /const resultsEndpoint = `\/api\/public\/competitions\/\$\{encodeURIComponent\(competitionId\)\}\/results`;/,
  'SP-4P must derive team membership only from the competition-scoped public Results contract.');
assert.match(browser, /stackerNames = new Map\(stackers[\s\S]*String\(stacker\?\.id/,
  'SP-4P must key public display names by explicit competition participant ID.');
assert.match(browser, /\[team\.one, team\.two\]/,
  'Doubles member linking must use explicit team member participant IDs.');
assert.match(browser, /Array\.isArray\(team\.members\)/,
  'Relay member linking must honor explicit relay member participant IDs.');
assert.match(browser, /\[team\.one, team\.two, team\.three, team\.four, team\.five, team\.six\]/,
  'legacy relay membership must use explicit stored participant slots, not name matching.');
assert.match(browser, /profileLinks\.get\(participant\)/,
  'each member link must pass through the existing server-approved SP-4O profile-link map.');
assert.match(browser, /document\.querySelectorAll\("\.doubles-table tbody tr"\)/,
  'SP-4P must decorate Doubles member presentation.');
assert.match(browser, /document\.querySelectorAll\("\.relay-table tbody tr"\)/,
  'SP-4P must decorate Relay member presentation.');
assert.match(browser, /flattenDecoratedMemberCell\(memberCell, separator\)/,
  'missing membership evidence must revert previously decorated member cells to plain text.');
assert.match(browser, /stackerNames = new Map\(\);[\s\S]*doublesMembers = new Map\(\);[\s\S]*relayMembers = new Map\(\);/,
  'team-membership lookup failures must fail closed.');
assert.doesNotMatch(browser, /innerHTML\s*=/,
  'SP-4P browser rendering must remain DOM/textContent based.');
assert.doesNotMatch(browser, /stackers\.find\([^\n]*(?:name|wssa|birth|email|phone)/i,
  'SP-4P must not infer team-member identity from names or registration demographics.');

assert.match(results, /const memberCell = make\("td", "member-cell", row\.meta\.members\);/,
  'existing Doubles result rendering must remain authoritative and usable without SP-4P.');
assert.match(results, /const memberCell = make\("td", "member-cell relay-members", row\.meta\.members\);/,
  'existing Relay result rendering must remain authoritative and usable without SP-4P.');

console.log('SP-4P Doubles/Relay member career-profile linking static guards passed.');
