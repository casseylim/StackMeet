BEGIN TRANSACTION;
GO

ALTER TABLE [dbo].[CompetitionState] ADD [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
GO

UPDATE [dbo].[CompetitionState] SET [CreatedAt] = [UpdatedAt] WHERE [CreatedAt] IS NULL OR [CreatedAt] = '0001-01-01T00:00:00.0000000'
GO

ALTER TABLE [dbo].[Competition] ADD [ArchivedAt] datetime2 NULL;
GO

ALTER TABLE [dbo].[Competition] ADD [ArchivedBy] nvarchar(100) NULL;
GO

ALTER TABLE [dbo].[Competition] ADD [CompetitionKey] nvarchar(100) NULL;
GO

UPDATE [dbo].[Competition] SET [CompetitionKey] = UPPER([CompetitionCode]) WHERE [CompetitionKey] IS NULL OR LTRIM(RTRIM([CompetitionKey])) = ''
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Competition]') AND [c].[name] = N'CompetitionKey');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[Competition] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [dbo].[Competition] ALTER COLUMN [CompetitionKey] nvarchar(100) NOT NULL;
GO

ALTER TABLE [dbo].[Competition] ADD [PasswordHash] nvarchar(500) NULL;
GO

CREATE UNIQUE INDEX [IX_Competition_CompetitionKey] ON [dbo].[Competition] ([CompetitionKey]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713034108_CompetitionAdminPhase1', N'8.0.8');
GO

COMMIT;
GO

