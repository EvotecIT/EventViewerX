<#
.SYNOPSIS
Compares per-event typed-rule planning with one reusable compiled plan.

.DESCRIPTION
Builds the EventViewerX benchmark fixture and delegates warmup, rotated ordering,
validation, comparison, and artifacts to the PowerForge benchmark engine.

.EXAMPLE
.\Invoke-EventProjectionBenchmark.ps1 -EventCount 1000,10000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [string[]] $EventCount = @('1000', '10000'),

    [ValidateRange(0, [int]::MaxValue)]
    [int] $WarmupCount = 1,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $IterationCount = 3,

    [string] $OutputRoot,

    [switch] $SkipBuild,

    [switch] $Plan,

    [switch] $UpdateReadme
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$fixtureProject = Join-Path $PSScriptRoot 'EventProjection.BenchmarkFixture.csproj'
$fixtureAssembly = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\EventViewerX.ProjectionBenchmark.dll'
$specPath = Join-Path $PSScriptRoot 'event-projection.benchmark.ps1'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventProjection'
}
[int[]] $resolvedCounts = @($EventCount | ForEach-Object {
        foreach ($token in $_.Split(',')) {
            [int] $value = 0
            if (-not [int]::TryParse($token.Trim(), [ref] $value) -or $value -le 0) {
                throw "EventCount must contain positive 32-bit values. Received '$token'."
            }
            $value
        }
    } | Sort-Object -Unique)

if (-not $SkipBuild.IsPresent) {
    dotnet build $fixtureProject --configuration Release --framework net8.0-windows --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'The event-projection benchmark fixture build failed.'
    }
}

Add-Type -Path $fixtureAssembly -ErrorAction Stop
Import-Module PSPublishModule -MinimumVersion 3.0.76 -Force -ErrorAction Stop
$invoke = @{
    Path = $specPath
    OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    Variable = @{
        EventCounts = $resolvedCounts -join ','
    }
}
if ($Plan.IsPresent) {
    $invoke.Plan = $true
}
$result = Invoke-BenchmarkSuite @invoke
if (-not $Plan.IsPresent -and @($result.Summary | Where-Object {
            $_.FailureCount -gt 0 -or $_.Status -eq 'Failed'
        }).Count -gt 0) {
    throw "Event-projection benchmark run $($result.RunId) contained failed samples."
}
if ($UpdateReadme.IsPresent) {
    if ($Plan.IsPresent) {
        throw 'README evidence cannot be updated from a benchmark plan.'
    }
    if ($IterationCount -lt 3) {
        throw 'README evidence requires at least three rotated iterations.'
    }
    Update-BenchmarkDocument `
        -Path (Join-Path $PSScriptRoot 'README.md') `
        -BlockId 'event-projection-comparison' `
        -ComparisonPath $result.Artifacts['comparison.json'] `
        -Renderer ComparisonTable `
        -Confirm:$false | Out-Null
    Update-BenchmarkDocument `
        -Path (Join-Path $PSScriptRoot 'README.md') `
        -BlockId 'event-projection-summary' `
        -SummaryPath $result.Artifacts['summary.json'] `
        -Renderer SummaryTable `
        -Confirm:$false | Out-Null
}
$result
