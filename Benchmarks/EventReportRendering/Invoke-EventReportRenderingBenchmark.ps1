<#
.SYNOPSIS
Measures renderer allocation and digest bounds on deterministic report snapshots.
.EXAMPLE
./Invoke-EventReportRenderingBenchmark.ps1 -RowCount 1000,10000 -OutputRoot ./Ignore/Benchmarks/Rendering
#>
[CmdletBinding()]
param(
    [int[]] $RowCount = @(1000,10000),
    [string] $OutputRoot = (Join-Path $PSScriptRoot '../../Ignore/Benchmarks/EventReportRendering'),
    [ValidateRange(0, 100)] [int] $WarmupCount = 1,
    [ValidateRange(1, 100)] [int] $IterationCount = 3
)
$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'EventReportRendering.BenchmarkFixture.csproj') --configuration Release --verbosity quiet | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'The report-rendering benchmark fixture build failed.' }
Import-Module PSPublishModule -MinimumVersion 3.0.134 -ErrorAction Stop
$result = Invoke-BenchmarkSuite -Path (Join-Path $PSScriptRoot 'event-report-rendering.benchmark.ps1') -OutputRoot $OutputRoot -WarmupCount $WarmupCount -IterationCount $IterationCount -Variable @{
    RowCounts = $RowCount -join ','
    FixturePath = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/EventReportRendering.BenchmarkFixture.dll'
}
if (@($result.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' }).Count -gt 0) { throw 'The report-rendering benchmark contains failed samples.' }
$result
