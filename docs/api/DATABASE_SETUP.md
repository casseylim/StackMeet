# Database setup

## Connection string location

`backend/StackMeet.Api/appsettings.json` contains the required placeholder only. Replace `CHANGE_ME` through a secure environment variable or IIS configuration; do not commit the password.

```powershell
$env:ConnectionStrings__StackMeet = "Server=YOUR_SQL_SERVER;Database=YOUR_DATABASE;User Id=YOUR_DATABASE_USER;Password=<secret>;Encrypt=True;TrustServerCertificate=False;"
```

## Bootstrap

Apply the checked-in EF Core migration instead of running the legacy SQL bootstrap script:

```powershell
cd backend/StackMeet.Api
dotnet ef database update
```

The `InitialCreate` migration creates exactly one application table:

```text
dbo.CompetitionState
```

The script matches the EF Core `CompetitionState` model:

- `Id`: identity primary key
- `CompetitionKey`: required, unique `nvarchar(100)`
- `JsonData`: required `nvarchar(max)`
- `SchemaVersion`: required `nvarchar(50)`, default `0.9-online`
- `UpdatedAt`: required `datetime2`
- `UpdatedBy`: nullable `nvarchar(100)`

No normalized tables, foreign keys, triggers, or procedures are part of this phase. EF Core also creates its standard `__EFMigrationsHistory` table to track applied migrations.

## Connection check

After applying the migration and starting the API, save and retrieve a test state through Swagger or the commands in [API_SETUP.md](API_SETUP.md). A successful POST confirms both the connection and table write permission.
