param(
  [Parameter(Mandatory=$true)][string]$BaseUrl,
  [Parameter(Mandatory=$true)][string]$AdminKey,
  [Parameter(Mandatory=$true)][string]$CompetitionKey,
  [Parameter(Mandatory=$true)][string]$CompetitionPassword,
  [string]$OtherCompetitionKey = "SHOULD_NOT_EXIST"
)

$ErrorActionPreference = "Stop"
$adminHeaders = @{ "X-StackMeet-Admin-Key" = $AdminKey }

function StatusOf($ScriptBlock) {
  try { return & $ScriptBlock } catch { return [int]$_.Exception.Response.StatusCode }
}

$health = (Invoke-WebRequest "$BaseUrl/api/health" -UseBasicParsing).StatusCode
$version = (Invoke-WebRequest "$BaseUrl/api/version" -UseBasicParsing).StatusCode
$adminList = (Invoke-WebRequest "$BaseUrl/api/admin/competitions" -Headers $adminHeaders -UseBasicParsing).StatusCode
$loginBody = @{ competitionId = $CompetitionKey; password = $CompetitionPassword; displayName = "Smoke" } | ConvertTo-Json
$login = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
$userHeaders = @{ Authorization = "Bearer $($login.token)" }
$ownState = (Invoke-WebRequest "$BaseUrl/api/state/$CompetitionKey" -Headers $userHeaders -UseBasicParsing).StatusCode
$otherState = StatusOf { (Invoke-WebRequest "$BaseUrl/api/state/$OtherCompetitionKey" -Headers $userHeaders -UseBasicParsing -SkipHttpErrorCheck).StatusCode }
$adminWithUserToken = StatusOf { (Invoke-WebRequest "$BaseUrl/api/admin/competitions" -Headers $userHeaders -UseBasicParsing -SkipHttpErrorCheck).StatusCode }

[pscustomobject]@{
  Health = $health
  Version = $version
  AdminList = $adminList
  LoginCompetition = $login.competitionId
  OwnState = $ownState
  OtherStateExpected403 = $otherState
  AdminWithUserTokenExpected401 = $adminWithUserToken
}