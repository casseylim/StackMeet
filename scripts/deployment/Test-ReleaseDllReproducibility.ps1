[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourceDirectory,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$RunSuites,[switch]$MirrorPreflight)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'NadiTrackApplicationRelease.psm1') -Force
$policy=Read-ReleaseJson (Join-Path $PSScriptRoot 'career-profile-admin-ui.approved.json')
$SourceDirectory=(Resolve-Path $SourceDirectory).Path
$head=(& git -C $SourceDirectory rev-parse HEAD).Trim()
if($LASTEXITCODE -or $head -cne $policy.sourceSha){throw 'Exact reviewed checkout required'}
& git -C $SourceDirectory diff --exit-code HEAD --;if($LASTEXITCODE){throw 'Reviewed source modified'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path $OutputDirectory){throw 'Use a fresh output directory'}
New-Item -ItemType Directory $OutputDirectory|Out-Null
$archive=Join-Path $OutputDirectory 'source.zip'
& git -C $SourceDirectory archive --format=zip "--output=$archive" $policy.sourceSha
if($LASTEXITCODE){throw 'Exact archive failed'}
function Run([string]$Exe,[string[]]$Arguments,[string]$Log){
 & $Exe @Arguments *> $Log
 if($LASTEXITCODE){throw "$Exe failed: $([IO.Path]::GetFileName($Log))"}
}
function Get-DllMeasurement([string]$BuildRoot){& (Join-Path $PSScriptRoot 'Measure-ReleaseDll.ps1') $BuildRoot}
$builds=@(foreach($number in 1..3){
 $checkout=Join-Path $OutputDirectory "build-$number"
 if($MirrorPreflight -and $number -eq 1){$checkout=$SourceDirectory}else{
 Run git @('clone','--shared','--no-checkout',$SourceDirectory,$checkout) (Join-Path $OutputDirectory "clone-$number.log")
 # Match actions/checkout's GitHub remote, not the filesystem clone transport.
 Run git @('-C',$checkout,'remote','set-url','origin','https://github.com/casseylim/StackMeet') (Join-Path $OutputDirectory "remote-$number.log")
 Run git @('-C',$checkout,'checkout','--detach',$policy.sourceSha) (Join-Path $OutputDirectory "checkout-$number.log")
 }
 $buildRoot=Join-Path $checkout 'outputs/career-admin-ui-validation/source-6182e40'
 if(Test-Path $buildRoot){throw 'Disposable build root already exists'}
 Expand-Archive $archive $buildRoot
 Initialize-ReleaseBuildSdk $buildRoot
 Push-Location $buildRoot
 try{
  $sdk=(& dotnet --version).Trim();Assert-ReleaseBuildSdk @(& dotnet --list-sdks) $sdk
  Run dotnet @('restore','StackMeet.sln','--configfile','NuGet.Config') (Join-Path $OutputDirectory "restore-$number.log")
  Run dotnet @('build','StackMeet.sln','-c','Release','--no-restore','-p:ContinuousIntegrationBuild=true',"-p:SourceRevisionId=$($policy.sourceSha)") (Join-Path $OutputDirectory "build-$number.log")
  $result=Get-DllMeasurement $buildRoot
  $result['number']=$number;$result['buildRoot']=$buildRoot;$result['sdk']=$sdk
  $result|ConvertTo-Json -Depth 25|Set-Content (Join-Path $OutputDirectory "build-$number.json")
  foreach($extension in 'dll','pdb'){Copy-Item (Join-Path $buildRoot "backend/StackMeet.Api/bin/Release/net8.0/StackMeet.Api.$extension") (Join-Path $OutputDirectory "measurement-$number.$extension")}
  "BUILD_${number}_SHA256=$($result.dllSha256)"|Write-Host
  "BUILD_${number}_BYTES=$($result.dllBytes)"|Write-Host
 }finally{Pop-Location}
 $result
})
$post=$null
if($RunSuites){
 $buildRoot=$builds[0].buildRoot;Push-Location $buildRoot
 try{
  foreach($project in Get-ChildItem tests -Recurse -Filter *.csproj|Sort-Object FullName){
   Run dotnet @('restore',$project.FullName,'--configfile','NuGet.Config') (Join-Path $OutputDirectory ($project.BaseName+'.restore.log'))
   Run dotnet @('run','--project',$project.FullName,'-c','Release','--no-restore') (Join-Path $OutputDirectory ($project.BaseName+'.test.log'))
   Write-Host "SUITE_PASS=$($project.BaseName)"
  }
  foreach($test in Get-ChildItem tests -Filter *.test.js|Sort-Object Name){Run node @($test.FullName) (Join-Path $OutputDirectory ($test.Name+'.log'))}
  Run node @('backend/StackMeet.Api/wwwroot/js/storage/storage-smoke.test.js') (Join-Path $OutputDirectory 'storage.log')
  Run dotnet @('build','StackMeet.sln','-c','Release','--no-restore','-p:ContinuousIntegrationBuild=true',"-p:SourceRevisionId=$($policy.sourceSha)") (Join-Path $OutputDirectory 'post-test-build.log')
  $post=Get-DllMeasurement $buildRoot
  $post|ConvertTo-Json -Depth 25|Set-Content (Join-Path $OutputDirectory 'post-test.json')
  foreach($extension in 'dll','pdb'){Copy-Item (Join-Path $buildRoot "backend/StackMeet.Api/bin/Release/net8.0/StackMeet.Api.$extension") (Join-Path $OutputDirectory "post-test.$extension")}
  Write-Host "POST_TEST_BUILD_SHA256=$($post.dllSha256)"
 }finally{Pop-Location}
}
foreach($b in $builds){& git -C (Split-Path (Split-Path (Split-Path $b.buildRoot))) diff --exit-code HEAD --;if($LASTEXITCODE){throw 'Original checkout changed'}}
$summary=[ordered]@{sourceSha=$policy.sourceSha;expectedDllSha256=$policy.files[-1].sha256;expectedDllBytes=$policy.files[-1].bytes;builds=$builds;postTest=$post;sameMachineReproducible=@($builds.dllSha256|Select-Object -Unique).Count -eq 1;pathIndependent=$builds[0].dllSha256 -ceq $builds[1].dllSha256;testSequenceAltersDll=$(if($post){$builds[0].dllSha256 -cne $post.dllSha256}else{$null});dependencyResolutionStable=@($builds.dependencyResolutionSha256|Select-Object -Unique).Count -eq 1;productionConnections=0;productionWrites=0;deploymentArtifact=$false}
$summary|ConvertTo-Json -Depth 30|Set-Content (Join-Path $OutputDirectory 'summary.json')
Write-Host 'OFFLINE_DLL_ANALYSIS=COMPLETE; PRODUCTION_WRITES=0'
