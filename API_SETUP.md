# StackMeet Online Sprint 1 API setup

## Prerequisites

- .NET 8 SDK on the build/deployment machine.
- SQL Server 2022 connectivity and a valid `ConnectionStrings__StackMeet` value.
- IIS ASP.NET Core Hosting Bundle on the production IIS server when the API is hosted by IIS.

## Local start

From the project directory:

```powershell
Set-Location backend\StackMeet.Api
$env:ConnectionStrings__StackMeet = "Server=localhost;Database=StackMeet;Trusted_Connection=True;TrustServerCertificate=True"
dotnet restore
dotnet run
```

On its launch URL, open `/swagger` and confirm that the Swagger UI shows exactly these endpoints:

- `GET /api/health`
- `GET /api/state/{competitionKey}`
- `POST /api/state/{competitionKey}`

## Endpoint checks

```powershell
Invoke-RestMethod http://localhost:<port>/api/health

$state = '{"settings":{"name":"Smoke Test"},"stackers":[]}'
Invoke-WebRequest http://localhost:<port>/api/state/competition-4257 -Method Post -ContentType 'application/json' -Body $state

Invoke-RestMethod http://localhost:<port>/api/state/competition-4257
```

The health response is:

```json
{"status":"ok","version":"0.9-online"}
```

The POST creates or overwrites one row for the supplied competition key. The GET returns the stored JSON body directly, without an envelope or state transformation. A missing key returns HTTP 404.

## Hosting shape

Publish the frontend behind the same HTTPS origin as StackMeet API so its `ApiProvider` URL (`/api/state/DEFAULT`) remains same-origin. The frontend uses this API as its only persistence provider; it does not use browser localStorage or a fallback provider.

## Publish

```powershell
Set-Location backend\StackMeet.Api
dotnet publish -c Release -o .\publish
```

Deploy the published directory as an IIS ASP.NET Core application. Set `ConnectionStrings__StackMeet` in the IIS application environment or another secure hosting configuration; do not store credentials in source control.
