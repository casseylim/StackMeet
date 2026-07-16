# StackMeet.Api setup

## Folder structure

```text
StackMeet.sln
backend/
  StackMeet.Api/
    Controllers/
    Data/
    Models/
    Program.cs
    StackMeet.Api.csproj
database/
  001_CreateCompetitionState.sql
docs/api/
  API_SETUP.md
  DATABASE_SETUP.md
  DEPLOYMENT_GUIDE.md
  ONLINE_FOUNDATION_REPORT.md
```

## Build

```powershell
dotnet restore StackMeet.sln
dotnet build StackMeet.sln --configuration Release
```

## Run locally

Set the real connection string outside source control, then run the API:

```powershell
$env:ConnectionStrings__StackMeet = "Server=YOUR_SQL_SERVER;Database=YOUR_DATABASE;User Id=YOUR_DATABASE_USER;Password=<secret>;Encrypt=True;TrustServerCertificate=False;"
dotnet run --project backend/StackMeet.Api
```

Swagger is available at `http://localhost:<port>/swagger` or `https://localhost:<port>/swagger`, according to the ASP.NET Core launch URL.

## Endpoints

| Method | URL | Result |
| --- | --- | --- |
| GET | `/api/health` | `{ "status": "ok", "service": "StackMeet.Api", "version": "0.9-online" }` |
| GET | `/api/version` | `{ "version": "0.9-online", "framework": ".NET 8", "storage": "SQL Server" }` |
| GET | `/api/state/{competitionKey}` | Raw stored `JsonData`, or HTTP 404 when no state exists. |
| POST | `/api/state/{competitionKey}` | Creates or updates the raw request JSON and returns HTTP 204. |

Example state test:

```powershell
$body = '{"settings":{"name":"API smoke test"},"stackers":[]}'
Invoke-WebRequest http://localhost:<port>/api/state/competition-4257 -Method Post -ContentType 'application/json' -Body $body
Invoke-RestMethod http://localhost:<port>/api/state/competition-4257
```

The state controller reads and returns the JSON body as text. It does not deserialize, validate, normalize, or apply competition logic.

