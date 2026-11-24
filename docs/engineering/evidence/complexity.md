# JavaScript function complexity

Acorn parsed 27 tracked first-party browser scripts and 1948 functions. Each function starts at one. Conditional expressions, logical operators, branches, loops, catches and non-default switch cases each add one. Nested functions have independent scores. The table lists the 40 highest scores.

| Source | Function | Decisions + 1 |
|---|---|---:|
| backend/StackMeet.Api/wwwroot/app.js:4774 | (callback) | 76 |
| backend/StackMeet.Api/wwwroot/js/CompetitionPackage.js:98 | validatePackage | 43 |
| backend/StackMeet.Api/wwwroot/results/results.js:160 | render | 39 |
| backend/StackMeet.Api/wwwroot/career-admin.js:12 | render | 37 |
| backend/StackMeet.Api/wwwroot/app.js:4680 | reportValue | 32 |
| backend/StackMeet.Api/wwwroot/career-admin.js:87 | mutate | 30 |
| backend/StackMeet.Api/wwwroot/js/CompetitionPackage.js:60 | createPackage | 28 |
| backend/StackMeet.Api/wwwroot/app.js:4122 | competitionParticipantMeta | 27 |
| backend/StackMeet.Api/wwwroot/app.js:1528 | generatedDivisionDisplayCategories | 26 |
| backend/StackMeet.Api/wwwroot/app.js:504 | (callback) | 25 |
| backend/StackMeet.Api/wwwroot/app.js:530 | (callback) | 23 |
| backend/StackMeet.Api/wwwroot/app.js:3278 | finalsReportDefinition | 23 |
| backend/StackMeet.Api/wwwroot/app.js:5415 | addStacker | 22 |
| backend/StackMeet.Api/wwwroot/app.js:5496 | loadStackerForEdit | 22 |
| backend/StackMeet.Api/wwwroot/js/reports/FinalsReportEngine.js:105 | participantMeta | 22 |
| backend/StackMeet.Api/wwwroot/app.js:5647 | saveStackerDoubleAssignment | 21 |
| backend/StackMeet.Api/wwwroot/app.js:5713 | addDouble | 21 |
| backend/StackMeet.Api/wwwroot/app.js:368 | normalizeState | 20 |
| backend/StackMeet.Api/wwwroot/app.js:783 | sqlStackerToRuntime | 20 |
| backend/StackMeet.Api/wwwroot/app.js:5127 | divisionSortInfo | 20 |
| backend/StackMeet.Api/wwwroot/profile/profile.js:68 | renderPersonalBests | 20 |
| backend/StackMeet.Api/wwwroot/profile/profile.js:241 | renderFinalsCareer | 19 |
| backend/StackMeet.Api/wwwroot/app.js:5869 | addRelay | 18 |
| backend/StackMeet.Api/wwwroot/js/reports/FinalsReportEngine.js:116 | appliesFilters | 18 |
| backend/StackMeet.Api/wwwroot/profile/profile.js:332 | renderFinalsPlacements | 18 |
| backend/StackMeet.Api/wwwroot/results/results.js:1160 | (callback) | 18 |
| backend/StackMeet.Api/wwwroot/results/results.js:1671 | participantMeta | 18 |
| backend/StackMeet.Api/wwwroot/app.js:1005 | applyRemoteCompetitionState | 17 |
| backend/StackMeet.Api/wwwroot/app.js:3315 | stageReportDefinition | 17 |
| backend/StackMeet.Api/wwwroot/app.js:5332 | mapStackTrackCsvRow | 17 |
| backend/StackMeet.Api/wwwroot/js/reports/FinalsRankingPolicy.js:170 | publicationScope | 17 |
| backend/StackMeet.Api/wwwroot/results/results.js:119 | refresh | 17 |
| backend/StackMeet.Api/wwwroot/results/results.js:791 | (callback) | 17 |
| backend/StackMeet.Api/wwwroot/app.js:5383 | parseCsv | 16 |
| backend/StackMeet.Api/wwwroot/results/results.js:1394 | (callback) | 16 |
| backend/StackMeet.Api/wwwroot/results/results.js:1571 | (callback) | 16 |
| backend/StackMeet.Api/wwwroot/app.js:412 | (callback) | 15 |
| backend/StackMeet.Api/wwwroot/app.js:4722 | (callback) | 15 |
| backend/StackMeet.Api/wwwroot/profile/profile.js:418 | renderTeamCareer | 15 |
| backend/StackMeet.Api/wwwroot/app.js:829 | refreshSqlStackers | 14 |
