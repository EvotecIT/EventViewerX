<#
.SYNOPSIS
Runs repeatable EventViewerX local storage, summary, and typed CSV benchmarks.

.DESCRIPTION
Builds the current module so its architecture-aware SQLite bootstrap is active, then delegates
rotated warmup, iteration, validation, metrics, and artifacts to PowerForge benchmarking.

.EXAMPLE
.\Invoke-EventStoreBenchmark.ps1 -RowCount 1000,10000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [string[]] $RowCount = @('1000', '10000'),

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
$projectPath = Join-Path $repositoryRoot 'Sources\PSEventViewer\PSEventViewer.csproj'
$modulePath = Join-Path $repositoryRoot 'PSEventViewer.psd1'
$specPath = Join-Path $PSScriptRoot 'event-store.benchmark.ps1'
$fixtureProjectPath = Join-Path $PSScriptRoot 'EventStore.BenchmarkFixture.csproj'
$fixtureAssemblyPath = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\EventStore.BenchmarkFixture.dll'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventStore'
}
$resolvedRowCounts = @($RowCount | ForEach-Object {
        foreach ($token in $_.Split(',')) {
            [int] $value = 0
            if (-not [int]::TryParse(
                    $token.Trim(),
                    [Globalization.NumberStyles]::None,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [ref] $value) -or $value -lt 10 -or $value -gt 10000000) {
                throw "RowCount must contain invariant integers between 10 and 10000000. Received '$token'."
            }
            $value
        }
    } | Sort-Object -Unique)

if (-not $SkipBuild.IsPresent) {
    dotnet build $projectPath --configuration Release --framework net10.0-windows
    if ($LASTEXITCODE -ne 0) {
        throw 'The PSEventViewer Release build failed before the event-store benchmark.'
    }
    dotnet build $fixtureProjectPath --configuration Release --framework net8.0-windows
    if ($LASTEXITCODE -ne 0) {
        throw 'The compiled event-store benchmark fixture build failed.'
    }
}

Import-Module $modulePath -Force -ErrorAction Stop
Add-Type -Path $fixtureAssemblyPath -ErrorAction Stop
Import-Module PSPublishModule -MinimumVersion 3.0.134 -Force -ErrorAction Stop
$results = foreach ($currentRowCount in $resolvedRowCounts) {
    $invoke = @{
        Path = $specPath
        OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
        WarmupCount = $WarmupCount
        IterationCount = $IterationCount
        Variable = @{
            RowCounts = [string] $currentRowCount
        }
    }
    if ($Plan.IsPresent) {
        $invoke.Plan = $true
    }
    Invoke-BenchmarkSuite @invoke
}
if (-not $Plan.IsPresent) {
    $failed = @($results.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' })
    if ($failed.Count -gt 0) {
        throw "One or more event-store benchmark runs contained failed samples."
    }
}
if ($UpdateReadme.IsPresent) {
    if ($Plan.IsPresent) {
        throw 'README evidence cannot be updated from a benchmark plan.'
    }
    if ($IterationCount -lt 3) {
        throw 'README evidence requires at least three measured iterations.'
    }

    $readmePath = Join-Path $PSScriptRoot 'README.md'
    foreach ($result in @($results)) {
        $summaryPath = [string] $result.Artifacts['summary.json']
        $rowCountValue = [string] $result.Summary[0].Variables['RowCount']
        if ([string]::IsNullOrWhiteSpace($summaryPath) -or -not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
            throw "Benchmark result '$($result.RunId)' does not expose a summary artifact."
        }
        if ($resolvedRowCounts -notcontains [int] $rowCountValue) {
            throw "Benchmark result '$($result.RunId)' does not match a requested row count."
        }
        Update-BenchmarkDocument -Path $readmePath -BlockId "event-store-$rowCountValue" -SummaryPath $summaryPath -Renderer SummaryTable -Confirm:$false | Out-Null
    }
}
$results
