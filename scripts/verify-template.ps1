param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:BOILERATE_TEST_POSTGRES)) {
    throw 'Set BOILERATE_TEST_POSTGRES to an isolated PostgreSQL test server before verification.'
}
if (!$IsWindows -and [string]::IsNullOrWhiteSpace($env:BOILERATE_TEST_MSSQL)) {
    throw 'Set BOILERATE_TEST_MSSQL to an isolated SQL Server test server before verification.'
}
$taskRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $taskRoot ('artifacts/verification/template-' + [guid]::NewGuid().ToString('N'))
$hive = Join-Path $output 'hive'
$generated = Join-Path $output 'RenamedBackend'
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE) { throw "dotnet $($Arguments[0]) failed ($LASTEXITCODE)" }
}
$previousTemplateRoot = $env:BOILERATE_TEMPLATE_ROOT
Push-Location $taskRoot
try {
    Invoke-Dotnet -Arguments @('pack', 'Boilerate.Template.csproj', '-o', (Join-Path $output 'packages'), '--verbosity', 'quiet')
    $package = Join-Path $output 'packages/Boilerate.CleanArchitecture.Template.1.0.0.nupkg'
    Invoke-Dotnet -Arguments @('new', 'install', $package, '--debug:custom-hive', $hive)
    Invoke-Dotnet -Arguments @('new', 'boilerate', '-n', 'RenamedBackend', '-o', $generated, '--debug:custom-hive', $hive)
    Push-Location $generated
    try {
        foreach ($requiredFile in @('Dockerfile', 'compose.yaml', '.dockerignore', 'README.md', 'plan.md', '.editorconfig', 'RenamedBackend.sln')) {
            if (!(Test-Path -LiteralPath (Join-Path $generated $requiredFile) -PathType Leaf)) {
                throw "Generated template is missing the expected root file: $requiredFile"
            }
        }
        Invoke-Dotnet -Arguments @('tool', 'restore')
        Invoke-Dotnet -Arguments @('restore', 'RenamedBackend.sln', '--locked-mode', '--verbosity', 'quiet')
        Invoke-Dotnet -Arguments @('build', 'RenamedBackend.sln', '-c', $Configuration, '--no-restore', '--verbosity', 'quiet')
        $env:BOILERATE_TEMPLATE_ROOT = $generated
        Invoke-Dotnet -Arguments @('test', 'RenamedBackend.sln', '-c', $Configuration, '--no-build', '--logger', 'trx;LogFileName=renamed.trx')
        "Renamed template passed restore/build/test and CLI provider/Catalog matrix." | Set-Content (Join-Path $output 'result.txt')
    } finally { Pop-Location }
} finally {
    $env:BOILERATE_TEMPLATE_ROOT = $previousTemplateRoot
    Pop-Location
}
Write-Host "Verification output: $output"
