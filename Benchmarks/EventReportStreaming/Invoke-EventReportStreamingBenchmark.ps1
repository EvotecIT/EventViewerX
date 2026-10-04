<#
.SYNOPSIS
Compares normalized snapshot and streaming query memory and first-result delivery.
.DESCRIPTION
Delegates warmup, rotated sampling, correctness checks, and artifacts to PowerForge.
.EXAMPLE
.\Invoke-EventReportStreamingBenchmark.ps1 -EventCount 1000,10000,100000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [int[]] $EventCount = @(1000, 10000, 100000),
    [ValidateRange(0, [int]::MaxValue)] [int] $WarmupCount = 1,
    [ValidateRange(1, [int]::MaxValue)] [int] $IterationCount = 3,
    [string] $OutputRoot,
    [switch] $SkipBuild,
    [switch] $Plan
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot '..\..\Ignore\Benchmarks\EventReportStreaming'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (-not $SkipBuild.IsPresent) {
    dotnet build (Join-Path $PSScriptRoot 'EventReportStreaming.BenchmarkFixture.csproj') --configuration Release --verbosity quiet | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'The report-streaming benchmark fixture build failed.' }
}
Add-Type -Path (Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\EventViewerX.ReportStreamingBenchmark.dll')
Import-Module PSPublishModule -MinimumVersion 3.0.134 -Force -ErrorAction Stop
$invoke = @{
    Path = Join-Path $PSScriptRoot 'event-report-streaming.benchmark.ps1'
    OutputRoot = $OutputRoot
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    Plan = $Plan.IsPresent
    Variable = @{ EventCounts = $EventCount -join ','; WorkRoot = Join-Path $OutputRoot 'work' }
}
$result = Invoke-BenchmarkSuite @invoke
if (-not $Plan.IsPresent -and @($result.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' }).Count -gt 0) {
    throw "Report-streaming benchmark run $($result.RunId) contained failed samples."
}
$result
