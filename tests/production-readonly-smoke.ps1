[CmdletBinding()]
param(
    [string]$BaseUrl = "https://naditrack.com",
    [string]$CompetitionKey,
    [int]$CompetitionId,
    [string]$BearerToken
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')
$headers = @{ Accept = "application/json" }
if ($BearerToken) { $headers.Authorization = "Bearer $BearerToken" }

function Get-ReadOnlyJson([string]$Path) {
    $uri = "$base$Path"
    Write-Host "GET $uri"
    return Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -UseBasicParsing
}

Write-Host "Read-only production smoke test"
Write-Host "Target: $base"
Write-Host "No POST, PUT, PATCH, DELETE, import, export, or FTP operation is performed."

$health = Get-ReadOnlyJson "/api/health"
$version = Get-ReadOnlyJson "/api/version"
Write-Host "Health: OK"
Write-Host ("Version: {0}" -f (($version | ConvertTo-Json -Compress)))

if (-not $CompetitionKey -and -not $CompetitionId) {
    Write-Host "Basic production checks completed. Provide -CompetitionKey and -CompetitionId for competition checks."
    exit 0
}
if (-not $CompetitionKey -or $CompetitionId -le 0 -or -not $BearerToken) {
    throw "Competition checks require -CompetitionKey, a positive -CompetitionId, and an already-issued -BearerToken."
}

$state = Get-ReadOnlyJson ("/api/state/{0}" -f [uri]::EscapeDataString($CompetitionKey))
$stackers = @(Get-ReadOnlyJson ("/api/competitions/{0}/stackers" -f $CompetitionId))

$doubles = @($state.doubles)
$relays = @($state.relays)
$results = @($state.results)
$genderedTeams = @($doubles + $relays | Where-Object {
    $division = if ($_.division) { [string]$_.division } else { [string]$_.timedRelayDivision }
    -not $_.customDivision -and $division -match '\s+[MF]$'
})

Write-Host "Competition: $CompetitionKey (SQL id $CompetitionId)"
Write-Host "Individuals: $($stackers.Count)"
Write-Host "Doubles: $($doubles.Count)"
Write-Host "Relay: $($relays.Count)"
Write-Host "Results: $($results.Count)"
if ($genderedTeams.Count -gt 0) {
    Write-Error ("Gendered team divisions found: " + (($genderedTeams | ForEach-Object { $_.id }) -join ', '))
    exit 2
}
Write-Host "Team division check: PASS (no Male/Female suffix on Doubles or Relay)"
Write-Host "Production read-only smoke test passed."
