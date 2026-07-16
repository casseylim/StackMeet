/*
StackMeet CompetitionAdminPhase1 pre-migration checks.
Read-only only: this script contains SELECT statements only.
Run against the target database before applying outputs/migrations/CompetitionAdminPhase1.sql.
*/

SELECT 'Competition row count' AS CheckName, COUNT_BIG(*) AS CheckValue
FROM [dbo].[Competition];

SELECT 'CompetitionState row count' AS CheckName, COUNT_BIG(*) AS CheckValue
FROM [dbo].[CompetitionState];

SELECT 'Stacker row count' AS CheckName, COUNT_BIG(*) AS CheckValue
FROM [dbo].[Stacker];

SELECT 'Blank CompetitionCode values that would produce invalid CompetitionKey' AS CheckName, COUNT_BIG(*) AS FailureCount
FROM [dbo].[Competition]
WHERE [CompetitionCode] IS NULL OR LTRIM(RTRIM([CompetitionCode])) = '';

SELECT UPPER([CompetitionCode]) AS ProposedCompetitionKey, COUNT_BIG(*) AS DuplicateCount
FROM [dbo].[Competition]
GROUP BY UPPER([CompetitionCode])
HAVING COUNT_BIG(*) > 1;

SELECT 'CompetitionCode values exceeding new CompetitionKey length 100' AS CheckName, COUNT_BIG(*) AS FailureCount
FROM [dbo].[Competition]
WHERE LEN(UPPER([CompetitionCode])) > 100;

SELECT 'CompetitionState blank CompetitionKey values' AS CheckName, COUNT_BIG(*) AS FailureCount
FROM [dbo].[CompetitionState]
WHERE [CompetitionKey] IS NULL OR LTRIM(RTRIM([CompetitionKey])) = '';

SELECT [CompetitionKey], COUNT_BIG(*) AS DuplicateCount
FROM [dbo].[CompetitionState]
GROUP BY [CompetitionKey]
HAVING COUNT_BIG(*) > 1;

SELECT cs.[CompetitionKey], COUNT_BIG(*) AS OrphanedStateCount
FROM [dbo].[CompetitionState] cs
LEFT JOIN [dbo].[Competition] c
    ON UPPER(c.[CompetitionCode]) = UPPER(cs.[CompetitionKey])
WHERE c.[Id] IS NULL
GROUP BY cs.[CompetitionKey];

SELECT 'DEFAULT CompetitionState row exists' AS CheckName, COUNT_BIG(*) AS MatchCount
FROM [dbo].[CompetitionState]
WHERE [CompetitionKey] = 'DEFAULT';

SELECT 'DEFAULT Competition master row exists by CompetitionCode' AS CheckName, COUNT_BIG(*) AS MatchCount
FROM [dbo].[Competition]
WHERE UPPER([CompetitionCode]) = 'DEFAULT';

SELECT 'Existing competitions without password hash after migration will require admin password setup' AS CheckName, COUNT_BIG(*) AS AffectedCount
FROM [dbo].[Competition];