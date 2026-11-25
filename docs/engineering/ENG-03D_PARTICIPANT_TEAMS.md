# ENG-03D — Participant and team rule boundary (draft)

Base: 1dd0bb2602fb006a01ca74bad2fea2149100b4bf (ENG-03C validated). Extract nine existing operator-side functions into js/ParticipantTeams.js with explicit inputs and compatibility wrappers in app.js. No change to existing SQL-backed participant registration, team mutation/delete/tombstone behavior, scoring or historical records.

| Extracted responsibility | Functions |
|---|---|
| Participant identity and sort | stackerIdNumber, compareStackers, nextTeamCode |
| Team member representation | registeredDoubleMemberIds, relayMemberIds |
| Relay eligibility/status | relayCanCompete, relayTeamStatus |
| Existing form validation | validateDoubleEntry, validateRelayEntry |

Relay validation receives the current relay list and editing ID explicitly. ID generation receives the runtime state explicitly. Other implementations retain their original logic. Legacy aliases and filtering, numeric suffix IDs, ready threshold (four members), maximum six-member form check, existing Child/Parent external-name handling, duplicate-name comparison and all English message keys are preserved. This is a mechanical extraction, not stricter validation or an activity-rule migration. The server remains authoritative; do not interpret UI readiness as a new server eligibility policy.

The three full-app VM harnesses load the real module, preserving prior characterization and result-editor scenarios. The reviewed shell manifest includes the new script; the new direct contract test covers ID ordering, legacy member shapes, relay counts and messages, self-pairing, duplicates, edit exclusion, input immutability, browser export and wrapper wiring. Hosted CI pending.

Deliberately defer DOM registration/editor flows, age and division rules, team reassignment and persistence to later controlled extraction/reconciliation; avoid rule changes before ENG-09 owner decisions. Real-browser EN/MS/zh-Hans, keyboard and actual registration/score workflows remain pending. The new script, corresponding app.js and index.html must be in the approved coordinated release inventory. Never overwrite, regenerate, upload or delete production web.config; exclude it from upload/sync/rollback manifests, preserve secure backup and verify identical pre/post SHA-256. Stop if preservation cannot be guaranteed. No master merge, schema or production deployment.
