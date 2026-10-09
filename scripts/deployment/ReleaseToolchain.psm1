$ErrorActionPreference='Stop'
function Enable-ReleaseToolchain([string]$DotnetExecutable) {
 if(-not [IO.Path]::IsPathFullyQualified($DotnetExecutable) -or -not (Test-Path -LiteralPath $DotnetExecutable -PathType Leaf)){throw 'Isolated toolchain executable unavailable'}
 $root=Split-Path $DotnetExecutable
 if([IO.Path]::GetFileName($DotnetExecutable) -cne 'dotnet.exe'){throw 'Isolated toolchain executable unavailable'}
 $env:DOTNET_ROOT=$root;$env:DOTNET_ROOT_X64=$root;$env:DOTNET_HOST_PATH=$DotnetExecutable
 $env:DOTNET_MULTILEVEL_LOOKUP='0'
 # net8 apps need patch roll-forward; the isolated compiler has only patch 10.0.11.
 $env:DOTNET_ROLL_FORWARD='LatestPatch'
 $env:PATH=$root+[IO.Path]::PathSeparator+$env:PATH
 $env:UseSharedCompilation='false'
}
function Assert-ReleaseToolchainInventory([string]$Root,[string]$Sdk,[string[]]$Runtimes) {
 if($Sdk -cne '10.0.400'){throw 'Isolated toolchain requires SDK 10.0.400'}
 $core10=@($Runtimes|Where-Object {$_ -cmatch '^Microsoft.NETCore.App 10\.'})
 if($core10.Count -ne 1 -or $core10[0] -cnotmatch '^Microsoft.NETCore.App 10\.0\.11 \['){throw 'Isolated toolchain requires only compiler runtime 10.0.11'}
 foreach($line in $Runtimes){
  if($line -notmatch '\[([^\]]+)\]$' -or -not [IO.Path]::GetFullPath($Matches[1]).StartsWith($Root.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime outside isolated toolchain'}
 }
 Write-Host "ISOLATED_DOTNET_VERSION=$Sdk"
 $Runtimes|ForEach-Object {Write-Host "ISOLATED_RUNTIME_LIST=$_"}
}
function Assert-ReleaseToolchain([string]$DotnetExecutable) {
 if(-not (Test-Path -LiteralPath $DotnetExecutable -PathType Leaf)){throw 'Isolated toolchain executable unavailable'}
 $sdk=(& $DotnetExecutable --version) -join '';if($LASTEXITCODE){throw 'Isolated SDK selection failed'}
 $runtimes=@(& $DotnetExecutable --list-runtimes);if($LASTEXITCODE){throw 'Isolated runtime inventory failed'}
 Assert-ReleaseToolchainInventory (Split-Path $DotnetExecutable) $sdk.Trim() $runtimes
}
function Get-ReleaseCompilerRuntime([string]$Pdb) {
 $stream=[IO.File]::OpenRead($Pdb)
 $provider=[System.Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
 try{
  $reader=$provider.GetMetadataReader();$versions=@(foreach($handle in $reader.CustomDebugInformation){
   $row=$reader.GetCustomDebugInformation($handle)
   if($reader.GetGuid($row.Kind).ToString() -ceq 'b5feec05-8cd0-4a83-96da-466284bb4bd8'){
    $parts=[Text.Encoding]::UTF8.GetString($reader.GetBlobBytes($row.Value)).Split([char]0)
    for($i=0;$i -lt $parts.Length-1;$i+=2){if($parts[$i] -ceq 'runtime-version'){$parts[$i+1]}}
   }
  })
  if($versions.Count -ne 1){throw 'Compiler runtime cannot be proven from portable PDB'}
  return $versions[0]
 }finally{$provider.Dispose();$stream.Dispose()}
}
function Assert-ReleaseCompilerRuntime([string]$Version) {
 Write-Host "COMPILER_EXECUTION_RUNTIME=$Version"
 if($Version -cne '10.0.11-servicing.26373.116+e2f47b0110ed922f21a1522da67279133ce28f32'){throw 'Compiler did not execute on reviewed runtime 10.0.11'}
}
Export-ModuleMember -Function *-Release*
