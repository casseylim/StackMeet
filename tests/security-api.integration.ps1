param(
    [string]$BaseUrl = "http://127.0.0.1:48809",
    [Parameter(Mandatory = $true)][string]$ApiKey,
    [Parameter(Mandatory = $true)][string]$AdminKey,
    [string]$StateKey = "DEFAULT"
)

$ErrorActionPreference = "Stop"

function Invoke-Http(
    [string]$Method,
    [string]$Path,
    [hashtable]$Headers = @{},
    [string]$Body = $null
) {
    $parameters = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        UseBasicParsing = $true
        Headers = $Headers
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body
    }

    try {
        $response = Invoke-WebRequest @parameters
        return @{
            Status = [int]$response.StatusCode
            Content = $response.Content
            Headers = $response.Headers
        }
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return @{
            Status = [int]$response.StatusCode
            Content = $content
            Headers = $response.Headers
        }
    }
}

function Assert-Status($response, [int]$expected, [string]$name) {
    if ($response.Status -ne $expected) {
        throw "$name expected HTTP $expected but received $($response.Status). Body: $($response.Content)"
    }
    Write-Output "PASS $name ($expected)"
}

$apiHeaders = @{ "X-StackMeet-Api-Key" = $ApiKey }
$adminHeaders = @{ "X-StackMeet-Admin-Key" = $AdminKey }

$r = Invoke-Http GET "/api/health"
Assert-Status $r 200 "public health"

$r = Invoke-Http GET "/api/state/$StateKey"
Assert-Status $r 401 "state rejects missing credentials"

$r = Invoke-Http GET "/api/state/$StateKey" @{ "X-StackMeet-Api-Key" = "incorrect-test-key" }
Assert-Status $r 401 "state rejects invalid API key"

$before = Invoke-Http GET "/api/state/$StateKey" $apiHeaders
Assert-Status $before 200 "authorized state read before malformed save"

$invalidJson = Invoke-Http POST "/api/state/$StateKey" $apiHeaders "{invalid-json"
Assert-Status $invalidJson 400 "malformed state JSON rejection"

$after = Invoke-Http GET "/api/state/$StateKey" $apiHeaders
Assert-Status $after 200 "authorized state read after malformed save"
if ($before.Content -cne $after.Content) {
    throw "Malformed state save changed the canonical state."
}
Write-Output "PASS malformed state save leaves canonical state unchanged"

$unsupported = Invoke-Http POST "/api/admin/competitions/DOES-NOT-EXIST/status" $adminHeaders '{"status":"Banana"}'
Assert-Status $unsupported 400 "unsupported competition status rejection"

$supported = Invoke-Http POST "/api/admin/competitions/DOES-NOT-EXIST/status" $adminHeaders '{"status":"Draft"}'
Assert-Status $supported 404 "supported competition status reaches normal lookup"

$loginBody = '{"competitionId":"XX","password":"wrong-password","displayName":"Security Test"}'
$loginStatuses = 1..6 | ForEach-Object {
    (Invoke-Http POST "/api/auth/login" @{} $loginBody).Status
}
$expectedLoginStatuses = @(401, 401, 401, 401, 401, 429)
if ((Compare-Object $expectedLoginStatuses $loginStatuses -SyncWindow 0)) {
    throw "Login throttling expected 401,401,401,401,401,429 but received $($loginStatuses -join ','). Run against a freshly started API process."
}
Write-Output "PASS login throttling (401,401,401,401,401,429)"

Write-Output "Security API integration tests passed."
