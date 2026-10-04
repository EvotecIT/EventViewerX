<#
.SYNOPSIS
Measures Unicode checkpoint lookup and store initialization through PowerForge.
.EXAMPLE
.\Invoke-EventCheckpointBenchmark.ps1 -CheckpointCount 100,1000,10000
#>
[CmdletBinding()]
param(
    [int[]] $CheckpointCount = @(100, 1000, 10000),
    [ValidateRange(0, [int]::MaxValue)] [int] $WarmupCount = 1,
    [ValidateRange(1, [int]::MaxValue)] [int] $IterationCount = 3,
    [string] $OutputRoot,
    [switch] $SkipBuild,
    [switch] $Plan
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot '..\..\Ignore\Benchmarks\EventCheckpoints'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $SkipBuild.IsPresent) {
    dotnet build (Join-Path $PSScriptRoot 'EventCheckpoints.BenchmarkFixture.csproj') --configuration Release --verbosity quiet | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'The checkpoint benchmark fixture build failed.' }
}
$runtimeRoot = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows'
$nativeArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
[System.Runtime.InteropServices.NativeLibrary]::Load((Join-Path $runtimeRoot "runtimes\win-$nativeArchitecture\native\e_sqlite3.dll")) | Out-Null
Add-Type -Path (Join-Path $runtimeRoot 'EventViewerX.CheckpointBenchmark.dll')
Import-Module PSPublishModule -MinimumVersion 3.0.134 -Force -ErrorAction Stop
$invoke = @{
    Path = Join-Path $PSScriptRoot 'event-checkpoints.benchmark.ps1'
    OutputRoot = $OutputRoot
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    Plan = $Plan.IsPresent
    Variable = @{ CheckpointCounts = $CheckpointCount -join ','; WorkRoot = Join-Path $OutputRoot 'work' }
}
$result = Invoke-BenchmarkSuite @invoke
if (-not $Plan.IsPresent -and @($result.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' }).Count -gt 0) {
    throw "Checkpoint benchmark run $($result.RunId) contained failed samples."
}
$result
