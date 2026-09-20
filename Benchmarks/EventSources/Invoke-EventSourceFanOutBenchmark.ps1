<#
.SYNOPSIS
Measures bounded EventViewerX fan-out across several remote Windows Event Log targets.

.DESCRIPTION
Captures a fixed record boundary per target, then compares sequential and bounded-parallel
priming over the same exact per-machine event windows. Every sample validates that all
targets contributed the requested count and that both modes returned the same identities.

.EXAMPLE
.\Invoke-EventSourceFanOutBenchmark.ps1 -MachineName AD0,AD1,AD2 -LogName Security -SampleCount 100,1000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateCount(2, 64)]
    [string[]] $MachineName,

    [ValidateNotNullOrEmpty()]
    [string] $LogName = 'Security',

    [ValidateRange(1, [int]::MaxValue)]
    [int[]] $SampleCount = @(100, 1000),

    [ValidateRange(0, 100)]
    [int] $WarmupCount = 0,

    [ValidateRange(1, 100)]
    [int] $IterationCount = 3,

    [ValidateRange(2, 64)]
    [int] $MaxConcurrency = 8,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $RemoteConnectionTimeoutMilliseconds = 5000,

    [ValidateRange(0, [int]::MaxValue)]
    [int] $RemoteReadTimeoutMilliseconds = 30000,

    [string] $OutputRoot,

    [switch] $Plan,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repositoryRoot 'Sources\PSEventViewer\PSEventViewer.csproj'
$modulePath = Join-Path $repositoryRoot 'Sources\PSEventViewer\bin\Release\net10.0-windows\PSEventViewer.dll'
$corePath = Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll'
$specPath = Join-Path $PSScriptRoot 'event-source-fanout.benchmark.ps1'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventSources\FanOut'
}

[string[]] $targets = @($MachineName | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
if ($targets.Count -lt 2) {
    throw 'MachineName must contain at least two distinct non-empty targets.'
}
if (-not $SkipBuild.IsPresent) {
    dotnet build $projectPath --configuration Release --framework net10.0-windows
    if ($LASTEXITCODE -ne 0) {
        throw 'The PSEventViewer Release build failed before the event-source fan-out benchmark.'
    }
}

Import-Module $modulePath -Force -ErrorAction Stop
Import-Module PSPublishModule -MinimumVersion 3.0.134 -Force -ErrorAction Stop
[array] $boundaries = foreach ($target in $targets) {
    $boundaryEvent = Get-WinEvent -ComputerName $target -LogName $LogName -MaxEvents 1
    if ($null -eq $boundaryEvent -or $null -eq $boundaryEvent.RecordId) {
        throw "Unable to capture a stable record boundary for '$LogName' on '$target'."
    }
    [pscustomobject]@{
        MachineName = $target
        MaximumRecordId = [long] $boundaryEvent.RecordId
    }
}

$invoke = @{
    Path = $specPath
    OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    RunMode = 'remote'
    Variable = @{
        EventViewerXPath = $corePath
        TargetsJson = $boundaries | ConvertTo-Json -Compress
        LogName = $LogName
        SampleCounts = [string] (($SampleCount | Sort-Object -Unique) -join ',')
        MaxConcurrency = $MaxConcurrency
        RemoteConnectionTimeoutMilliseconds = $RemoteConnectionTimeoutMilliseconds
        RemoteReadTimeoutMilliseconds = $RemoteReadTimeoutMilliseconds
    }
}
if ($Plan.IsPresent) {
    $invoke.Plan = $true
}
$result = Invoke-BenchmarkSuite @invoke
if (-not $Plan.IsPresent) {
    $failed = @($result.Summary | Where-Object { $_.FailureCount -gt 0 -or $_.Status -eq 'Failed' })
    if ($failed.Count -gt 0) {
        throw "Event-source fan-out benchmark run $($result.RunId) contained failed samples."
    }
}
$result
