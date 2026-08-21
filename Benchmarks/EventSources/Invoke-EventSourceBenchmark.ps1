<#
.SYNOPSIS
Measures local or remote Windows Event Log query paths with identity validation.

.DESCRIPTION
Builds the thin PowerShell module, loads it once outside the timed operations,
then compares the EventViewerX C# core, Get-EVXEvent, and Get-WinEvent against
the same channel, direction, event window, and metadata projection.

.EXAMPLE
.\Invoke-EventSourceBenchmark.ps1 -MachineName AD0 -LogName Security -SampleCount 100,1000 -IterationCount 3
#>
[CmdletBinding()]
param(
    [string] $MachineName,

    [ValidateNotNullOrEmpty()]
    [string] $LogName = 'Security',

    [ValidateRange(1, [int]::MaxValue)]
    [int[]] $SampleCount = @(100, 1000),

    [ValidateRange(0, 100)]
    [int] $WarmupCount = 0,

    [ValidateRange(1, 100)]
    [int] $IterationCount = 3,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $RemoteConnectionTimeoutMilliseconds = 5000,

    [ValidateRange(0, [int]::MaxValue)]
    [int] $RemoteReadTimeoutMilliseconds = 30000,

    [string] $OutputRoot,

    [switch] $UpdateReadme,

    [switch] $Plan,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repositoryRoot 'Sources\PSEventViewer\PSEventViewer.csproj'
$modulePath = Join-Path $repositoryRoot 'Sources\PSEventViewer\bin\Release\net10.0-windows\PSEventViewer.dll'
$corePath = Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll'
$specPath = Join-Path $PSScriptRoot 'event-source.benchmark.ps1'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'Ignore\Benchmarks\EventSources'
}

if (-not $SkipBuild.IsPresent) {
    dotnet build $projectPath --configuration Release --framework net10.0-windows
    if ($LASTEXITCODE -ne 0) {
        throw 'The PSEventViewer Release build failed before the event-source benchmark.'
    }
}

Import-Module $modulePath -Force -ErrorAction Stop
Import-Module PSPublishModule -MinimumVersion 3.0.76 -Force -ErrorAction Stop
$boundaryParameters = @{
    LogName = $LogName
    MaxEvents = 1
}
if (-not [string]::IsNullOrWhiteSpace($MachineName)) {
    $boundaryParameters.ComputerName = $MachineName
}
$boundaryEvent = Get-WinEvent @boundaryParameters
if ($null -eq $boundaryEvent -or $null -eq $boundaryEvent.RecordId) {
    throw "Unable to capture a stable record boundary for '$LogName' on '$MachineName'."
}
$invoke = @{
    Path = $specPath
    OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    WarmupCount = $WarmupCount
    IterationCount = $IterationCount
    RunMode = 'remote'
    Variable = @{
        EventViewerXPath = $corePath
        MachineName = $MachineName
        LogName = $LogName
        MaximumRecordId = [long] $boundaryEvent.RecordId
        SampleCounts = [string] (($SampleCount | Sort-Object -Unique) -join ',')
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
        throw "Event-source benchmark run $($result.RunId) contained failed samples."
    }
    if ($UpdateReadme.IsPresent) {
        if ($IterationCount -lt 3) {
            throw 'Publishing remote benchmark evidence requires at least three rotated iterations.'
        }
        Update-BenchmarkDocument `
            -Path (Join-Path $repositoryRoot 'README.MD') `
            -BlockId 'event-log-remote-benchmark' `
            -ComparisonPath $result.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    }
}
$result
