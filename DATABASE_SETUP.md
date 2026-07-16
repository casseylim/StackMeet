# StackMeet Online Sprint 1 database setup

## Scope

Online Foundation Phase 2 uses exactly one SQL Server table, `dbo.CompetitionState`. It stores the existing full competition-state JSON without normalization or business-rule processing.

The database schema is managed by Entity Framework Core migrations. Apply the initial migration before starting the API; the API does not create tables on startup.

## Required SQL Server access

Create or nominate a `StackMeet` database on SQL Server 2022. The account used to apply migrations needs permission to create and alter schema objects. The API identity needs permission to connect and to read/write `dbo.CompetitionState`.

For Windows authentication, grant the IIS application-pool identity access to the database. For SQL authentication, use an environment-variable connection string rather than committing a password.

## Connection string

Set the connection string before starting the API. This environment variable overrides the placeholder in `backend/StackMeet.Api/appsettings.json`:

```powershell
$env:ConnectionStrings__StackMeet = "Server=SERVERNAME;Database=StackMeet;User Id=stackmeet_api;Password=<secret>;Encrypt=True;TrustServerCertificate=False"
```

For a local trusted SQL Server connection:

```powershell
$env:ConnectionStrings__StackMeet = "Server=localhost;Database=StackMeet;Trusted_Connection=True;TrustServerCertificate=True"
```

## Apply migrations

From the repository root, restore the EF Core design-time dependency and apply the checked-in initial migration:

```powershell
dotnet restore StackMeet.sln
dotnet ef database update --project backend/StackMeet.Api --startup-project backend/StackMeet.Api
```

For development changes that require a new migration, run:

```powershell
dotnet ef migrations add <MigrationName> --project backend/StackMeet.Api --startup-project backend/StackMeet.Api --output-dir Migrations
```

## Expected table definition

```sql
CREATE TABLE dbo.CompetitionState
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CompetitionKey NVARCHAR(100) NOT NULL UNIQUE,
    JsonData NVARCHAR(MAX) NOT NULL,
    SchemaVersion NVARCHAR(50) NOT NULL DEFAULT N'0.9-online',
    UpdatedAt DATETIME2 NOT NULL,
    UpdatedBy NVARCHAR(100) NULL
);
```

`JsonData` is the unmodified request JSON. `UpdatedAt` is recorded in UTC by SQL Server. `UpdatedBy` is optional and is supplied only by the `X-StackMeet-Updated-By` request header.

## Verification

After applying migrations and starting the API, verify the table and a saved state:

```sql
SELECT Id, CompetitionKey, UpdatedAt, UpdatedBy, DATALENGTH(JsonData) AS JsonBytes
FROM dbo.CompetitionState;
```

Do not manually edit `JsonData` during the competition. Use the application’s XML export/import process for operational backup and recovery.
