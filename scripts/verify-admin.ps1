# Run from any directory with an absolute -FrontendRoot path.
# -SkipBuild reuses the frontend production build; the backend runner still builds Release.
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$FrontendRoot,
    [switch]$SkipBuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PlaywrightArguments
)

$ErrorActionPreference = 'Stop'
$isFullyQualified = try {
    [IO.Path]::IsPathFullyQualified($FrontendRoot)
} catch {
    [IO.Path]::IsPathRooted($FrontendRoot) -and ([IO.Path]::GetPathRoot($FrontendRoot).Length -gt 1)
}
if (-not $isFullyQualified) {
    throw '-FrontendRoot must be an absolute path to the Frontend checkout.'
}
$resolvedFrontend = (Resolve-Path -LiteralPath $FrontendRoot).ProviderPath
$frontendRunner = Join-Path $resolvedFrontend 'run.ps1'
if (-not (Test-Path -LiteralPath $frontendRunner -PathType Leaf)) {
    throw 'The selected Frontend checkout does not contain run.ps1.'
}
$previousBackendRoot = $env:BOILERATE_BACKEND_ROOT
$argumentsForRunner = @('test:e2e:live')
if ($SkipBuild) { $argumentsForRunner += '--skip-build' }
$argumentsForRunner += $PlaywrightArguments
try {
    $env:BOILERATE_BACKEND_ROOT = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    # Invocation uses an argument array, so paths and filters remain literal values.
    & $frontendRunner @argumentsForRunner
    $verificationExitCode = $LASTEXITCODE
} finally {
    $env:BOILERATE_BACKEND_ROOT = $previousBackendRoot
}
exit $verificationExitCode
