$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot '../scripts/deployment/NadiTrackApplicationRelease.psm1') -Force
$approved=Read-ReleaseJson (Join-Path $PSScriptRoot '../scripts/deployment/career-profile-admin-ui.approved.json')
$testRoot=Join-Path (Join-Path $PSScriptRoot '../outputs') ('application-release-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$count=0
function Check([string]$Name,[scriptblock]$Body){& $Body;$script:count++;Write-Host "PASS $Name"}
function Reject([scriptblock]$Body,[string]$Pattern='.'){
 $caught=$false
 try{& $Body | Out-Null}catch{if($_.Exception.Message -notmatch $Pattern){throw "Unexpected rejection: $($_.Exception.Message)"};$caught=$true}
 if(-not $caught){throw 'Expected rejection but operation succeeded'}
}
function Clone($Object){$Object|ConvertTo-Json -Depth 15|ConvertFrom-Json}
function Fixture {
 $dir=Join-Path $testRoot ([Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $dir | Out-Null
 foreach($name in 'payload','baseline'){New-Item -ItemType Directory -Path (Join-Path $dir $name) | Out-Null}
 $live=$dir+'-live';New-Item -ItemType Directory -Path $live | Out-Null
 $p=Clone $approved
 foreach($f in $p.files){
  $target=Join-Path (Join-Path $dir 'payload') $f.name;Set-Content -LiteralPath $target -Value "new $($f.name)" -Encoding utf8
  $f.sha256=Get-ReleaseHash $target;$f.bytes=(Get-Item -LiteralPath $target).Length
  if($f.name -cne 'career-admin.js'){
   $old=Join-Path (Join-Path $dir 'baseline') $f.name;Set-Content -LiteralPath $old -Value "old $($f.name)" -Encoding utf8
   Copy-Item -LiteralPath $old -Destination (Join-Path $live $f.name)
  }
 }
 $p.baselineDllSha256=Get-ReleaseHash (Join-Path (Join-Path $dir 'baseline') 'StackMeet.Api.dll')
 $r=[pscustomobject]@{files=@($p.files|ForEach-Object{
  $old=Join-Path (Join-Path $dir 'baseline') $_.name
  if(Test-Path -LiteralPath $old){[pscustomobject]@{name=$_.name;existed=$true;sha256=(Get-ReleaseHash $old);bytes=(Get-Item -LiteralPath $old).Length}}else{[pscustomobject]@{name=$_.name;existed=$false;sha256=$null;bytes=0}}
 });protectedConfiguration=[pscustomobject]@{'web.config'=('A'*64);'appsettings.json'=('B'*64);'appsettings.Production.json'=('C'*64)}}
 Write-ReleaseJson $r (Join-Path $dir 'rollback-manifest.json')
 $m=[pscustomobject]@{sourceSha=$p.sourceSha;files=$p.files;rollbackManifestSha256=(Get-ReleaseHash (Join-Path $dir 'rollback-manifest.json'));schemaMigration=$false;productionWrites=0;operation='preflight';preflightRunId='123';workflowSha=('d'*40)}
 Write-ReleaseJson $m (Join-Path $dir 'manifest.json')
 return @{dir=$dir;live=$live;policy=$p;rollback=$r;manifest=$m}
}
function PackageHash($f){Get-ReleaseHash (Join-Path $f.dir 'manifest.json')}
function Snapshot($f){
 [pscustomobject]@{files=@($f.policy.files|ForEach-Object{
  $path=Join-Path $f.live $_.name
  if(Test-Path -LiteralPath $path){[pscustomobject]@{name=$_.name;existed=$true;sha256=(Get-ReleaseHash $path);bytes=(Get-Item -LiteralPath $path).Length}}else{[pscustomobject]@{name=$_.name;existed=$false;sha256=$null;bytes=0}}
 });protectedConfiguration=$f.rollback.protectedConfiguration}
}
function Transaction($f,$state){
 $upload={param($name,$local)
  $state.calls.Add($name);$state.writes++
  $destination=Join-Path $f.live $name
  if($state.writes -eq $state.failAt){Set-Content -LiteralPath $destination 'partial';throw 'simulated partial STOR'}
  Copy-Item -LiteralPath $local -Destination $destination
  if($state.writes -eq $state.corruptAt){Set-Content -LiteralPath $destination 'corrupt'}
  if($state.writes -eq $state.rollbackFailAt){throw 'simulated rollback transport failure'}
 }.GetNewClosure()
 $delete={param($name)$state.deletes.Add($name);$path=Join-Path $f.live $name;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}.GetNewClosure()
 # Capture the function body explicitly: GitHub dot-sources the runner script.
 $snapshotFunction=${function:Snapshot}
 $snapshot={& $snapshotFunction $f}.GetNewClosure()
 $before={$state.gates++;if($state.drift){throw 'master drift'}}.GetNewClosure()
 Invoke-ReleaseTransaction $f.dir $f.policy $upload $delete $snapshot $before
}
function State { @{writes=0;calls=[Collections.Generic.List[string]]::new();deletes=[Collections.Generic.List[string]]::new();failAt=-1;corruptAt=-1;rollbackFailAt=-1;gates=0;drift=$false} }
Check 'approved policy has exactly five safe destinations' {Assert-ReleasePolicy $approved}
Check 'exact valid bundle accepted' {$f=Fixture;$null=Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)}
foreach($name in 'web.config','appsettings.json','appsettings.Production.json','secret.txt','database.mdf','upload.png','debug.pdb','source.map'){
 Check "unexpected payload rejected: $name" {$f=Fixture;Set-Content (Join-Path (Join-Path $f.dir 'payload') $name) 'forbidden';Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'allow-list'}
}
Check 'unexpected root artifacts rejected' {$f=Fixture;Set-Content (Join-Path $f.dir 'unreviewed.json') '{}';Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'allow-list'}
Check 'missing payload rejected' {$f=Fixture;Remove-Item (Join-Path (Join-Path $f.dir 'payload') 'admin.js');Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)}}
Check 'manifest hash mismatch rejected' {$f=Fixture;Reject {Assert-ReleaseBundle $f.dir $f.policy ('0'*64)} 'manifest mismatch'}
Check 'payload hash mismatch rejected' {$f=Fixture;Add-Content (Join-Path (Join-Path $f.dir 'payload') 'admin.js') 'changed';Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'hash/size'}
Check 'wrong source SHA rejected' {$p=Clone $approved;$p.sourceSha=('f'*40);Reject {Assert-ReleasePolicy $p} 'source SHA'}
Check 'manifest source mismatch rejected' {$f=Fixture;$f.manifest.sourceSha=('e'*40);Write-ReleaseJson $f.manifest (Join-Path $f.dir 'manifest.json');Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'Wrong source'}
Check 'backup incomplete rejected' {$f=Fixture;Remove-Item (Join-Path (Join-Path $f.dir 'baseline') 'admin.css');Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'allow-list'}
Check 'backup corruption rejected' {$f=Fixture;Add-Content (Join-Path (Join-Path $f.dir 'baseline') 'StackMeet.Api.dll') 'changed';Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'hash/size'}
Check 'rollback manifest mismatch rejected' {$f=Fixture;Add-Content (Join-Path $f.dir 'rollback-manifest.json') ' ';Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'Rollback manifest'}
Check 'unmerged and unpushed source rejected' {
 $c=[pscustomobject]@{repository='casseylim/StackMeet';ref='refs/heads/master';protected=$true;remoteSource=$approved.sourceSha;relationship='ahead';masterSha=('d'*40);workflowSha=('d'*40)}
 Assert-SourceGate $approved $c
 foreach($bad in 'behind','diverged'){$c.relationship=$bad;Reject {Assert-SourceGate $approved $c} 'reachability'}
 $c.relationship='ahead';$c.remoteSource=$null;Reject {Assert-SourceGate $approved $c} 'reachability'
 $c.remoteSource=$approved.sourceSha;$c.protected=$false;Reject {Assert-SourceGate $approved $c}
}
Check 'master drift rejected' {$c=[pscustomobject]@{repository='casseylim/StackMeet';ref='refs/heads/master';protected=$true;remoteSource=$approved.sourceSha;relationship='ahead';masterSha=('a'*40);workflowSha=('d'*40)};Reject {Assert-SourceGate $approved $c}}
Check 'missing preflight and invalid confirmations rejected' {
 Assert-DeployInterlocks '123' ('A'*64) 'SITE-STOPPED-NADITRACK-ONLY' 'DEPLOY-CAREER-PROFILE-ADMIN-UI'
 Reject {Assert-DeployInterlocks '' ('A'*64) 'SITE-STOPPED-NADITRACK-ONLY' 'DEPLOY-CAREER-PROFILE-ADMIN-UI'}
 Reject {Assert-DeployInterlocks '123' ('A'*64) 'POOL-STOPPED' 'DEPLOY-CAREER-PROFILE-ADMIN-UI'}
 Reject {Assert-DeployInterlocks '123' ('A'*64) 'SITE-STOPPED-NADITRACK-ONLY' 'NOT-CONFIRMED'}
}
Check 'wrong workflow, unsuccessful/skipped preflight, wrong head/run rejected' {
 $f=Fixture;$r=[pscustomobject]@{id=123;status='completed';conclusion='success';event='workflow_dispatch';head_branch='master';head_sha=('d'*40);path='.github/workflows/deploy-career-profile-admin-ui.yml'}
 $j=[pscustomobject]@{name='preflight';conclusion='success'}
 Assert-PreflightRun $r $j $f.manifest '123' ('d'*40)
 $j.conclusion='skipped';Reject {Assert-PreflightRun $r $j $f.manifest '123' ('d'*40)};$j.conclusion='success'
 $r.path='.github/workflows/deploy-career-profile-remediation.yml';Reject {Assert-PreflightRun $r $j $f.manifest '123' ('d'*40)}
 $r.path='.github/workflows/deploy-career-profile-admin-ui.yml';$r.conclusion='failure';Reject {Assert-PreflightRun $r $j $f.manifest '123' ('d'*40)}
 $r.conclusion='success';Reject {Assert-PreflightRun $r $j $f.manifest '124' ('d'*40)}
 Reject {Assert-PreflightRun $r $j $f.manifest '123' ('e'*40)}
}
Check 'live baseline drift stops before writes' {$f=Fixture;$s=State;Add-Content (Join-Path $f.live 'admin.js') 'drift';Reject {Transaction $f $s} 'baseline drift';if($s.writes){throw 'Wrote despite drift'}}
Check 'last master check stops before writes' {$f=Fixture;$s=State;$s.drift=$true;Reject {Transaction $f $s} 'master drift';if($s.writes){throw 'Wrote despite drift'}}
Check 'successful five-file upload has DLL last' {$f=Fixture;$s=State;$result=Transaction $f $s;if($result -cne 'PASS' -or $s.writes -ne 5 -or $s.calls[4] -cne 'StackMeet.Api.dll'){throw 'Incorrect write set'}}
foreach($index in 1..5){
 Check "partial upload $index restores complete prior release including absent module" {
  $f=Fixture;$s=State;$s.failAt=$index;Reject {Transaction $f $s} 'ROLLED_BACK'
  Assert-LiveSnapshot $f.rollback (Snapshot $f)
  if($s.deletes.Count -ne 1 -or $s.calls[$s.calls.Count-1] -cne 'StackMeet.Api.dll'){throw 'Incomplete rollback'}
 }
 Check "upload checksum failure $index restores complete prior release" {$f=Fixture;$s=State;$s.corruptAt=$index;Reject {Transaction $f $s} 'ROLLED_BACK';Assert-LiveSnapshot $f.rollback (Snapshot $f)}
}
Check 'rollback continues restoring other members after transport failure and fails closed' {$f=Fixture;$s=State;$s.failAt=3;$s.rollbackFailAt=4;Reject {Transaction $f $s} 'CRITICAL: rollback unverified';if($s.calls[$s.calls.Count-1] -cne 'StackMeet.Api.dll' -or $s.deletes.Count -ne 1){throw 'Rollback stopped early'}}
Check 'configuration drift rejected' {$f=Fixture;$actual=Snapshot $f;$actual.protectedConfiguration=Clone $f.rollback.protectedConfiguration;$actual.protectedConfiguration.'web.config'=('D'*64);Reject {Assert-LiveSnapshot $f.rollback $actual} 'configuration drift'}
Check 'all five pre-existing files restored together on failure' {
 $f=Fixture;$s=State;$s.failAt=5
 $old=Join-Path (Join-Path $f.dir 'baseline') 'career-admin.js';Set-Content -LiteralPath $old 'prior module'
 Copy-Item -LiteralPath $old -Destination (Join-Path $f.live 'career-admin.js')
 $record=@($f.rollback.files|Where-Object name -CEQ 'career-admin.js')[0]
 $record.existed=$true;$record.sha256=Get-ReleaseHash $old;$record.bytes=(Get-Item -LiteralPath $old).Length
 Write-ReleaseJson $f.rollback (Join-Path $f.dir 'rollback-manifest.json')
 $f.manifest.rollbackManifestSha256=Get-ReleaseHash (Join-Path $f.dir 'rollback-manifest.json')
 Write-ReleaseJson $f.manifest (Join-Path $f.dir 'manifest.json')
 $null=Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)
 Reject {Transaction $f $s} 'ROLLED_BACK';Assert-LiveSnapshot $f.rollback (Snapshot $f)
 if($s.deletes.Count -ne 0 -or $s.writes -ne 10){throw 'Existing module not restored as part of whole set'}
}
Check 'DLL-first ordering rejected' {$p=Clone $approved;$p.files=@($p.files[4]) + @($p.files[0..3]);Reject {Assert-ReleasePolicy $p} 'DLL-last'}
Check 'missing rollback record rejected even if rollback hash updated' {
 $f=Fixture;$f.rollback.files=@($f.rollback.files | Where-Object name -CNE 'admin.js')
 Write-ReleaseJson $f.rollback (Join-Path $f.dir 'rollback-manifest.json');$f.manifest.rollbackManifestSha256=Get-ReleaseHash (Join-Path $f.dir 'rollback-manifest.json')
 Write-ReleaseJson $f.manifest (Join-Path $f.dir 'manifest.json');Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'allow-list'
}
Check 'case aliases or duplicate destination names rejected before FTP writes' {
 Assert-ReleaseRootNames @('admin.html','admin.js','admin.css','StackMeet.Api.dll','web.config','appsettings.json') $approved
 Reject {Assert-ReleaseRootNames @('Career-admin.js') $approved} 'filename case'
 Reject {Assert-ReleaseRootNames @('admin.js','ADMIN.JS') $approved} 'duplicate'
 Reject {Assert-ReleaseRootNames @('../web.config') $approved} 'listing'
}
Check 'destination traversal in policy rejected' {$p=Clone $approved;$p.files[0].productionPath='/../admin.html';Reject {Assert-ReleasePolicy $p} 'destination'}
Check 'actual entry-point FTP snapshot captures backups, missing module and hash-only config' {
 $policy=Clone $approved
 $scratch=Join-Path $testRoot 'wrapper-scratch';New-Item -ItemType Directory -Path $scratch | Out-Null
 $fake=@{'admin.html'='old html';'admin.js'='old js';'admin.css'='old css';'StackMeet.Api.dll'='old dll';'web.config'='private-config-content';'appsettings.json'='private-settings-content';'appsettings.Production.json'='private-production-settings'}
 $wrapper=Join-Path $PSScriptRoot '../scripts/deployment/Invoke-NadiTrackApplicationRelease.ps1'
 $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($wrapper,[ref]$tokens,[ref]$errors)
 foreach($name in 'Read-RootNames','Read-LiveSnapshot'){
  $fn=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
  . ([scriptblock]::Create($fn.Extent.Text))
 }
 function FtpUrl([string]$Name){'ftp://fake/'+$Name}
 function Invoke-Ftp([string[]]$Arguments){
  $out=$Arguments[[Array]::IndexOf($Arguments,'--output')+1]
  if('--list-only' -cin $Arguments){$fake.Keys|Set-Content -LiteralPath $out;return}
  if('--upload-file' -cin $Arguments -or '--quote' -cin $Arguments){throw 'Unexpected FTP write'}
  $name=([Uri]$Arguments[-1]).AbsolutePath.TrimStart('/')
  if($name -ceq $script:fakeFailName){throw 'simulated RETR error'}
  if(-not $fake.ContainsKey($name)){throw 'simulated unavailable file'}
  Set-Content -LiteralPath $out -Value $fake[$name]
 }
 $script:fakeFailName=''
 $snapshot=Read-LiveSnapshot (Join-Path $testRoot 'wrapper-backup')
 if(@($snapshot.files).Count -ne 5 -or @($snapshot.files|Where-Object existed).Count -ne 4){throw 'Incomplete logical backup'}
 if(@($snapshot.protectedConfiguration.PSObject.Properties).Count -ne 3){throw 'Config variants omitted'}
 if(Test-Path -LiteralPath (Join-Path $scratch 'web.config')){throw 'Configuration contents retained'}
 if(Test-Path -LiteralPath (Join-Path $scratch 'appsettings.json')){throw 'Configuration contents retained'}
 $fake['career-admin.js']='existing module';$script:fakeFailName='career-admin.js'
 Reject {Read-LiveSnapshot (Join-Path $testRoot 'wrapper-retr-error')} 'simulated RETR error'
 $script:fakeFailName='';$snapshot=Read-LiveSnapshot (Join-Path $testRoot 'wrapper-five-backup')
 if(@($snapshot.files|Where-Object existed).Count -ne 5){throw 'Existing fifth file not backed up'}
}
Check 'approved installed and selected SDK passes' {Assert-ReleaseBuildSdk @('10.0.400 [sdk]') '10.0.400'}
Check 'multiple installed SDKs accept only approved selection' {Assert-ReleaseBuildSdk @('10.0.400 [sdk]','11.0.100-preview.1 [sdk]') '10.0.400'}
Check 'missing approved SDK fails closed' {Reject {Assert-ReleaseBuildSdk @('11.0.100 [sdk]') '10.0.400'} 'requires SDK'}
Check 'newer selected SDK fails closed' {Reject {Assert-ReleaseBuildSdk @('10.0.400 [sdk]','11.0.100 [sdk]') '11.0.100'} 'requires SDK'}
Check 'temporary pin specifies exact nonrolling stable SDK' {
 $script:build=Join-Path $testRoot 'disposable-build';New-Item -ItemType Directory $build | Out-Null
 Initialize-ReleaseBuildSdk $build
 $pin=Read-ReleaseJson (Join-Path $build 'global.json')
 if($pin.sdk.version -cne '10.0.400' -or $pin.sdk.rollForward -cne 'disable' -or $pin.sdk.allowPrerelease -ne $false){throw 'Incorrect SDK pin'}
}
Check 'existing source SDK pin is never overwritten' {
 $path=Join-Path $build 'global.json';$before=Get-ReleaseHash $path
 Reject {Initialize-ReleaseBuildSdk $build} 'unexpectedly contains'
 if((Get-ReleaseHash $path) -cne $before){throw 'Source pin overwritten'}
}
Check 'real dotnet host honors disposable pin or fails when SDK unavailable' {
 $installed=@(& dotnet --list-sdks);if($LASTEXITCODE){throw 'Cannot inspect test SDK inventory'}
 $available=@($installed | Where-Object {$_ -cmatch '^10\.0\.400\s+\['}).Count -gt 0
 Push-Location $build
 try {
  $selected=@(& dotnet --version 2>$null);$code=$LASTEXITCODE
  if($available){
   if($code -ne 0 -or ($selected -join '').Trim() -cne '10.0.400'){throw 'Real host ignored exact SDK pin'}
  }elseif($code -eq 0){throw 'Real host rolled forward despite unavailable exact SDK'}
  # The expected rejection was asserted. GitHub's dot-sourced wrapper exits with
  # LASTEXITCODE, so do not leak that deliberately nonzero native result.
  $global:LASTEXITCODE=0
 }finally{Pop-Location}
}
Check 'global.json cannot enter production payload' {
 $f=Fixture;Copy-Item (Join-Path $build 'global.json') (Join-Path $f.dir 'payload/global.json')
 Reject {Assert-ReleaseBundle $f.dir $f.policy (PackageHash $f)} 'allow-list'
}
Check 'pin changes only disposable archive, not original source' {
 $original=Join-Path $testRoot 'original-source';$copy=Join-Path $testRoot 'expanded-source'
 New-Item -ItemType Directory $original,$copy | Out-Null
 Set-Content (Join-Path $original 'source.txt') 'reviewed source'
 Copy-Item (Join-Path $original 'source.txt') $copy
 $before=Get-ReleaseHash (Join-Path $original 'source.txt')
 Initialize-ReleaseBuildSdk $copy
 if((Test-Path (Join-Path $original 'global.json')) -or (Get-ReleaseHash (Join-Path $original 'source.txt')) -cne $before){throw 'Original source changed'}
 if((Get-ReleaseHash (Join-Path $copy 'source.txt')) -cne $before){throw 'Expanded source changed'}
}
Check 'hash/size mismatch prints expected and actual diagnostics and still rejects' {
 $f=Fixture;$target=Join-Path $f.dir 'payload/StackMeet.Api.dll';Set-Content $target 'different bytes'
 $expected=@($f.policy.files|Where-Object name -CEQ 'StackMeet.Api.dll')[0]
 $actualHash=Get-ReleaseHash $target;$actualBytes=(Get-Item $target).Length
 $diagnostics=@(& {try{Assert-ReleaseDirectory (Join-Path $f.dir 'payload') $f.policy.files;throw 'Mismatch accepted'}catch{if($_.Exception.Message -notmatch '^File hash/size mismatch: StackMeet.Api.dll$'){throw}}} 6>&1)|Out-String
 foreach($value in "EXPECTED_SHA256=$($expected.sha256)","ACTUAL_SHA256=$actualHash","EXPECTED_BYTES=$($expected.bytes)","ACTUAL_BYTES=$actualBytes"){
  if(-not $diagnostics.Contains($value)){throw "Missing diagnostic: $value"}
 }
}
[ordered]@{status='PASS';tests=$count;productionConnections=0;productionWrites=0;directory=$testRoot}|ConvertTo-Json|Set-Content (Join-Path $testRoot 'results.json')
Write-Host "DEPLOYMENT_SCRIPT_TESTS=PASS ($count cases); PRODUCTION_WRITES=0"
