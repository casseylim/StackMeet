[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallDirectory)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseToolchain.psm1') -Force
$InstallDirectory=[IO.Path]::GetFullPath($InstallDirectory)
if(Test-Path -LiteralPath $InstallDirectory){throw 'Release toolchain installation requires a fresh disposable directory'}
New-Item -ItemType Directory -Path $InstallDirectory|Out-Null
$installer=Join-Path $InstallDirectory 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -TimeoutSec 120
# Official non-admin ZIP installation. Preserve the SDK's host when adding test runtimes.
& $installer -Version 10.0.400 -Architecture x64 -InstallDir $InstallDirectory -NoPath
& $installer -Runtime dotnet -Version 10.0.11 -Architecture x64 -InstallDir $InstallDirectory -NoPath -SkipNonVersionedFiles
# The reviewed projects target net8.0; these runtimes execute tests, not Roslyn.
foreach($runtime in 'dotnet','aspnetcore'){
 & $installer -Runtime $runtime -Version 8.0.30 -Architecture x64 -InstallDir $InstallDirectory -NoPath -SkipNonVersionedFiles
}
$exe=Join-Path $InstallDirectory 'dotnet.exe'
Enable-ReleaseToolchain $exe
Assert-ReleaseToolchain $exe
Write-Host "ISOLATED_DOTNET_PATH=$exe"
