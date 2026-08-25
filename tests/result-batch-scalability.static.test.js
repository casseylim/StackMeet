"use strict";
const assert = require("assert");
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const controller = fs.readFileSync(
  path.join(root, "backend/StackMeet.Api/Controllers/CompetitionResultsController.cs"),
  "utf8"
);

const batchStart = controller.indexOf('[HttpPost("batch")]');
const batchEnd = controller.indexOf("async Task<ActionResult?> Access", batchStart);
assert(batchStart >= 0 && batchEnd > batchStart, "Batch method must remain discoverable for scalability checks.");
const batch = controller.slice(batchStart, batchEnd);

assert(!/var existing\s*=\s*await database\.CompetitionResults\s*\.Where\(x => x\.CompetitionId == competitionId\)\s*\.ToListAsync\(ct\)/s.test(batch),
  "Batch persistence must not load every competition result before applying a bounded change set.");
assert(/const int CandidateParticipantChunkSize = 300;/.test(controller),
  "Candidate participant lookup must use the reviewed bounded chunk size of 300.");
assert(/participantCodes[\s\S]*\.Select\(x => x\.Participant\.Trim\(\)\)[\s\S]*\.Distinct\(StringComparer\.OrdinalIgnoreCase\)/.test(batch),
  "Batch must derive a distinct participant-code candidate set from requested changes.");
assert(/participantCodes\.Chunk\(CandidateParticipantChunkSize\)/.test(batch),
  "Candidate participant codes must be queried in bounded chunks.");
assert(/\.Where\(x => x\.CompetitionId == competitionId && participantChunk\.Contains\(x\.ParticipantCode\)\)/.test(batch),
  "Candidate result queries must remain scoped to the competition and requested participant codes.");
assert(/new Dictionary<string, CompetitionResult>\(StringComparer\.Ordinal\)/.test(batch),
  "Loaded candidates must be indexed by canonical logical-result identity.");
assert(/existingByKey\.TryGetValue\(Key\(item\), out var row\)/.test(batch),
  "Upserts must resolve existing rows from the logical-key dictionary.");
assert(/existingByKey\.Add\(Key\(item\), row\)/.test(batch),
  "New rows must be added to the logical-key dictionary immediately.");
assert(/if \(!existingByKey\.TryGetValue\(Key\(item\), out var row\)\) continue;/.test(batch),
  "Deletes must resolve candidates from the same logical-key dictionary.");
assert(/if \(row is not null && item\.ExpectedRevision is not null && row\.Revision != item\.ExpectedRevision\) return Conflict/.test(batch),
  "Stale upsert ExpectedRevision conflicts must remain enforced.");
assert(/if \(item\.ExpectedRevision is not null && row\.Revision != item\.ExpectedRevision\) return Conflict/.test(batch),
  "Stale delete ExpectedRevision conflicts must remain enforced.");
assert(/if \(touched\.Count == 0 && deletedCount == 0\)[\s\S]*return await List\(competitionId, ct\);/.test(batch),
  "No-op batches must return without incrementing the competition revision.");
assert(/competition\.ResultsRevision\+\+;/.test(batch),
  "A real batch must increment ResultsRevision exactly once in the mutation path.");
assert(/foreach \(var row in touched\)[\s\S]*row\.Revision = competition\.ResultsRevision;/.test(batch),
  "Only touched upsert rows must receive the new competition results revision.");
assert(/SendAsync\("ResultsChanged"/.test(batch),
  "Committed result changes must keep the ResultsChanged SignalR event.");
assert(/return await List\(competitionId, ct\);/.test(batch),
  "This optimization must preserve the established full batch response contract.");
assert(/a\.Trim\(\)\.ToUpperInvariant\(\)[\s\S]*b\.Trim\(\)\.ToUpperInvariant\(\)[\s\S]*c\.Trim\(\)\.ToUpperInvariant\(\)[\s\S]*d\.Trim\(\)\.ToUpperInvariant\(\)/.test(controller),
  "Canonical logical result keys must preserve Trim plus ToUpperInvariant normalization.");

console.log("Result batch scalability safety tests passed.");
