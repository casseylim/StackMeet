IF OBJECT_ID(N'dbo.CompetitionState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompetitionState
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CompetitionState PRIMARY KEY,
        CompetitionKey NVARCHAR(100) NOT NULL,
        JsonData NVARCHAR(MAX) NOT NULL,
        SchemaVersion NVARCHAR(50) NOT NULL CONSTRAINT DF_CompetitionState_SchemaVersion DEFAULT N'0.9-online',
        UpdatedAt DATETIME2 NOT NULL,
        UpdatedBy NVARCHAR(100) NULL,
        CONSTRAINT UQ_CompetitionState_CompetitionKey UNIQUE (CompetitionKey)
    );
END;
