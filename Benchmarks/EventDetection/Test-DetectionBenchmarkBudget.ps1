<#
.SYNOPSIS
Validates full BenchmarkDotNet detection results against checked-in budgets.

.DESCRIPTION
Normalizes the permanent candidate-index and streaming-throughput BenchmarkDotNet
JSON reports into PowerForge summary rows. The gate verifies the complete 4-case
candidate matrix and 12-case throughput matrix before comparing timing and
allocation metrics. Allocation is expressed per event for throughput so scale
changes cannot hide a regression.

.EXAMPLE
.\Test-DetectionBenchmarkBudget.ps1 `
    -CandidateResultPath .\BenchmarkDotNet.Artifacts\results\EventViewerX.DetectionBenchmarks.DetectionCandidateIndexBenchmarks-report-full-compressed.json `
    -ThroughputResultPath .\BenchmarkDotNet.Artifacts\results\EventViewerX.DetectionBenchmarks.DetectionThroughputBenchmarks-report-full-compressed.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CandidateResultPath,

    [Parameter(Mandatory)]
    [string] $ThroughputResultPath,

    [string] $BaselineRoot = $PSScriptRoot,

    [string] $OutputRoot,

    [switch] $UpdateBaseline
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventDetection\Gate'
}
$outputRoot = [IO.Path]::GetFullPath($OutputRoot)
$baselineRoot = [IO.Path]::GetFullPath($BaselineRoot)
$candidateResultPath = (Resolve-Path -LiteralPath $CandidateResultPath).Path
$throughputResultPath = (Resolve-Path -LiteralPath $ThroughputResultPath).Path

function ConvertFrom-BenchmarkParameters {
    param([string] $Value)

    $result = [ordered] @{}
    foreach ($part in @($Value -split '&')) {
        if ([string]::IsNullOrWhiteSpace($part)) {
            continue
        }
        $pair = $part.Split('=', 2)
        if ($pair.Count -ne 2) {
            throw "Benchmark parameter '$part' is not a name=value pair."
        }
        $result[[Uri]::UnescapeDataString($pair[0])] = [Uri]::UnescapeDataString($pair[1])
    }
    $result
}

function ConvertTo-DetectionSummaryRow {
    param(
        [Parameter(Mandatory)] $Benchmark,
        [Parameter(Mandatory)] [string] $Scenario,
        [Parameter(Mandatory)] [string[]] $RequiredParameters
    )

    $variables = ConvertFrom-BenchmarkParameters -Value ([string] $Benchmark.Parameters)
    foreach ($name in $RequiredParameters) {
        if (-not $variables.Contains($name)) {
            throw "Benchmark '$($Benchmark.FullName)' is missing required parameter '$name'."
        }
    }
    if ($null -eq $Benchmark.Statistics -or $null -eq $Benchmark.Memory) {
        throw "Benchmark '$($Benchmark.FullName)' is missing statistics or memory diagnostics."
    }
    $eventCount = if ($variables.Contains('EventCount')) {
        [long] $variables.EventCount
    } else {
        1L
    }
    if ($eventCount -le 0) {
        throw "Benchmark '$($Benchmark.FullName)' has an invalid EventCount."
    }
    $sampleCountProperty = $Benchmark.Statistics.PSObject.Properties['N']
    $medianProperty = $Benchmark.Statistics.PSObject.Properties['Median']
    $allocationProperty = $Benchmark.Memory.PSObject.Properties['BytesAllocatedPerOperation']
    if ($null -eq $sampleCountProperty -or $null -eq $sampleCountProperty.Value -or
        $null -eq $medianProperty -or $null -eq $medianProperty.Value) {
        throw "Benchmark '$($Benchmark.FullName)' is missing required timing statistics."
    }
    $sampleCount = [int] $sampleCountProperty.Value
    $medianNanoseconds = [double] $medianProperty.Value
    if ($sampleCount -le 0 -or
        [double]::IsNaN($medianNanoseconds) -or
        [double]::IsInfinity($medianNanoseconds) -or
        $medianNanoseconds -lt 0) {
        throw "Benchmark '$($Benchmark.FullName)' has invalid timing statistics."
    }
    if ($null -eq $allocationProperty -or $null -eq $allocationProperty.Value) {
        throw "Benchmark '$($Benchmark.FullName)' is missing BytesAllocatedPerOperation."
    }
    $allocated = [double] $allocationProperty.Value
    if ([double]::IsNaN($allocated) -or [double]::IsInfinity($allocated) -or $allocated -lt 0) {
        throw "Benchmark '$($Benchmark.FullName)' has invalid BytesAllocatedPerOperation."
    }
    [ordered] @{
        suite = 'event-detection'
        scenario = $Scenario
        operation = [string] $Benchmark.Method
        engine = 'EventViewerXDetection'
        variables = $variables
        sampleCount = $sampleCount
        failureCount = 0
        status = 'Succeeded'
        medianMs = [double] $Benchmark.Statistics.Median / 1000000.0
        metrics = [ordered] @{
            AllocatedBytes = $allocated
            AllocatedBytesPerEvent = $allocated / $eventCount
        }
    }
}

function Assert-ExactMatrix {
    param(
        [Parameter(Mandatory)] [object[]] $Rows,
        [Parameter(Mandatory)] [string[]] $ExpectedKeys,
        [Parameter(Mandatory)] [scriptblock] $Key
    )

    [string[]] $allKeys = @($Rows | ForEach-Object $Key)
    [array] $duplicates = @($allKeys | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) {
        throw "Benchmark matrix contains duplicate parameter tuples: $($duplicates.Name -join ', ')."
    }
    [string[]] $actual = @($allKeys | Sort-Object)
    [string[]] $expected = @($ExpectedKeys | Sort-Object -Unique)
    if (($actual -join "`n") -ne ($expected -join "`n")) {
        throw "Benchmark matrix mismatch. Expected [$($expected -join ', ')]; actual [$($actual -join ', ')]."
    }
}

$candidateDocument = Get-Content -LiteralPath $candidateResultPath -Raw | ConvertFrom-Json
$throughputDocument = Get-Content -LiteralPath $throughputResultPath -Raw | ConvertFrom-Json
[object[]] $candidateRows = @(
    $candidateDocument.Benchmarks |
        ForEach-Object {
            ConvertTo-DetectionSummaryRow -Benchmark $_ -Scenario 'CandidateIndex' -RequiredParameters RuleCount
        }
)
[object[]] $throughputRows = @(
    $throughputDocument.Benchmarks |
        ForEach-Object {
            ConvertTo-DetectionSummaryRow -Benchmark $_ -Scenario 'Throughput' -RequiredParameters EventCount,Lane
        }
)

Assert-ExactMatrix -Rows $candidateRows -ExpectedKeys @('1', '10', '100', '1000') -Key {
    [string] $_.variables.RuleCount
}
$expectedThroughput = foreach ($eventCount in 1000,10000,100000,1000000) {
    foreach ($lane in 'StatelessPredicate','ThresholdWindow','OrderedTemporal') {
        "$eventCount|$lane"
    }
}
Assert-ExactMatrix -Rows $throughputRows -ExpectedKeys $expectedThroughput -Key {
    "$($_.variables.EventCount)|$($_.variables.Lane)"
}
if ($UpdateBaseline.IsPresent -and
    @($candidateRows + $throughputRows | Where-Object sampleCount -lt 3).Count -gt 0) {
    throw 'Updating detection baselines requires at least three measured samples for every benchmark tuple.'
}

[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$candidateSummaryPath = Join-Path $outputRoot 'candidate-summary.json'
$throughputSummaryPath = Join-Path $outputRoot 'throughput-summary.json'
$candidateRows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $candidateSummaryPath -Encoding utf8
$throughputRows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $throughputSummaryPath -Encoding utf8

Import-Module PSPublishModule -MinimumVersion 3.0.134 -Force -ErrorAction Stop
$groupBy = @('Suite', 'Scenario', 'Operation', 'Engine', 'Variables')
$gates = @(
    @{
        SummaryPath = $candidateSummaryPath
        BaselinePath = Join-Path $baselineRoot 'detection-candidate-timing-baseline.json'
        Metric = 'MedianMs'
        RelativeTolerance = 0.5
        AbsoluteToleranceMs = 0.25
    }
    @{
        SummaryPath = $candidateSummaryPath
        BaselinePath = Join-Path $baselineRoot 'detection-candidate-allocation-baseline.json'
        Metric = 'AllocatedBytes'
        RelativeTolerance = 0.1
        AbsoluteToleranceMs = 65536
    }
    @{
        SummaryPath = $throughputSummaryPath
        BaselinePath = Join-Path $baselineRoot 'detection-throughput-timing-baseline.json'
        Metric = 'MedianMs'
        RelativeTolerance = 0.5
        AbsoluteToleranceMs = 0.25
    }
    @{
        SummaryPath = $throughputSummaryPath
        BaselinePath = Join-Path $baselineRoot 'detection-throughput-allocation-baseline.json'
        Metric = 'AllocatedBytesPerEvent'
        RelativeTolerance = 0.1
        AbsoluteToleranceMs = 16
    }
)

$results = foreach ($gate in $gates) {
    $gate.GroupBy = $groupBy
    $gate.Confirm = $false
    if ($UpdateBaseline.IsPresent) {
        $gate.Update = $true
    }
    Test-BenchmarkGate @gate
}
$results
