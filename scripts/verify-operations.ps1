#requires -Version 7.2
<#
.SYNOPSIS
Runs one explicit operations acceptance stage and records its actual outcome.
.EXAMPLE
./scripts/verify-operations.ps1 -Stage Local
.EXAMPLE
./scripts/verify-operations.ps1 -Stage Recovery
.EXAMPLE
./scripts/verify-operations.ps1 -Stage Container -ContainerImage boilerate-ci -SkipBuild
#>
param(
    [ValidateSet('Local', 'Recovery', 'Container')][string]$Stage = 'Local',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$ContainerImage = $env:BOILERATE_CONTAINER_IMAGE,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$runRoot = Join-Path $taskRoot ('artifacts/operations/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$summaryPath = Join-Path $runRoot 'summary.json'
$head = & git -C $taskRoot rev-parse HEAD 2>$null
if ($LASTEXITCODE) { $head = $null }
$sourceChanges = & git -C $taskRoot status --porcelain=v1 2>$null
$summary = [ordered]@{
    stage = $Stage; configuration = $Configuration; sourceCommit = $head; sourceDirty = [bool]$sourceChanges
    startedAtUtc = [DateTime]::UtcNow.ToString('o'); completedAtUtc = $null
    status = 'running'; reason = $null; passed = 0; failed = 0; skipped = 0; total = 0
    hostOS = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    buildRequested = !$SkipBuild; logs = @()
}
$previousRecovery = $env:BOILERATE_TEST_RECOVERY
$previousImage = $env:BOILERATE_CONTAINER_IMAGE
$exitCode = 1

function Stop-Blocked([string]$Reason) {
    $summary.status = 'blocked'
    $summary.reason = $Reason
    throw [InvalidOperationException]::new($Reason)
}

function Invoke-Dotnet([string[]]$Arguments, [string]$LogName) {
    $summary.logs += $LogName
    & dotnet @Arguments *> (Join-Path $runRoot $LogName)
    if ($LASTEXITCODE) {
        $summary.reason = "dotnet command failed; inspect $LogName locally."
        throw [InvalidOperationException]::new($summary.reason)
    }
}

Push-Location $taskRoot
try {
    if ($Stage -in @('Recovery', 'Container')) {
        if ([string]::IsNullOrWhiteSpace($env:BOILERATE_TEST_POSTGRES)) {
            Stop-Blocked 'Set BOILERATE_TEST_POSTGRES to an isolated PostgreSQL test server.'
        }
        if (!$IsWindows -and [string]::IsNullOrWhiteSpace($env:BOILERATE_TEST_MSSQL)) {
            Stop-Blocked 'Set BOILERATE_TEST_MSSQL to an isolated SQL Server test server.'
        }
    }
    if ($Stage -eq 'Container') {
        if (!$IsLinux) { Stop-Blocked 'The current container gate requires a Linux host; run it in Linux CI.' }
        if ([string]::IsNullOrWhiteSpace($ContainerImage)) { Stop-Blocked 'Build the production image and supply -ContainerImage.' }
        $probe = [Diagnostics.ProcessStartInfo]::new('docker')
        $probe.UseShellExecute = $false
        $probe.CreateNoWindow = $true
        $probe.RedirectStandardOutput = $true
        $probe.RedirectStandardError = $true
        foreach ($argument in @('info', '--format', '{{.OSType}}')) { $probe.ArgumentList.Add($argument) }
        try { $process = [Diagnostics.Process]::Start($probe) }
        catch { Stop-Blocked 'Docker CLI is unavailable.' }
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (!$process.WaitForExit(15000)) {
                $process.Kill($true)
                Stop-Blocked 'Docker engine did not respond within 15 seconds.'
            }
            $engine = $stdout.GetAwaiter().GetResult().Trim()
            $null = $stderr.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0 -or $engine -ne 'linux') { Stop-Blocked 'A running Linux Docker engine is required.' }
        } finally { $process.Dispose() }
        $env:BOILERATE_CONTAINER_IMAGE = $ContainerImage
    }
    if ($Stage -eq 'Recovery') { $env:BOILERATE_TEST_RECOVERY = '1' }
    if (!$SkipBuild) {
        Invoke-Dotnet -Arguments @('restore', 'Boilerate.sln', '--locked-mode') -LogName 'restore.log'
        Invoke-Dotnet -Arguments @('build', 'Boilerate.sln', '-c', $Configuration, '--no-restore') -LogName 'build.log'
    }
    $filter = switch ($Stage) {
        'Local' { 'FullyQualifiedName~DeploymentRuntimeTests' }
        'Recovery' { 'Runtime=Recovery' }
        'Container' { 'Runtime=Container' }
    }
    $trxName = 'operations-' + $Stage.ToLowerInvariant() + '.trx'
    # A failed test must still have its TRX counted, so inspect the exit code after parsing it.
    $summary.logs += 'test.log'
    & dotnet test tests/Boilerate.Tests/Boilerate.Tests.csproj -c $Configuration --no-build --filter $filter --results-directory $runRoot --logger "trx;LogFileName=$trxName" *> (Join-Path $runRoot 'test.log')
    $testExit = $LASTEXITCODE
    $trxPath = Join-Path $runRoot $trxName
    if (!(Test-Path -LiteralPath $trxPath -PathType Leaf)) { throw 'Test execution produced no TRX evidence.' }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counts = $trx.GetElementsByTagName('Counters') | Select-Object -First 1
    if ($null -eq $counts) { throw 'TRX has no test counters.' }
    $summary.total = [int]$counts.total
    $summary.passed = [int]$counts.passed
    $summary.failed = [int]$counts.failed
    $summary.skipped = $summary.total - $summary.passed - $summary.failed
    $expected = if ($Stage -eq 'Local') { 1 } else { 2 }
    if ($testExit -ne 0 -or $summary.failed -ne 0 -or $summary.skipped -ne 0 -or $summary.passed -lt $expected) {
        throw 'Acceptance requires executed passing cases, no failures/skips, and coverage of both providers for Recovery/Container.'
    }
    if ($Stage -ne 'Local') {
        $results = @($trx.GetElementsByTagName('UnitTestResult'))
        foreach ($provider in @('mssql', 'postgresql')) {
            if (!($results | Where-Object { $_.outcome -eq 'Passed' -and $_.testName -match ('provider: "' + $provider + '"') })) {
                throw 'The TRX must contain a passing case for each configured database provider.'
            }
        }
    }
    $summary.status = 'passed'
    $exitCode = 0
} catch {
    if ($summary.status -eq 'running') { $summary.status = 'failed' }
    if (!$summary.reason) { $summary.reason = $_.Exception.Message }
    # Only print the safe stage outcome; credentials and process output remain out of the summary.
    Write-Host "Operations $Stage`: $($summary.status). Inspect local evidence for details."
} finally {
    $env:BOILERATE_TEST_RECOVERY = $previousRecovery
    $env:BOILERATE_CONTAINER_IMAGE = $previousImage
    $summary.completedAtUtc = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath
    Pop-Location
    Write-Host "Evidence: $runRoot"
}
exit $exitCode
