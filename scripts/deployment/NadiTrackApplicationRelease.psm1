Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Get-ReleaseHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant() }
function Read-ReleaseJson([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
function Write-ReleaseJson($Value,[string]$Path) { $Value | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Assert-ReleaseNames($Names,$Expected) {
 $actual=@($Names | Sort-Object -CaseSensitive); $wanted=@($Expected | Sort-Object -CaseSensitive)
 if($actual.Count -ne $wanted.Count -or ($actual -join '|') -cne ($wanted -join '|')) { throw 'Exact file allow-list mismatch' }
}
function Assert-ReleasePolicy($Policy) {
 if($Policy.sourceSha -cne '6182e405ced08c24cf5f8738f1172c8df5197d3e'){throw 'Wrong approved source SHA'}
 Assert-ReleaseNames @($Policy.files.name) @('admin.html','admin.js','admin.css','career-admin.js','StackMeet.Api.dll')
 if(($Policy.files.name -join '|') -cne 'admin.html|admin.js|admin.css|career-admin.js|StackMeet.Api.dll'){throw 'Approved DLL-last order required'}
 foreach($f in $Policy.files){if($f.productionPath -cne ('/'+$f.name)){throw 'Invalid production destination'};if($f.sha256 -cnotmatch '^[A-F0-9]{64}$' -or $f.bytes -le 0){throw 'Invalid approved file metadata'}}
}
function Assert-ReleaseDirectory([string]$Directory,$Files) {
 if(-not(Test-Path -LiteralPath $Directory -PathType Container)){throw 'Package directory missing'}
 $items=@(Get-ChildItem -LiteralPath $Directory -Force)
 Assert-ReleaseNames @($items.Name) @($Files.name)
 foreach($item in $items){if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Directory or link in payload'}}
 foreach($f in $Files){
  $p=Join-Path $Directory $f.name;$actualHash=Get-ReleaseHash $p;$actualBytes=(Get-Item -LiteralPath $p).Length
  if($actualHash -cne $f.sha256 -or $actualBytes -ne $f.bytes){
   Write-Host "MISMATCH_FILE=$($f.name)"
   Write-Host "EXPECTED_SHA256=$($f.sha256)";Write-Host "ACTUAL_SHA256=$actualHash"
   Write-Host "EXPECTED_BYTES=$($f.bytes)";Write-Host "ACTUAL_BYTES=$actualBytes"
   throw "File hash/size mismatch: $($f.name)"
  }
 }
}
function Assert-ReleaseRootNames($Names,$Policy) {
 if(@($Names | Where-Object {$_ -match '[/\\]' -or $_ -eq '..'}).Count){throw 'Ambiguous FTP root listing'}
 if(@($Names | Group-Object | Where-Object Count -GT 1).Count){throw 'Ambiguous duplicate FTP root names'}
 foreach($f in $Policy.files){if($f.name -iin $Names -and $f.name -cnotin $Names){throw 'Ambiguous destination filename case'}}
}
function Assert-SourceGate($Policy,$Context) {
 Assert-ReleasePolicy $Policy
 if($Context.repository -ine 'casseylim/StackMeet' -or $Context.ref -cne 'refs/heads/master' -or -not $Context.protected -or
    $Context.remoteSource -cne $Policy.sourceSha -or $Context.relationship -notin @('ahead','identical') -or
    $Context.masterSha -cne $Context.workflowSha){throw 'Remote protected master/source reachability gate failed'}
}
function Assert-DeployInterlocks($RunId,$Hash,$Site,$Confirmation) {
 if($RunId -cnotmatch '^[1-9][0-9]*$' -or $Hash -cnotmatch '^[A-Fa-f0-9]{64}$'){throw 'Missing preflight run/manifest evidence'}
 if($Site -cne 'SITE-STOPPED-NADITRACK-ONLY'){throw 'NADITrack-only site-stop confirmation required'}
 if($Confirmation -cne 'DEPLOY-CAREER-PROFILE-ADMIN-UI'){throw 'Deployment confirmation required'}
}
function Assert-PreflightRun($Run,$Job,$Manifest,$RunId,$WorkflowSha) {
 if($Run.id.ToString() -cne $RunId -or $Run.status -cne 'completed' -or $Run.conclusion -cne 'success' -or
    $Run.event -cne 'workflow_dispatch' -or $Run.head_branch -cne 'master' -or $Run.head_sha -cne $WorkflowSha -or
    $Run.path -cne '.github/workflows/deploy-career-profile-admin-ui.yml' -or
    $Job.name -cne 'preflight' -or $Job.conclusion -cne 'success' -or
    $Manifest.operation -cne 'preflight' -or $Manifest.preflightRunId -cne $RunId -or $Manifest.workflowSha -cne $WorkflowSha){throw 'Successful matching preflight job/run required'}
}
function Assert-ReleaseBundle([string]$Directory,$Policy,[string]$ExpectedHash) {
 Assert-ReleasePolicy $Policy
 Assert-ReleaseNames @(Get-ChildItem -LiteralPath $Directory -Force | Select-Object -ExpandProperty Name) @('payload','baseline','manifest.json','rollback-manifest.json')
 foreach($item in Get-ChildItem -LiteralPath $Directory -Force -Recurse){if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Links forbidden in bundle'}}
 $mp=Join-Path $Directory 'manifest.json'
 if($ExpectedHash -cnotmatch '^[A-Fa-f0-9]{64}$' -or (Get-ReleaseHash $mp) -cne $ExpectedHash.ToUpperInvariant()){throw 'Preflight manifest mismatch'}
 $m=Read-ReleaseJson $mp
 if($m.sourceSha -cne $Policy.sourceSha -or $m.schemaMigration -ne $false -or $m.productionWrites -ne 0){throw 'Wrong source or package scope'}
 Assert-ReleaseNames @($m.files.name) @($Policy.files.name)
 foreach($f in $Policy.files){$a=@($m.files | Where-Object name -CEQ $f.name)[0]; if($a.sha256 -cne $f.sha256 -or $a.bytes -ne $f.bytes){throw 'Approved manifest file mismatch'}}
 Assert-ReleaseDirectory (Join-Path $Directory 'payload') $Policy.files
 if((Get-ReleaseHash (Join-Path $Directory 'rollback-manifest.json')) -cne $m.rollbackManifestSha256){throw 'Rollback manifest mismatch'}
 $r=Read-ReleaseJson (Join-Path $Directory 'rollback-manifest.json')
 Assert-ReleaseNames @($r.files.name) @($Policy.files.name)
 if(($r.files.name -join '|') -cne ($Policy.files.name -join '|')){throw 'Rollback DLL-last order required'}
 foreach($f in $r.files){
  if($f.existed -isnot [bool]){throw 'Invalid rollback presence state'}
  if(-not $f.existed -and $f.name -cne 'career-admin.js'){throw 'Required baseline file missing'}
  if($f.existed -and ($f.sha256 -cnotmatch '^[A-F0-9]{64}$' -or $f.bytes -le 0)){throw 'Invalid backup metadata'}
 }
 $dll=@($r.files | Where-Object name -CEQ 'StackMeet.Api.dll')[0]
 if($dll.sha256 -cne $Policy.baselineDllSha256){throw 'Unapproved live DLL baseline'}
 Assert-ReleaseDirectory (Join-Path $Directory 'baseline') @($r.files | Where-Object existed)
 if(-not $r.protectedConfiguration.PSObject.Properties['web.config'] -or -not $r.protectedConfiguration.PSObject.Properties['appsettings.json']){throw 'Protected configuration hashes missing'}
 foreach($p in $r.protectedConfiguration.PSObject.Properties){if($p.Name -cne 'web.config' -and $p.Name -cnotmatch '^appsettings(?:\.[A-Za-z0-9_-]+)?\.json$'){throw 'Invalid protected config name'}; if($p.Value -cnotmatch '^[A-F0-9]{64}$'){throw 'Invalid config hash'}}
 return $m
}
function Assert-LiveSnapshot($Expected,$Actual) {
 Assert-ReleaseNames @($Actual.files.name) @($Expected.files.name)
 foreach($e in $Expected.files){$a=@($Actual.files | Where-Object name -CEQ $e.name)[0]; if($e.existed -ne $a.existed -or ($e.existed -and ($e.sha256 -cne $a.sha256 -or $e.bytes -ne $a.bytes))){throw "Live baseline drift: $($e.name)"}}
 Assert-ReleaseNames @($Actual.protectedConfiguration.PSObject.Properties.Name) @($Expected.protectedConfiguration.PSObject.Properties.Name)
 foreach($p in $Expected.protectedConfiguration.PSObject.Properties){if($Actual.protectedConfiguration.($p.Name) -cne $p.Value){throw "Protected configuration drift: $($p.Name)"}}
}
# Transport callbacks are supplied only by the guarded entry point (or isolated fake tests).
# There is no test bypass switch on the production entry point.
function Invoke-ReleaseTransaction([string]$Directory,$Policy,[scriptblock]$Upload,[scriptblock]$Delete,[scriptblock]$Snapshot,[scriptblock]$BeforeWrite) {
 $r=Read-ReleaseJson (Join-Path $Directory 'rollback-manifest.json')
 Assert-LiveSnapshot $r (& $Snapshot)
 & $BeforeWrite
 $attempted=$false
 try {
  foreach($f in $Policy.files){
   $attempted=$true # BEFORE STOR: partial uploads must also roll back.
   & $Upload $f.name (Join-Path (Join-Path $Directory 'payload') $f.name)
   $after=& $Snapshot
   $a=@($after.files | Where-Object name -CEQ $f.name)[0]
   if(-not $a.existed -or $a.sha256 -cne $f.sha256 -or $a.bytes -ne $f.bytes){throw "Upload verification failed: $($f.name)"}
  }
  $expected=[pscustomobject]@{files=@($Policy.files | ForEach-Object { [pscustomobject]@{name=$_.name;existed=$true;sha256=$_.sha256;bytes=$_.bytes} });protectedConfiguration=$r.protectedConfiguration}
  Assert-LiveSnapshot $expected (& $Snapshot)
  return 'PASS'
 } catch {
  $original=$_.Exception.Message
  if($attempted){
   $errors=[Collections.Generic.List[string]]::new()
   # Restore EVERY member, not only those with successful upload receipts; DLL last.
   foreach($f in $r.files){try{if($f.existed){& $Upload $f.name (Join-Path (Join-Path $Directory 'baseline') $f.name)}else{& $Delete $f.name}}catch{$errors.Add($f.name)}}
   try{Assert-LiveSnapshot $r (& $Snapshot)}catch{$errors.Add('whole-set verification')}
   if($errors.Count){throw "CRITICAL: rollback unverified ($($errors -join ', ')); keep NADITrack stopped"}
   throw "ROLLED_BACK: complete previous release verified; keep site stopped pending review. $original"
  }
  throw
 }
}
function Initialize-ReleaseBuildSdk([string]$BuildRoot) {
 $pin=Join-Path $BuildRoot 'global.json'
 if(Test-Path -LiteralPath $pin){throw 'Reviewed source unexpectedly contains global.json'}
 # Only call after expanding the exact source archive into its disposable build root.
 Write-ReleaseJson ([ordered]@{sdk=[ordered]@{version='10.0.400';rollForward='disable';allowPrerelease=$false}}) $pin
}
function Assert-ReleaseBuildSdk([string[]]$Installed,[string]$Selected) {
 $available=@($Installed | Where-Object {$_ -cmatch '^10\.0\.400\s+\['}).Count -gt 0
 Write-Host ('INSTALLED_SDK_10_0_400='+$(if($available){'YES'}else{'NO'}))
 Write-Host "SELECTED_SDK=$Selected"
 if(-not $available -or $Selected -cne '10.0.400'){throw 'Approved artifact requires SDK 10.0.400'}
}
Export-ModuleMember -Function *-Release*,Assert-SourceGate,Assert-DeployInterlocks,Assert-PreflightRun,Assert-LiveSnapshot
