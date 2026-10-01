param(
    [Parameter(Mandatory = $true)][string]$SqlPath,
    [Parameter(Mandatory = $true)][string]$ManifestPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Text inspection only. This validator never connects to a database or executes SQL.
$packageStream = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $SqlPath).Path)
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $packageHash = [System.BitConverter]::ToString($sha256.ComputeHash($packageStream)).Replace('-', '')
} finally {
    $packageStream.Dispose()
    $sha256.Dispose()
}
if ($packageHash -eq
    '4101718F6B550480A9CA579015B8143EDAE23FF78F7C4165CF17F755432364BA') {
    throw 'Blocked Release v2 package: invalid direct CREATE TRIGGER; must not execute'
}
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$expected = @(
    '20260906033000_AddCompetitionActivityModuleCode',
    '20260910103000_StackerIdentityPersistenceV1',
    '20260914093000_FinalsRankingGovernanceSp4g'
)
if ($manifest.databaseMigrationFrom -cne '20260826074651_AddAccountSessionVersion' -or
    $manifest.databaseMigrationTo -cne $expected[-1] -or
    (@($manifest.databaseMigrations) -join "`n") -cne ($expected -join "`n")) {
    throw 'Unexpected governed Release v2 migration range or list'
}
$sql = Get-Content -LiteralPath $SqlPath -Raw
$actual = @([regex]::Matches($sql, '\b[0-9]{14}_[A-Za-z][A-Za-z0-9_]*\b') |
    ForEach-Object { $_.Value } | Sort-Object -Unique)
if (($actual -join "`n") -cne ($expected -join "`n")) {
    throw "Migration package IDs differ from the governed set: $($actual -join ', ')"
}
foreach ($migration in $expected) {
    $escaped = [regex]::Escape($migration)
    if ($sql -cnotmatch "WHERE\s+\[MigrationId\]\s*=\s*N'$escaped'" -or
        $sql -cnotmatch "(?s)INSERT INTO \[__EFMigrationsHistory\].*?VALUES\s*\(N'$escaped'") {
        throw "Missing idempotent migration guard or history insert: $migration"
    }
}
'MIGRATION_PACKAGE_SCOPE=PASS'
'GOVERNED_MIGRATION_COUNT=3'
