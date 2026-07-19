# StackMeet.Api deployment guide

## Publish

```powershell
dotnet publish backend/StackMeet.Api --configuration Release --output .\publish\StackMeet.Api
```

Deploy the published output as an ASP.NET Core application in IIS. Install the .NET 8 ASP.NET Core Hosting Bundle on the IIS server and give the application identity network/database access required by SQL Server.

## Configuration

Set `ConnectionStrings__StackMeet` in the IIS application environment or the approved secret configuration mechanism. The source `appsettings.json` deliberately contains `Password=CHANGE_ME` and must never be used with that placeholder in production.

## Verification after deployment

1. Open `https://<api-host>/swagger` and confirm Swagger lists the three controllers and four endpoints.
2. Open `https://<api-host>/api/health` and confirm the health JSON response.
3. Open `https://<api-host>/api/version` and confirm the version JSON response.
4. POST a disposable test JSON body to `/api/state/<test-key>`.
5. GET the same key and confirm the response body equals the JSON saved.
6. Confirm the SQL table has one row for the test key, then remove only that disposable test row if operational policy permits.

## Frontend boundary

This phase does not deploy or alter the static frontend. It continues to use `LocalStorageProvider`; the inactive `ApiProvider` is not loaded and no IIS routing to `/api` is enabled by this foundation alone.

