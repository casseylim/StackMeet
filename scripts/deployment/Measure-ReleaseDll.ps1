param([Parameter(Mandatory)][string]$BuildRoot,[string]$DotnetExecutable='dotnet')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseToolchain.psm1') -Force
$project=Join-Path $BuildRoot 'backend/StackMeet.Api'
$dll=Join-Path $project 'bin/Release/net8.0/StackMeet.Api.dll'
$obj=Join-Path $project 'obj/Release/net8.0'
$assets=Get-Content (Join-Path $project 'obj/project.assets.json') -Raw|ConvertFrom-Json
$libraries=@($assets.libraries.PSObject.Properties|Sort-Object Name|ForEach-Object{[ordered]@{name=$_.Name;sha512=$_.Value.sha512;type=$_.Value.type}})
$resolution=[ordered]@{libraries=$libraries;frameworks=$assets.project.frameworks}
$normalized=$resolution|ConvertTo-Json -Depth 15 -Compress
$sha=[Security.Cryptography.SHA256]::Create()
$resolutionHash=[Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized)))
$stream=[IO.File]::OpenRead($dll)
$pe=[System.Reflection.PortableExecutable.PEReader]::new($stream)
try{
 $reader=[System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
 $mvid=$reader.GetGuid($reader.GetModuleDefinition().Mvid).ToString()
 $debug=@(foreach($entry in $pe.ReadDebugDirectory()){
  $item=[ordered]@{type=$entry.Type.ToString();stamp=$entry.Stamp;major=$entry.MajorVersion;minor=$entry.MinorVersion;size=$entry.DataSize}
  if($entry.Type.ToString() -eq 'CodeView'){$cv=$pe.ReadCodeViewDebugDirectoryData($entry);$item.path=$cv.Path;$item.guid=$cv.Guid.ToString();$item.age=$cv.Age}
  [pscustomobject]$item
 })
 $stamp=$pe.PEHeaders.CoffHeader.TimeDateStamp
}finally{$pe.Dispose();$stream.Dispose()}
$generated=@(Get-ChildItem $obj -File|Where-Object {$_.Extension -eq '.cs' -or $_.Name -like '*sourcelink.json'}|Sort-Object Name|ForEach-Object{[ordered]@{name=$_.Name;sha256=(Get-FileHash $_.FullName).Hash}})
$sourceLink=Get-ChildItem $obj -Filter '*sourcelink.json'|Select-Object -First 1
$inputJson=& $DotnetExecutable msbuild (Join-Path $project 'StackMeet.Api.csproj') -target:InitializeSourceControlInformation,InitializeSourceRootMappedPaths -getProperty:SourceRevisionId,PathMap,ContinuousIntegrationBuild,Deterministic,MSBuildVersion,RoslynTargetsPath -getItem:SourceRoot -p:ContinuousIntegrationBuild=true -p:SourceRevisionId=6182e405ced08c24cf5f8738f1172c8df5197d3e
if($LASTEXITCODE){throw 'Safe build-input query failed'}
$inputs=($inputJson -join "`n")|ConvertFrom-Json
$compiler=Join-Path $inputs.Properties.RoslynTargetsPath 'bincore/csc.dll'
$compilerVersion=(& $DotnetExecutable $compiler -version) -join ''
if($LASTEXITCODE){throw 'Compiler version query failed'}
[ordered]@{compilerRuntime=(Get-ReleaseCompilerRuntime (Join-Path $project 'bin/Release/net8.0/StackMeet.Api.pdb'));dllSha256=(Get-FileHash $dll).Hash;dllBytes=(Get-Item $dll).Length;informationalVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion;mvid=$mvid;peStamp=$stamp;debug=$debug;generated=$generated;sourceLink=$(if($sourceLink){Get-Content $sourceLink.FullName -Raw}else{$null});assetsRawSha256=(Get-FileHash (Join-Path $project 'obj/project.assets.json')).Hash;dependencyResolutionSha256=$resolutionHash;dependencyResolution=$resolution;buildInputs=$inputs;compilerVersion=$compilerVersion;compilerSha256=(Get-FileHash $compiler).Hash}
