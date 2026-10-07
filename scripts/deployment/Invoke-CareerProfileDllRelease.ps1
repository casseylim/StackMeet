param(
 [Parameter(Mandatory)][ValidateSet('Preflight','Deploy')][string]$Operation,
 [Parameter(Mandatory)][string]$PackageDirectory,
 [string]$SourceDll
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$lockedSource='1e2c0211aa00d39d55e444c42565c4111592fe78'
$lockedLive='1533B551313FAAFF6B3A28985EC4FB5EB3FF3D4DEE04EEEEF82894479387D097'
if($env:SOURCE_SHA -cne $lockedSource -or $env:EXPECTED_LIVE_DLL -cne $lockedLive){throw 'Locked source or live baseline mismatch'}
if($env:GITHUB_REF -cne 'refs/heads/master'){throw 'Protected master dispatch required'}
foreach($name in 'FTP_HOST','FTP_USER','FTP_PASS','FTP_PORT','FTP_SSL','GH_TOKEN'){
 if([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))){throw "Missing $name"}
}
$port=0
if(-not[int]::TryParse($env:FTP_PORT,[ref]$port)-or$port-lt1-or$port-gt65535){throw 'Invalid FTP port'}
$ssl=switch($env:FTP_SSL.Trim().ToLower()){'true'{$true}'false'{$false}default{throw 'Invalid FTP_SSL'}}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToUpperInvariant()}
function CurlBase{
 $arguments=@('--fail','--silent','--show-error','--ftp-pasv','--connect-timeout','30','--max-time','120','--proto','=ftp','--user',"$($env:FTP_USER):$($env:FTP_PASS)")
 if($ssl){$arguments+='--ssl-reqd'}
 return ,$arguments
}
function Retrieve([string]$remote,[string]$local){
 $arguments=@(CurlBase)+@('--output',$local,"ftp://$($env:FTP_HOST):$port$remote")
 & curl.exe @arguments
 if($LASTEXITCODE){throw "Read failed: $remote"}
}
function UploadDll([string]$local){
 # Hard-coded single destination: no config, assets, upload/data directories or app_offline file.
 $arguments=@(CurlBase)+@('--upload-file',$local,"ftp://$($env:FTP_HOST):$port/StackMeet.Api.dll")
 & curl.exe @arguments
 if($LASTEXITCODE){throw 'DLL upload failed'}
}
function GitHub([string]$path){
 Invoke-RestMethod "https://api.github.com/repos/$env:GITHUB_REPOSITORY/$path" -Headers @{
 Authorization="Bearer $env:GH_TOKEN";Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'} -TimeoutSec 30
}
function CheckMaster([string]$expected){
 if((GitHub 'commits/master').sha -cne $expected){throw 'Master drifted; abort before writing'}
}
$scratch=Join-Path $env:RUNNER_TEMP ('career-read-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch|Out-Null
function ReadLive{
 $dll=Join-Path $scratch 'live.dll';Retrieve '/StackMeet.Api.dll' $dll
 $liveHash=Hash $dll
 if($liveHash -cne $lockedLive){throw 'Live DLL drifted; no deployment permitted'}
 $configuration=[ordered]@{}
 foreach($remote in '/web.config','/appsettings.json'){
  $local=Join-Path $scratch ([IO.Path]::GetFileName($remote))
  Retrieve $remote $local
  $configuration[$remote]=Hash $local
 }
 return [pscustomobject]@{dllSha256=$liveHash;configuration=$configuration}
}
function VerifyConfig($expected){
 foreach($remote in '/web.config','/appsettings.json'){
  $local=Join-Path $scratch ([IO.Path]::GetFileName($remote))
  Retrieve $remote $local
  if((Hash $local) -cne [string]$expected.$remote){throw "Protected configuration drift: $remote"}
 }
}
try{
 CheckMaster $env:GITHUB_SHA
 $manifestPath=Join-Path $PackageDirectory 'manifest.json'
 if($Operation -eq 'Preflight'){
  if(-not(Test-Path -LiteralPath $SourceDll -PathType Leaf)){throw 'Source DLL missing'}
  $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $SourceDll).Path).ProductVersion
  if($version -notlike "*+$lockedSource"){throw 'DLL informational version does not bind exact source SHA'}
  $live=ReadLive
  New-Item -ItemType Directory -Force -Path $PackageDirectory|Out-Null
  Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $PackageDirectory 'StackMeet.Api.dll')
  $baseline=Join-Path $PackageDirectory 'baseline'
  New-Item -ItemType Directory -Force -Path $baseline|Out-Null
  Copy-Item -LiteralPath (Join-Path $scratch 'live.dll') -Destination (Join-Path $baseline 'StackMeet.Api.dll')
  $manifest=[ordered]@{sourceSha=$lockedSource;workflowMasterSha=$env:GITHUB_SHA;preflightRunId=$env:GITHUB_RUN_ID;
   builtAtUtc=[DateTime]::UtcNow.ToString('o');deployedBaselineSha='c0c4d82fd81375543e1962eb7d8f12508a3a8ba8';
   dllSha256=(Hash $SourceDll);liveDllSha256=$live.dllSha256;protectedConfiguration=$live.configuration;
   files=@('/StackMeet.Api.dll');schemaMigration=$false;businessDataWrites=0}
  $manifest|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $manifestPath -Encoding utf8
  "RELEASE_MANIFEST_SHA256=$(Hash $manifestPath)"
  "TARGET_DLL_SHA256=$($manifest.dllSha256)"
  'CAREER_PROFILE_PREFLIGHT=PASS';'PRODUCTION_WRITES=0'
  exit 0
 }
 if($env:POOL_CONFIRMATION -cne 'SITE-STOPPED' -or $env:DEPLOYMENT_CONFIRMATION -cne 'DEPLOY CAREER PROFILE DLL'){throw 'Site-stop/deployment interlock failed'}
 if($env:PREFLIGHT_RUN_ID -notmatch '^\d+$' -or $env:EXPECTED_MANIFEST -notmatch '^[A-Fa-f0-9]{64}$'){throw 'Preflight evidence missing'}
 $run=GitHub "actions/runs/$env:PREFLIGHT_RUN_ID"
 if($run.conclusion -cne 'success' -or $run.event -cne 'workflow_dispatch' -or $run.head_branch -cne 'master' -or
  $run.path -cne '.github/workflows/deploy-career-profile-remediation.yml'){throw 'Preflight run ownership/state invalid'}
 if((Hash $manifestPath) -cne $env:EXPECTED_MANIFEST.ToUpperInvariant()){throw 'Preflight artifact manifest mismatch'}
 $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
 if($manifest.sourceSha -cne $lockedSource -or $manifest.workflowMasterSha -cne $env:GITHUB_SHA -or
  $run.head_sha -cne $env:GITHUB_SHA -or $manifest.preflightRunId -cne $env:PREFLIGHT_RUN_ID -or
  $manifest.schemaMigration -ne $false -or @($manifest.files).Count -ne 1 -or $manifest.files[0] -cne '/StackMeet.Api.dll'){throw 'Release package scope invalid'}
 $target=Join-Path $PackageDirectory 'StackMeet.Api.dll'
 if((Hash $target) -cne $manifest.dllSha256){throw 'Target artifact checksum mismatch'}
 $live=ReadLive
 VerifyConfig $manifest.protectedConfiguration
 $rollback=Join-Path $env:RUNNER_TEMP 'career-rollback'
 New-Item -ItemType Directory -Force -Path $rollback|Out-Null
 $backup=Join-Path $rollback 'StackMeet.Api.dll'
 Copy-Item -LiteralPath (Join-Path $scratch 'live.dll') -Destination $backup
 Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $rollback 'manifest.json')
 CheckMaster $env:GITHUB_SHA
 $attempted=$false
 try{
  # Track the attempted write BEFORE STOR, so a partial upload failure is also rolled back.
  $attempted=$true
  UploadDll $target
  $verified=Join-Path $scratch 'after.dll';Retrieve '/StackMeet.Api.dll' $verified
  if((Hash $verified) -cne $manifest.dllSha256){throw 'Post-upload DLL checksum mismatch'}
  VerifyConfig $manifest.protectedConfiguration
  [ordered]@{status='PASS';sourceSha=$lockedSource;deployedAtUtc=[DateTime]::UtcNow.ToString('o');
   dllSha256=$manifest.dllSha256;previousDllSha256=$lockedLive;files=@('/StackMeet.Api.dll');
   webConfigPreserved=$true;appsettingsPreserved=$true;businessDataWrites=0;identityWrites=0;profileLinkWrites=0}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $rollback 'deployment.json')
  'CAREER_PROFILE_DLL_DEPLOYMENT=PASS';'PROTECTED_CONFIGURATION_HASHES=UNCHANGED';'SITE_STATE=STOPPED-MANUAL-START-REQUIRED';'IDENTITY_WRITES=0';'PROFILE_LINK_WRITES=0'
 }catch{
  $failure=$_.Exception.Message
  if($attempted){
   try{UploadDll $backup;$check=Join-Path $scratch 'rollback.dll';Retrieve '/StackMeet.Api.dll' $check
    if((Hash $check) -cne $lockedLive){throw 'Rollback checksum mismatch'}
    VerifyConfig $manifest.protectedConfiguration
    'ROLLBACK_VERIFIED=TRUE'
   }catch{'ROLLBACK_VERIFIED=FALSE';throw 'CRITICAL: rollback unverified; keep site stopped'}
  }
  throw "Deployment failed; previous DLL restored. $failure"
 }
}finally{
 # Delete only this checked, generated scratch directory containing read-only config copies.
 $resolved=[IO.Path]::GetFullPath($scratch)
 $runnerRoot=[IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 if(-not$resolved.StartsWith($runnerRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Scratch cleanup target escaped runner temp'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
