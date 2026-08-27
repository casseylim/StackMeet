param(
    [string]$BaseUrl = "http://127.0.0.1:48819"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = "backend/StackMeet.Api/StackMeet.Api.csproj"
$dbName = "NADITrack_CoreIntegrity_$([Guid]::NewGuid().ToString('N'))"
$connection = "Server=(localdb)\MSSQLLocalDB;Database=$dbName;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$apiKey = "phase-c-api-key"
$adminKey = "phase-c-admin-key"
$signingKey = "phase-c-session-signing-key-for-ci-only-2026"
$stdout = Join-Path $env:TEMP "$dbName.out.log"
$stderr = Join-Path $env:TEMP "$dbName.err.log"
$process = $null

function Invoke-Http([string]$Method, [string]$Path, [hashtable]$Headers = @{}, $Body = $null) {
    $params = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        Headers = $Headers
        SkipHttpErrorCheck = $true
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
    }
    $response = Invoke-WebRequest @params
    [pscustomobject]@{ Status = [int]$response.StatusCode; Content = $response.Content; Headers = $response.Headers }
}

function Assert-Status($Response, [int]$Expected, [string]$Name) {
    if ($Response.Status -ne $Expected) {
        throw "$Name expected HTTP $Expected but received $($Response.Status). Body: $($Response.Content)"
    }
    Write-Host "PASS $Name ($Expected)"
}

function Wait-Api {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($null -ne $process -and $process.HasExited) {
            throw "API process exited before health check succeeded."
        }
        try {
            $health = Invoke-Http GET "/api/health"
            if ($health.Status -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "API health check did not become ready."
}

$oldConnection = $env:ConnectionStrings__StackMeet
$oldApiKey = $env:Security__ApiKey
$oldAdminKey = $env:Security__AdminKey
$oldSigningKey = $env:Security__SessionSigningKey
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT

try {
    Push-Location $root
    sqllocaldb start MSSQLLocalDB | Out-Null
    $env:ConnectionStrings__StackMeet = $connection
    $env:Security__ApiKey = $apiKey
    $env:Security__AdminKey = $adminKey
    $env:Security__SessionSigningKey = $signingKey
    $env:ASPNETCORE_ENVIRONMENT = "Development"

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }
    dotnet ef database update --project $project --startup-project $project --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "LocalDB migration failed." }

    $process = Start-Process dotnet -ArgumentList @(
        "run", "--project", $project, "-c", "Release", "--no-build", "--no-launch-profile", "--", "--urls", $BaseUrl
    ) -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Wait-Api

    $adminHeaders = @{ "X-StackMeet-Admin-Key" = $adminKey }
    $apiHeaders = @{ "X-StackMeet-Api-Key" = $apiKey }
    $email = "phase-c-$([Guid]::NewGuid().ToString('N'))@example.test"
    $password = "CoreIntegrity!2026Pass"

    $createdUser = Invoke-Http POST "/api/admin/users" $adminHeaders @{
        email = $email; displayName = "Phase C CI"; password = $password; isSystemAdmin = $true; emailConfirmed = $true
    }
    Assert-Status $createdUser 201 "create isolated system admin"

    $login = Invoke-Http POST "/api/auth/login" @{} @{ email = $email; password = $password }
    Assert-Status $login 200 "account login"
    $token = ($login.Content | ConvertFrom-Json).token
    if ([string]::IsNullOrWhiteSpace($token)) { throw "Account login did not return a bearer token." }
    $authHeaders = @{ Authorization = "Bearer $token" }

    $competitionCode = "CIC$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    $competition = Invoke-Http POST "/api/competitions" $apiHeaders @{
        competitionCode = $competitionCode; competitionName = "Core Integrity Phase C"; venue = "CI LocalDB";
        startDate = "2026-08-27"; endDate = "2026-08-28"; status = "Active"; isPubliclyListed = $false
    }
    Assert-Status $competition 201 "create isolated competition"
    $competitionId = [int](($competition.Content | ConvertFrom-Json).id)

    foreach ($code in @("1.1", "1.2", "1.3", "1.4")) {
        $stacker = Invoke-Http POST "/api/competitions/$competitionId/stackers" $apiHeaders @{
            stackerCode = $code; wssaId = $null; firstName = "CI"; lastName = $code; gender = "M";
            birthDate = "2010-01-01"; country = "Malaysia"; club = $null; region = $null; email = $null;
            phone = $null; customDivision = $null; paid = "No"; checkedIn = "No"; isSpecialStacker = $false
        }
        Assert-Status $stacker 201 "create participant $code"
    }

    $stateHeaders = @{ "X-StackMeet-Api-Key" = $apiKey; "If-Match" = '"0"' }
    $state = Invoke-Http POST "/api/state/$competitionCode" $stateHeaders @{
        stackers = @();
        doubles = @(@{ id = "2.1"; one = "1.1"; two = "1.2" });
        relays = @(@{ id = "3.1"; name = "CI Relay"; members = @("1.1", "1.2", "1.3", "1.4") });
        results = @()
    }
    Assert-Status $state 204 "seed team identities in competition state"

    $validIndividual = Invoke-Http POST "/api/competitions/$competitionId/results/batch" $authHeaders @{
        upserts = @(@{ stage = "prelims"; type = "individual"; participant = "1.1"; event = "cycle"; attempts = @(8.123); penalty = 0; expectedRevision = $null });
        deletes = @()
    }
    Assert-Status $validIndividual 200 "canonical individual result write"
    $individualPayload = $validIndividual.Content | ConvertFrom-Json
    if ($individualPayload.revision -ne 1 -or $individualPayload.results[0].stage -ne "Prelims" -or $individualPayload.results[0].type -ne "Individual" -or $individualPayload.results[0].event -ne "Cycle") {
        throw "Canonical result normalization was not persisted as expected."
    }
    Write-Host "PASS canonical result normalization"

    $validTeams = Invoke-Http POST "/api/competitions/$competitionId/results/batch" $authHeaders @{
        upserts = @(
            @{ stage = "Finals"; type = "Doubles"; participant = "2.1"; event = "Cycle"; attempts = @(7.901, 7.842, 7.990); penalty = 0; expectedRevision = $null },
            @{ stage = "Prelims"; type = "Relay"; participant = "3.1"; event = "3-6-3"; attempts = @(12.345); penalty = 0; expectedRevision = $null }
        );
        deletes = @()
    }
    Assert-Status $validTeams 200 "doubles and legacy relay alias write"
    $teamPayload = $validTeams.Content | ConvertFrom-Json
    if (-not ($teamPayload.results | Where-Object { $_.participant -eq "3.1" -and $_.type -eq "Timed Relay" })) {
        throw "Relay compatibility alias was not normalized to Timed Relay."
    }
    Write-Host "PASS team participant validation and relay canonicalization"

    $invalidCases = @(
        @{ Name = "reject report-only stage"; Body = @{ upserts = @(@{ stage = "All"; type = "Individual"; participant = "1.1"; event = "Cycle"; attempts = @(8.1); penalty = 0 }); deletes = @() } },
        @{ Name = "reject unknown individual"; Body = @{ upserts = @(@{ stage = "Prelims"; type = "Individual"; participant = "1.999"; event = "Cycle"; attempts = @(8.1); penalty = 0 }); deletes = @() } },
        @{ Name = "reject unknown doubles team"; Body = @{ upserts = @(@{ stage = "Prelims"; type = "Doubles"; participant = "2.999"; event = "Cycle"; attempts = @(8.1); penalty = 0 }); deletes = @() } },
        @{ Name = "reject four attempts"; Body = @{ upserts = @(@{ stage = "Prelims"; type = "Individual"; participant = "1.1"; event = "3-3-3"; attempts = @(1.1, 1.2, 1.3, 1.4); penalty = 0 }); deletes = @() } },
        @{ Name = "reject attempt precision beyond milliseconds"; Body = @{ upserts = @(@{ stage = "Prelims"; type = "Individual"; participant = "1.1"; event = "3-3-3"; attempts = @(1.2345); penalty = 0 }); deletes = @() } },
        @{ Name = "reject penalty precision beyond milliseconds"; Body = @{ upserts = @(@{ stage = "Prelims"; type = "Individual"; participant = "1.1"; event = "3-3-3"; attempts = @(1.234); penalty = 0.0001 }); deletes = @() } },
        @{ Name = "reject duplicate logical keys after normalization"; Body = @{ upserts = @(
            @{ stage = "prelims"; type = "individual"; participant = "1.1"; event = "cycle"; attempts = @(8.1); penalty = 0 },
            @{ stage = "PRELIMS"; type = "INDIVIDUAL"; participant = "1.1"; event = "CYCLE"; attempts = @(8.2); penalty = 0 }
        ); deletes = @() } }
    )
    foreach ($case in $invalidCases) {
        $response = Invoke-Http POST "/api/competitions/$competitionId/results/batch" $authHeaders $case.Body
        Assert-Status $response 400 $case.Name
    }

    $oversized = 1..1001 | ForEach-Object {
        @{ stage = "Prelims"; type = "Individual"; participant = "1.1"; event = "Cycle"; attempts = @(8.123); penalty = 0 }
    }
    $tooLarge = Invoke-Http POST "/api/competitions/$competitionId/results/batch" $authHeaders @{ upserts = $oversized; deletes = @() }
    Assert-Status $tooLarge 400 "reject oversized batch"

    $snapshot = Invoke-Http GET "/api/competitions/$competitionId/results" $authHeaders
    Assert-Status $snapshot 200 "authoritative result snapshot"
    $snapshotPayload = $snapshot.Content | ConvertFrom-Json
    if ($snapshotPayload.revision -ne 2 -or $snapshotPayload.results.Count -ne 3) {
        throw "Rejected writes changed durable results. Expected revision 2 with 3 rows; got revision $($snapshotPayload.revision) with $($snapshotPayload.results.Count) rows."
    }
    Write-Host "PASS rejected writes leave durable results unchanged"
    Write-Host "Core integrity V2 Phase C LocalDB integration tests passed."
}
catch {
    if (Test-Path $stdout) { Write-Host "--- API stdout ---"; Get-Content $stdout -ErrorAction SilentlyContinue }
    if (Test-Path $stderr) { Write-Host "--- API stderr ---"; Get-Content $stderr -ErrorAction SilentlyContinue }
    throw
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
    try {
        if (Test-Path (Join-Path $root $project)) {
            dotnet ef database drop --project (Join-Path $root $project) --startup-project (Join-Path $root $project) --force --no-build --configuration Release | Out-Null
        }
    } catch { Write-Warning "Could not drop temporary LocalDB database $dbName." }
    $env:ConnectionStrings__StackMeet = $oldConnection
    $env:Security__ApiKey = $oldApiKey
    $env:Security__AdminKey = $oldAdminKey
    $env:Security__SessionSigningKey = $oldSigningKey
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    Pop-Location -ErrorAction SilentlyContinue
    Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
}
