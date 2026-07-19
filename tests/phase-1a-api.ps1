param(
    [string]$BaseUrl = "http://127.0.0.1:5186",
    [Parameter(Mandatory = $true)][string]$ApiKey
)

$ErrorActionPreference = "Stop"

function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $parameters = @{ Method = $Method; Uri = "$BaseUrl$Path"; UseBasicParsing = $true; Headers = @{ "X-StackMeet-Api-Key" = $ApiKey } }
    if ($null -ne $Body) { $parameters.ContentType = "application/json"; $parameters.Body = ($Body | ConvertTo-Json -Depth 5) }
    try {
        $response = Invoke-WebRequest @parameters
        return @{ Status = [int]$response.StatusCode; Body = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null } }
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $content = $reader.ReadToEnd()
        return @{ Status = [int]$response.StatusCode; Body = if ($content) { $content | ConvertFrom-Json } else { $null } }
    }
}

function Assert-Status($response, [int]$Expected, [string]$Name) {
    if ($response.Status -ne $Expected) { throw "$Name expected HTTP $Expected but received $($response.Status)." }
    Write-Output "PASS $Name ($Expected)"
}

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()
$competitionOne = @{ competitionCode = "P1A-$suffix-A"; competitionName = "Phase 1A A"; venue = "Test Venue"; startDate = "2026-08-01"; endDate = "2026-08-02"; status = "Draft" }
$competitionTwo = @{ competitionCode = "P1A-$suffix-B"; competitionName = "Phase 1A B"; venue = "Test Venue"; startDate = "2026-08-01"; endDate = "2026-08-02"; status = "Draft" }
$stackerOne = @{ stackerCode = "S-$suffix"; wssaId = "W-$suffix"; firstName = "Ada"; lastName = "Lovelace"; gender = "Female"; birthDate = "2010-01-01"; country = "MY"; club = "StackMeet"; region = "Negeri Sembilan"; email = "ada@example.test"; phone = "0123456789"; customDivision = "Open"; paid = "Yes"; checkedIn = "Yes"; isSpecialStacker = $false }
$stackerTwo = @{ stackerCode = "S2-$suffix"; wssaId = $null; firstName = "Grace"; lastName = "Hopper"; gender = "Female"; birthDate = $null; country = "MY"; club = $null; region = $null; email = $null; phone = $null; customDivision = $null; paid = "No"; checkedIn = "No"; isSpecialStacker = $true }

$r = Invoke-Api POST "/api/competitions" $competitionOne; Assert-Status $r 201 "competition create"; $competitionOneId = $r.Body.id
if (-not $r.Body.createdAt.EndsWith("Z") -or -not $r.Body.updatedAt.EndsWith("Z")) { throw "Competition timestamps are not UTC." }
$r = Invoke-Api GET "/api/competitions"; Assert-Status $r 200 "competition list"
$r = Invoke-Api GET "/api/competitions/$competitionOneId"; Assert-Status $r 200 "competition get"
$r = Invoke-Api GET "/api/competitions/999999999"; Assert-Status $r 404 "unknown competition get rejection"
$r = Invoke-Api POST "/api/competitions" @{ competitionCode = ""; competitionName = "Invalid"; venue = "Test"; startDate = "2026-08-02"; endDate = "2026-08-01"; status = "Draft" }; Assert-Status $r 400 "competition validation rejection"
$competitionOne.status = "Banana"; $r = Invoke-Api PUT "/api/competitions/$competitionOneId" $competitionOne; Assert-Status $r 400 "unsupported competition status rejection"
$competitionOne.status = "Active"; $r = Invoke-Api PUT "/api/competitions/$competitionOneId" $competitionOne; Assert-Status $r 204 "competition update"
$r = Invoke-Api POST "/api/competitions" $competitionOne; Assert-Status $r 409 "competition duplicate-code rejection"
$r = Invoke-Api POST "/api/competitions" $competitionTwo; Assert-Status $r 201 "second competition create"; $competitionTwoId = $r.Body.id

$r = Invoke-Api POST "/api/competitions/$competitionOneId/stackers" $stackerOne; Assert-Status $r 201 "stacker create"; $stackerOneId = $r.Body.id
if (-not $r.Body.createdAt.EndsWith("Z") -or -not $r.Body.updatedAt.EndsWith("Z")) { throw "Stacker timestamps are not UTC." }
if ($r.Body.region -ne $stackerOne.region -or $r.Body.email -ne $stackerOne.email -or $r.Body.paid -ne $stackerOne.paid -or $r.Body.checkedIn -ne $stackerOne.checkedIn) { throw "Stacker registration fields were not persisted." }
$r = Invoke-Api GET "/api/competitions/$competitionOneId/stackers"; Assert-Status $r 200 "stacker list"
$r = Invoke-Api GET "/api/competitions/$competitionOneId/stackers/$stackerOneId"; Assert-Status $r 200 "stacker get"
$r = Invoke-Api GET "/api/competitions/$competitionOneId/stackers/999999999"; Assert-Status $r 404 "unknown stacker rejection"
$stackerOne.club = "Updated Club"; $r = Invoke-Api PUT "/api/competitions/$competitionOneId/stackers/$stackerOneId" $stackerOne; Assert-Status $r 204 "stacker update"
$r = Invoke-Api POST "/api/competitions/$competitionOneId/stackers" $stackerOne; Assert-Status $r 409 "stacker duplicate-code rejection"
$r = Invoke-Api POST "/api/competitions/$competitionOneId/stackers" $stackerTwo; Assert-Status $r 201 "second stacker create"; $stackerTwoId = $r.Body.id
$r = Invoke-Api PUT "/api/competitions/$competitionOneId/stackers/$stackerTwoId" $stackerOne; Assert-Status $r 409 "stacker update duplicate-code rejection"
$r = Invoke-Api POST "/api/competitions/$competitionTwoId/stackers" $stackerOne; Assert-Status $r 201 "same stacker code in another competition"; $otherStackerId = $r.Body.id
$r = Invoke-Api POST "/api/competitions/999999999/stackers" $stackerOne; Assert-Status $r 404 "unknown competition rejection"
$r = Invoke-Api DELETE "/api/competitions/$competitionOneId"; Assert-Status $r 409 "competition delete with stackers rejected"

$r = Invoke-Api DELETE "/api/competitions/$competitionOneId/stackers/$stackerOneId"; Assert-Status $r 204 "stacker delete"
$r = Invoke-Api DELETE "/api/competitions/$competitionOneId/stackers/$stackerTwoId"; Assert-Status $r 204 "second stacker delete"
$r = Invoke-Api DELETE "/api/competitions/$competitionTwoId/stackers/$otherStackerId"; Assert-Status $r 204 "cross-competition stacker delete"
$r = Invoke-Api DELETE "/api/competitions/$competitionOneId"; Assert-Status $r 204 "competition delete"
$r = Invoke-Api DELETE "/api/competitions/$competitionTwoId"; Assert-Status $r 204 "second competition delete"

Write-Output "Phase 1A API tests passed."
