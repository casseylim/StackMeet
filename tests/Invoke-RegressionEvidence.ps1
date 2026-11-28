[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CoverageExecutable,
    [ValidateSet('All', 'DotNet')][string]$Suite = 'All',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path (Join-Path $outputPath 'results.json')) {
    throw 'Choose a fresh report directory.'
}
$null = New-Item -ItemType Directory -Force $outputPath
$logPath = Join-Path $outputPath 'logs'
$null = New-Item -ItemType Directory -Force $logPath
$records = [Collections.Generic.List[object]]::new()

function Invoke-SuiteCommand([string]$Name, [string]$Kind, [string]$Exe, [string[]]$Arguments) {
    $file = Join-Path $logPath "$Name.log"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $exit = 1
    try {
        & $Exe @Arguments *> $file
        $exit = $LASTEXITCODE
    } catch {
        $_ | Out-File -Append $file
    } finally {
        $records.Add([ordered]@{
            name = $Name
            kind = $Kind
            exitCode = $exit
            durationMs = $timer.ElapsedMilliseconds
            log = "logs/$Name.log"
        })
        Write-Host "$Kind $Name exit=$exit"
    }
    return $exit
}

Push-Location $repo
try {
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE) { throw 'Commit identification failed.' }
    $sdk = (& dotnet --version).Trim()
    $nodeVersion = (& node --version).Trim()
    $projects = @(Get-ChildItem tests -Recurse -Filter *.csproj | Sort-Object FullName)
    if (!$projects.Count) { throw 'No .NET suites found.' }

    $blocked = [Collections.Generic.HashSet[string]]::new()
    if (!$NoBuild) {
        foreach ($project in $projects) {
            $code = Invoke-SuiteCommand "$($project.BaseName)-build" Build dotnet @(
                'build', $project.FullName, '-c', 'Release', "-p:RestoreConfigFile=$(Join-Path $repo 'NuGet.Config')"
            )
            if ($code) { $null = $blocked.Add($project.BaseName) }
        }
    }

    if ($CoverageExecutable) {
        if ($blocked.Count) { throw 'Coverage requires all .NET suite builds to succeed.' }
        $collector = (Resolve-Path $CoverageExecutable).Path
        $pwsh = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
        $env:DOTNET_COVERAGE_TELEMETRY_OPTOUT = '1'
        $coveredOutput = Join-Path $outputPath 'covered'
        $null = Invoke-SuiteCommand 'coverage' Coverage $collector @(
            'collect', '-f', 'cobertura', '-o', (Join-Path $outputPath 'coverage.cobertura.xml'),
            $pwsh, '-NoProfile', '-File', $PSCommandPath, '-OutputDirectory', $coveredOutput, '-Suite', 'DotNet', '-NoBuild'
        )
        if (!(Test-Path (Join-Path $coveredOutput 'results.json')) -or !(Test-Path (Join-Path $outputPath 'coverage.cobertura.xml'))) {
            throw 'Coverage report or covered suite ledger is missing.'
        }
        # Use class-level line entries, not method duplicates or whole-report
        # totals. The package rates are independently checked against counts.
        [xml]$coverageXml = Get-Content (Join-Path $outputPath 'coverage.cobertura.xml') -Raw
        $apiPackages = @($coverageXml.SelectNodes("/coverage/packages/package[@name='StackMeet.Api']"))
        if ($apiPackages.Count -ne 1) { throw 'Expected exactly one StackMeet.Api coverage package.' }
        $apiPackage = $apiPackages[0]
        $lines = @($apiPackage.SelectNodes('classes/class/lines/line'))
        $lineHits = 0
        $branchHits = 0
        $branchTotal = 0
        foreach ($line in $lines) {
            $hits = [long]$line.GetAttribute('hits')
            if ($hits -lt 0 -or !$line.HasAttribute('hits')) { throw 'Invalid API line hit count.' }
            if ($hits -gt 0) { $lineHits++ }
            if ($line.GetAttribute('branch') -ieq 'true') {
                $condition = $line.GetAttribute('condition-coverage')
                if ($condition -notmatch '\((\d+)/(\d+)\)\s*$') { throw 'Invalid API branch counts.' }
                $hit = [long]$Matches[1]
                $total = [long]$Matches[2]
                if ($total -le 0 -or $hit -gt $total) { throw 'Invalid API branch denominator.' }
                $branchHits += $hit
                $branchTotal += $total
            }
        }
        $culture = [Globalization.CultureInfo]::InvariantCulture
        $apiSummary = [ordered]@{
            schemaVersion = 1
            sourceSha = $commit
            package = 'StackMeet.Api'
            countingUnit = 'Cobertura class-level line entries'
            lines = @{ covered = $lineHits; total = $lines.Count; reportedRate = [double]::Parse($apiPackage.GetAttribute('line-rate'), $culture) }
            branches = @{ covered = $branchHits; total = $branchTotal; reportedRate = [double]::Parse($apiPackage.GetAttribute('branch-rate'), $culture) }
        }
        $apiSummary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outputPath 'api-coverage.json') -Encoding utf8NoBOM
        $covered = Get-Content (Join-Path $coveredOutput 'results.json') -Raw | ConvertFrom-Json
        foreach ($record in $covered.suites) {
            $record.log = "covered/$($record.log)"
            $records.Add($record)
        }
    } else {
        foreach ($project in $projects) {
            if (!$blocked.Contains($project.BaseName)) {
                $dll = Join-Path $project.DirectoryName "bin/Release/net8.0/$($project.BaseName).dll"
                $null = Invoke-SuiteCommand $project.BaseName DotNet dotnet @($dll)
            }
        }
    }

    if ($Suite -eq 'All') {
        foreach ($test in Get-ChildItem tests -Filter *.test.js | Sort-Object Name) {
            $null = Invoke-SuiteCommand $test.Name Node node @($test.FullName)
        }
        $null = Invoke-SuiteCommand 'storage-smoke' Node node @('backend/StackMeet.Api/wwwroot/js/storage/storage-smoke.test.js')
        $null = Invoke-SuiteCommand 'release-safety' PowerShell pwsh @('-NoProfile', '-File', 'tests/naditrack-application-release.tests.ps1')
    }
} catch {
    $records.Add([ordered]@{ name = 'runner'; kind = 'Infrastructure'; exitCode = 1; error = $_.Exception.Message })
} finally {
    $report = [ordered]@{
        schemaVersion = 1
        commit = $commit
        sdk = $sdk
        node = $nodeVersion
        os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        suites = @($records.ToArray())
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $outputPath 'results.json') -Encoding utf8NoBOM
    Pop-Location
}
if ($records.Where({ $_.exitCode -ne 0 }).Count) { exit 1 }
