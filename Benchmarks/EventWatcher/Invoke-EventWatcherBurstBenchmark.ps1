<#
.SYNOPSIS
Measures lossless EventViewerX persistent-watcher burst delivery.

.DESCRIPTION
Builds EventViewerX and its portable host, creates a disposable native Windows
Event Log for each sample, waits for an atomic readiness signal, writes the
requested burst, and verifies exact JSONL and completion-summary accounting.

.EXAMPLE
.\Invoke-EventWatcherBurstBenchmark.ps1 -BurstCount 100,1000,10000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [ValidateRange(1, [int]::MaxValue)]
    [int[]] $BurstCount = @(100, 1000, 10000),

    [ValidateRange(0, 100)]
    [int] $WarmupCount = 0,

    [ValidateRange(1, 100)]
    [int] $IterationCount = 3,

    [string] $OutputRoot,

    [switch] $Plan,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repositoryRoot 'Sources\EventViewerX.Cli\EventViewerX.Cli.csproj'
$cliPath = Join-Path $repositoryRoot 'Sources\EventViewerX.Cli\bin\Release\net10.0-windows\evx.exe'
$corePath = Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll'
$specPath = Join-Path $PSScriptRoot 'event-watcher-burst.benchmark.ps1'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventWatcher'
}

if (-not $SkipBuild.IsPresent) {
    dotnet build $projectPath --configuration Release --framework net10.0-windows
    if ($LASTEXITCODE -ne 0) {
        throw 'The EventViewerX CLI Release build failed before the watcher benchmark.'
    }
}
if (-not $Plan.IsPresent) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'The watcher burst benchmark requires an elevated Windows session to create and remove disposable event logs.'
    }
}

Import-Module PSPublishModule -MinimumVersion 3.0.76 -Force -ErrorAction Stop
$invoke = @{
    Path = $specPath
    OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    RunMode = 'local'
    Variable = @{
        EventViewerXCliPath = $cliPath
        EventViewerXPath = $corePath
        BurstCounts = [string] (($BurstCount | Sort-Object -Unique) -join ',')
    }
}
if ($Plan.IsPresent) {
    $invoke.Plan = $true
}
$result = Invoke-BenchmarkSuite @invoke
if (-not $Plan.IsPresent) {
    $failed = @($result.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' })
    if ($failed.Count -gt 0) {
        throw "Watcher burst benchmark run $($result.RunId) contained failed samples."
    }
}
$result
