[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Preflight','Deploy','VerifyRollback')][string]$Operation,
 [Parameter(Mandatory)][string]$PackageDirectory,
 [string]$SourceDirectory,
 [string]$PreflightRunId=$env:PREFLIGHT_RUN_ID,
 [string]$ExpectedManifestSha256=$env:EXPECTED_MANIFEST,
 [string]$SiteConfirmation=$env:SITE_CONFIRMATION,
 [string]$DeploymentConfirmation=$env:DEPLOYMENT_CONFIRMATION
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'NadiTrackApplicationRelease.psm1') -Force
$policy=Read-ReleaseJson (Join-Path $PSScriptRoot 'career-profile-admin-ui.approved.json')
Assert-ReleasePolicy $policy
function GitHub([string]$Path){
 Invoke-RestMethod "https://api.github.com/repos/casseylim/StackMeet/$Path" -Headers @{Authorization="Bearer $env:GH_TOKEN";Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'} -TimeoutSec 30
}
function Check-RemoteSource {
 $branch=GitHub 'branches/master'
 $source=GitHub "commits/$($policy.sourceSha)"
 $comparison=GitHub "compare/$($policy.sourceSha)...$($branch.commit.sha)"
 Assert-SourceGate $policy ([pscustomobject]@{repository=$env:GITHUB_REPOSITORY;ref=$env:GITHUB_REF;protected=$branch.protected;remoteSource=$source.sha;relationship=$comparison.status;masterSha=$branch.commit.sha;workflowSha=$env:GITHUB_SHA})
}
# Every operation needs remote provenance. No local-only commit or test bypass.
Check-RemoteSource
if($Operation -eq 'Deploy'){Assert-DeployInterlocks $PreflightRunId $ExpectedManifestSha256 $SiteConfirmation $DeploymentConfirmation}
foreach($name in 'FTP_HOST','FTP_USER','FTP_PASS','FTP_PORT','FTP_SSL','RUNNER_TEMP','GITHUB_RUN_ID'){
 if([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))){throw "Missing $name"}
}
if($env:FTP_HOST -cnotmatch '^[A-Za-z0-9.-]+$'){throw 'FTP host must be a hostname, not a URL or path'}
$port=0
if(-not [int]::TryParse($env:FTP_PORT,[ref]$port) -or $port -lt 1 -or $port -gt 65535){throw 'Invalid FTP port'}
$ssl=switch($env:FTP_SSL.ToLowerInvariant()){'true'{$true}'false'{$false}default{throw 'Invalid FTP_SSL'}}
$scratch=Join-Path $env:RUNNER_TEMP ('naditrack-application-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
function Invoke-Ftp([string[]]$Arguments){
 $argsBase=@('--fail','--silent','--show-error','--ftp-pasv','--connect-timeout','30','--max-time','120','--proto','=ftp','--user',"$($env:FTP_USER):$($env:FTP_PASS)")
 if($ssl){$argsBase+='--ssl-reqd'}
 # Capture diagnostics to avoid credential/host disclosure; retain only a generic failure.
 & curl.exe @argsBase @Arguments 2> (Join-Path $scratch 'ftp-error.txt')
 if($LASTEXITCODE){throw 'FTP operation failed; no credentials printed'}
}
function FtpUrl([string]$Name){"ftp://$($env:FTP_HOST):$port/$Name"}
function Assert-Target([string]$Name){if($Name -cnotin @($policy.files.name)){throw 'FTP write outside exact allow-list'}}
function Upload-Target([string]$Name,[string]$Local){Assert-Target $Name;Invoke-Ftp @('--upload-file',$Local,(FtpUrl $Name))}
function Remove-NewTarget([string]$Name){
 # The only allowable rollback deletion is the newly introduced absent module.
 if($Name -cne 'career-admin.js'){throw 'Rollback deletion outside approved new-file scope'}
 $list=Read-RootNames
 if($Name -cin $list){Invoke-Ftp @('--quote',"DELE $Name",'--list-only','--output',(Join-Path $scratch 'delete-list.txt'),(FtpUrl ''))}
}
function Read-RootNames {
 $listPath=Join-Path $scratch 'root-list.txt'
 Invoke-Ftp @('--list-only','--output',$listPath,(FtpUrl ''))
 $names=@(Get-Content -LiteralPath $listPath | ForEach-Object {$_.TrimEnd('/')} | Where-Object {$_ -ne ''})
 # A failed RETR is NEVER treated as absence. Presence comes from a successful root listing.
 Assert-ReleaseRootNames $names $policy
 return ,$names
}
function Read-LiveSnapshot([string]$SaveTo) {
 $names=Read-RootNames
 New-Item -ItemType Directory -Path $SaveTo -Force | Out-Null
 $files=@(foreach($f in $policy.files){
  $exists=$f.name -cin $names
  if(-not $exists -and $f.name -cne 'career-admin.js'){throw "Required live baseline missing: $($f.name)"}
  if($exists){
   $p=Join-Path $SaveTo $f.name;Invoke-Ftp @('--output',$p,(FtpUrl $f.name))
   [ordered]@{name=$f.name;existed=$true;sha256=(Get-ReleaseHash $p);bytes=(Get-Item -LiteralPath $p).Length}
  }else{[ordered]@{name=$f.name;existed=$false;sha256=$null;bytes=0}}
 })
 $configNames=@($names | Where-Object {$_ -ieq 'web.config' -or $_ -imatch '^appsettings.*\.json$'})
 if('web.config' -cnotin $configNames -or 'appsettings.json' -cnotin $configNames){throw 'Required protected config missing'}
 $config=[ordered]@{}
 foreach($name in $configNames){
  if($name -cne 'web.config' -and $name -cnotmatch '^appsettings(?:\.[A-Za-z0-9_-]+)?\.json$'){throw 'Unrecognized configuration filename; review required'}
  $p=Join-Path $scratch $name;Invoke-Ftp @('--output',$p,(FtpUrl $name));$config[$name]=Get-ReleaseHash $p
  Remove-Item -LiteralPath $p -Force # Configuration CONTENT must never enter any artifact.
 }
 return [pscustomobject]@{files=$files;protectedConfiguration=[pscustomobject]$config}
}
function Run-Checked([string]$Executable,[string[]]$Arguments){ & $Executable @Arguments; if($LASTEXITCODE){throw "$Executable failed (exit $LASTEXITCODE)"} }
try {
 if($Operation -eq 'Preflight'){
  if(-not $SourceDirectory){throw 'Exact source checkout required'}
  $SourceDirectory=(Resolve-Path -LiteralPath $SourceDirectory).Path
  $head=(& git -C $SourceDirectory rev-parse HEAD).Trim()
  if($LASTEXITCODE -or $head -cne $policy.sourceSha){throw 'Wrong build checkout SHA'}
  & git -C $SourceDirectory diff --exit-code HEAD --
  if($LASTEXITCODE){throw 'Reviewed source checkout modified'}
  # Preserve the reviewed build's deterministic SourceLink path prefix. Archive ONLY
  # exact tracked source beneath its exact checkout; untracked uploads never enter it.
  $archive=Join-Path $scratch 'reviewed-source.zip'
  Run-Checked git @('-C',$SourceDirectory,'archive','--format=zip',"--output=$archive",$policy.sourceSha)
  $buildRoot=Join-Path $SourceDirectory 'outputs/career-admin-ui-validation/source-6182e40'
  if(Test-Path -LiteralPath $buildRoot){throw 'Exact-source build directory already exists'}
  Expand-Archive -LiteralPath $archive -DestinationPath $buildRoot
  Initialize-ReleaseBuildSdk $buildRoot
  $SourceDirectory=$buildRoot
  if((Test-Path -LiteralPath $PackageDirectory) -and @(Get-ChildItem -LiteralPath $PackageDirectory -Force).Count){throw 'Preflight output must be empty'}
  # These tests use disposable LocalDB. No production database secrets are provided.
  Push-Location $SourceDirectory
  try {
   $installed=@(& dotnet --list-sdks)
   $inventoryExit=$LASTEXITCODE
   $installed | ForEach-Object {Write-Host $_}
   $selectedOutput=@(& dotnet --version)
   $selectionExit=$LASTEXITCODE
   $selectedOutput | ForEach-Object {Write-Host $_}
   $selected=($selectedOutput -join "`n").Trim()
   Assert-ReleaseBuildSdk $installed $selected
   if($inventoryExit -ne 0 -or $selectionExit -ne 0){throw 'SDK diagnostics failed'}
   Run-Checked dotnet @('restore','StackMeet.sln','--configfile','NuGet.Config')
   foreach($project in Get-ChildItem tests -Recurse -Filter *.csproj | Sort-Object FullName){
    Run-Checked dotnet @('restore',$project.FullName,'--configfile','NuGet.Config')
    Run-Checked dotnet @('run','--project',$project.FullName,'-c','Release','--no-restore')
   }
   foreach($test in Get-ChildItem tests -Filter *.test.js | Sort-Object Name){Run-Checked node @($test.FullName)}
   Run-Checked node @('backend/StackMeet.Api/wwwroot/js/storage/storage-smoke.test.js')
   # Final exact-source CI build AFTER tests (tests may build with different properties).
   Run-Checked dotnet @('build','StackMeet.sln','-c','Release','--no-restore','-p:ContinuousIntegrationBuild=true',"-p:SourceRevisionId=$($policy.sourceSha)")
  } finally {Pop-Location}
  $payload=Join-Path $PackageDirectory 'payload';New-Item -ItemType Directory -Path $payload -Force | Out-Null
  foreach($f in $policy.files){Copy-Item -LiteralPath (Join-Path $SourceDirectory $f.repositoryPath) -Destination (Join-Path $payload $f.name)}
  Assert-ReleaseDirectory $payload $policy.files
  $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'StackMeet.Api.dll')).ProductVersion
  if($version -cne "1.0.0+$($policy.sourceSha)"){throw 'DLL source version mismatch'}
  $baseline=Read-LiveSnapshot (Join-Path $PackageDirectory 'baseline')
  $dll=@($baseline.files | Where-Object name -CEQ 'StackMeet.Api.dll')[0]
  if($dll.sha256 -cne $policy.baselineDllSha256){throw 'Unapproved live DLL baseline'}
  Write-ReleaseJson $baseline (Join-Path $PackageDirectory 'rollback-manifest.json')
  $manifest=[ordered]@{operation='preflight';sourceSha=$policy.sourceSha;workflowSha=$env:GITHUB_SHA;preflightRunId=$env:GITHUB_RUN_ID;files=$policy.files;rollbackManifestSha256=(Get-ReleaseHash (Join-Path $PackageDirectory 'rollback-manifest.json'));schemaMigration=$false;productionWrites=0;tests='ALL_EXACT_SOURCE_SUITES_PASS'}
  Write-ReleaseJson $manifest (Join-Path $PackageDirectory 'manifest.json')
  $hash=Get-ReleaseHash (Join-Path $PackageDirectory 'manifest.json')
  $null=Assert-ReleaseBundle $PackageDirectory $policy $hash
  Check-RemoteSource
  "RELEASE_MANIFEST_SHA256=$hash";'APPLICATION_PREFLIGHT=PASS';'PRODUCTION_WRITES=0'
  return
 }
 $manifest=Assert-ReleaseBundle $PackageDirectory $policy $ExpectedManifestSha256
 $run=GitHub "actions/runs/$PreflightRunId"
 $jobs=GitHub "actions/runs/$PreflightRunId/jobs?per_page=100"
 $job=@($jobs.jobs | Where-Object name -CEQ 'preflight')
 if($job.Count -ne 1){throw 'Preflight job receipt missing or ambiguous'}
 Assert-PreflightRun $run $job[0] $manifest $PreflightRunId $env:GITHUB_SHA
 if($Operation -eq 'VerifyRollback'){
  Assert-LiveSnapshot (Read-ReleaseJson (Join-Path $PackageDirectory 'rollback-manifest.json')) (Read-LiveSnapshot (Join-Path $scratch 'verification'))
  'ROLLBACK_VERIFIED=TRUE';'PRODUCTION_WRITES=0';return
 }
 # Fresh complete live backup; refuse drift from preflight BEFORE first write.
 $fresh=Join-Path $env:RUNNER_TEMP 'career-admin-ui-rollback'
 if(Test-Path -LiteralPath $fresh){throw 'Rollback output already exists; use a fresh runner'}
 New-Item -ItemType Directory -Path $fresh | Out-Null
 $old=Read-ReleaseJson (Join-Path $PackageDirectory 'rollback-manifest.json')
 $current=Read-LiveSnapshot (Join-Path $fresh 'baseline')
 Assert-LiveSnapshot $old $current
 Assert-ReleaseDirectory (Join-Path $fresh 'baseline') @($old.files | Where-Object existed)
 Copy-Item -LiteralPath (Join-Path $PackageDirectory 'manifest.json'),(Join-Path $PackageDirectory 'rollback-manifest.json') -Destination $fresh
 # Deploy uses fresh verified backup files; preflight baseline hashes already matched.
 foreach($f in $old.files | Where-Object existed){Copy-Item -LiteralPath (Join-Path (Join-Path $fresh 'baseline') $f.name) -Destination (Join-Path (Join-Path $PackageDirectory 'baseline') $f.name)}
 $status='FAILED'
 try{
  $status=Invoke-ReleaseTransaction $PackageDirectory $policy ${function:Upload-Target} ${function:Remove-NewTarget} {Read-LiveSnapshot (Join-Path $scratch 'current')} {Check-RemoteSource}
  'CAREER_PROFILE_APPLICATION_DEPLOYMENT=PASS';'PROTECTED_CONFIGURATION_HASHES=UNCHANGED';'SITE_STATE=STOPPED-MANUAL-START-REQUIRED'
 }catch{$status=$_.Exception.Message;throw}finally{
  Write-ReleaseJson ([ordered]@{status=$status;sourceSha=$policy.sourceSha;siteState='STOPPED-MANUAL-START-REQUIRED';sharedPoolStopped=$false;sharedPoolRecycled=$false;identityWrites=0;profileLinkWrites=0}) (Join-Path $fresh 'deployment.json')
 }
}finally{
 $resolved=[IO.Path]::GetFullPath($scratch)
 $root=[IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 if(-not $resolved.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Scratch cleanup escaped runner temp'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
