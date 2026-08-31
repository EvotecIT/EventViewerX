<#
.SYNOPSIS
Builds and runs the reproducible EVTX parsing benchmark.

.DESCRIPTION
Builds the current EventViewerX/PSEventViewer sources, then delegates timing,
iteration order, validation, comparison, and artifacts to Invoke-BenchmarkSuite.
Large EVTX fixtures and competitor binaries remain external inputs.

.EXAMPLE
.\Invoke-EventLogParsingBenchmark.ps1 -Case Smoke-Common-Scan-Metadata -Engine DotNet, EventViewerX, GetWinEvent, PSEventViewer

.EXAMPLE
.\Invoke-EventLogParsingBenchmark.ps1 -LargeFixturePath C:\Temp\Security.evtx -ExpectedLargeCount 1000000 -EvtxECmdPath C:\Tools\EvtxECmd.exe -EvtxMapsPath C:\Tools\Maps -Case Large-Evtx-FullJson

.EXAMPLE
.\Invoke-EventLogParsingBenchmark.ps1 -LargeFixturePath C:\Temp\Security.evtx -ExpectedLargeCount 1000000 -ReadmeTable Common -IterationCount 3

.EXAMPLE
.\Invoke-EventLogParsingBenchmark.ps1 -LargeFixturePath C:\Temp\Security.evtx -ExpectedLargeCount 1000000 -ReadmeTable ExactOutput -IterationCount 3

.EXAMPLE
.\Invoke-EventLogParsingBenchmark.ps1 -LargeFixturePath C:\Temp\Security.evtx -ExpectedLargeCount 1000000 -EvtxECmdPath C:\Tools\EvtxECmd.exe -EvtxMapsPath C:\Tools\Maps -ReadmeTable NativeOutput -IterationCount 3
#>
[CmdletBinding()]
param(
    [string[]] $Case,

    [string[]] $Engine,

    [string] $LargeFixturePath,

    [ValidateRange(0, [long]::MaxValue)]
    [long] $ExpectedLargeCount,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $ExpensiveSampleCount = 100000,

    [ValidateRange(1, [int]::MaxValue)]
    [int[]] $ScaleSampleCount = @(1000, 10000, 100000, 1000000),

    [ValidateRange(1, [int]::MaxValue)]
    [int] $ReportSampleCount = 1000,

    [string] $TypedFixturePath,

    [ValidateRange(0, [long]::MaxValue)]
    [long] $ExpectedTypedCount,

    [string] $TypedEventTypes = 'ADUserLogon,ADUserLogonFailed,ADUserLockouts',

    [string] $EvtxECmdPath,

    [string] $EvtxMapsPath,

    [string] $BaselineHostPath,

    [string] $PSEventViewerPath,

    [string] $BaselineModulePath,

    [string] $EventViewerXCliPath,

    [string] $EventViewerXPortableCliPath,

    [string] $OutputRoot,

    [ValidateRange(0, [int]::MaxValue)]
    [int] $WarmupCount = 0,

    [ValidateRange(1, [int]::MaxValue)]
    [int] $IterationCount = 1,

    [ValidateSet('None', 'Common', 'Scale', 'ColdStart', 'Reporting', 'ExactOutput', 'NativeOutput', 'EvtxNative')]
    [string] $ReadmeTable = 'None',

    [switch] $Plan
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$hostProject = Join-Path $PSScriptRoot 'EventLogParsing.BenchmarkHost\EventLogParsing.BenchmarkHost.csproj'
$specPath = Join-Path $PSScriptRoot 'event-log-parsing.benchmark.ps1'

Import-Module PSPublishModule -MinimumVersion 3.0.76 -ErrorAction Stop

if ([bool] $EvtxECmdPath -ne [bool] $EvtxMapsPath) {
    throw 'EvtxECmdPath and EvtxMapsPath must be supplied together.'
}

if ($ReadmeTable -notin 'None', 'ColdStart', 'Reporting') {
    if (-not $LargeFixturePath -or $ExpectedLargeCount -le 0) {
        throw 'ReadmeTable requires LargeFixturePath and a positive ExpectedLargeCount.'
    }
    if ($Case -or $Engine) {
        throw 'ReadmeTable owns its curated Case and Engine matrix. Do not combine it with Case or Engine.'
    }

    if (-not $Plan -and $IterationCount -lt 3) {
        throw "The public $ReadmeTable table requires at least three iterations."
    }

    if ($ReadmeTable -eq 'Common') {
        $Case = @(
            'Large-Common-Scan-Metadata'
            'Large-Common-Sample-Message'
            'Large-Common-Sample-StructuredData'
            'Large-Common-Sample-StructuredDataAndMessage'
            'Large-Common-Sample-Full'
        )
        $Engine = 'DotNet', 'EventViewerX', 'PSEventViewer', 'GetWinEvent'
    } elseif ($ReadmeTable -eq 'Scale') {
        [array] $Case = foreach ($sampleCount in $ScaleSampleCount | Sort-Object -Unique) {
            if ($sampleCount -le $ExpectedLargeCount) {
                foreach ($mode in 'Metadata', 'StructuredDataAndMessage', 'Full') {
                    "Large-Scale-$sampleCount-$mode"
                }
            }
        }
        $Engine = 'DotNet', 'EventViewerX', 'PSEventViewer', 'GetWinEvent'
    } elseif ($ReadmeTable -eq 'ExactOutput') {
        $Case = @(
            'Large-Exact-Export-MetadataCsv'
            'Large-Exact-Export-RawXml'
        )
        $Engine = 'DotNet', 'EventViewerXExport', 'PSEventViewer', 'GetWinEvent'
    } elseif ($ReadmeTable -eq 'NativeOutput') {
        if (-not $EvtxECmdPath -or -not $EvtxMapsPath) {
            throw 'ReadmeTable NativeOutput requires EvtxECmdPath and EvtxMapsPath.'
        }
        $Case = @(
            'Large-Native-Output-Csv'
            'Large-Native-Output-FullJson'
            'Large-Native-Output-Xml'
        )
        $Engine = 'EventViewerXExport', 'EvtxECmd'
    } elseif ($ReadmeTable -eq 'EvtxNative') {
        if (-not $EvtxECmdPath -or -not $EvtxMapsPath) {
            throw 'ReadmeTable EvtxNative requires EvtxECmdPath and EvtxMapsPath.'
        }
        $Case = @(
            'Large-Evtx-NativeParse'
            'Large-Evtx-ForensicCsv'
            'Large-Evtx-FullJson'
            'Large-Evtx-Xml'
        )
        $Engine = 'EvtxECmd'
    }
}
if ($ReadmeTable -eq 'Reporting') {
    if ($Case -or $Engine) {
        throw 'ReadmeTable Reporting owns its curated Case and Engine matrix. Do not combine it with Case or Engine.'
    }
    if (-not $TypedFixturePath -or $ExpectedTypedCount -le 0) {
        throw 'ReadmeTable Reporting requires TypedFixturePath and a positive ExpectedTypedCount.'
    }
    if (-not $Plan -and $IterationCount -lt 3) {
        throw 'The public Reporting table requires at least three iterations.'
    }
    $Case = 'Typed-Report-Html', 'Typed-Report-Excel', 'Typed-Report-Email', 'Typed-Report-All'
    $Engine = 'EventViewerXReport'
}
if ($ReadmeTable -eq 'ColdStart') {
    if ($Case -or $Engine) {
        throw 'ReadmeTable ColdStart owns its curated Case and Engine matrix. Do not combine it with Case or Engine.'
    }
    if (-not $Plan -and $IterationCount -lt 3) {
        throw 'The public ColdStart table requires at least three iterations.'
    }
    $Case = 'Smoke-Command-Cold-StructuredDataAndMessage'
    $Engine = @('EventViewerXCli')
    if ($EventViewerXPortableCliPath) {
        $Engine += 'EventViewerXCliPortable'
    }
    $Engine += 'PSEventViewer', 'GetWinEvent'
}

dotnet build $hostProject --configuration Release --framework net10.0-windows
if ($LASTEXITCODE -ne 0) {
    throw "The benchmark host build failed with exit code $LASTEXITCODE."
}

dotnet build (Join-Path $repositoryRoot 'Sources\PSEventViewer\PSEventViewer.csproj') --configuration Release --framework net10.0-windows
if ($LASTEXITCODE -ne 0) {
    throw "The PSEventViewer build failed with exit code $LASTEXITCODE."
}
dotnet build (Join-Path $repositoryRoot 'Sources\EventViewerX.Cli\EventViewerX.Cli.csproj') --configuration Release --framework net10.0
if ($LASTEXITCODE -ne 0) {
    throw "The EventViewerX CLI build failed with exit code $LASTEXITCODE."
}

$variables = @{
    ReadmeTable       = $ReadmeTable
    ReportSampleCount = $ReportSampleCount
    ScaleSampleCounts = [string] ($ScaleSampleCount -join ',')
}
if ($LargeFixturePath) {
    $variables.LargeFixturePath = [IO.Path]::GetFullPath($LargeFixturePath)
    $variables.ExpectedLargeCount = $ExpectedLargeCount
    $variables.ExpensiveSampleCount = $ExpensiveSampleCount
}
if ($TypedFixturePath) {
    if ($ExpectedTypedCount -le 0) {
        throw 'TypedFixturePath requires a positive ExpectedTypedCount.'
    }
    $variables.TypedFixturePath = [IO.Path]::GetFullPath($TypedFixturePath)
    $variables.ExpectedTypedCount = $ExpectedTypedCount
    $variables.TypedEventTypes = $TypedEventTypes
}
if ($EvtxECmdPath) {
    $variables.EvtxECmdPath = [IO.Path]::GetFullPath($EvtxECmdPath)
}
if ($EvtxMapsPath) {
    $mapsFullPath = [IO.Path]::GetFullPath($EvtxMapsPath)
    if (-not (Test-Path -LiteralPath $mapsFullPath -PathType Container)) {
        throw "EvtxECmd maps directory '$mapsFullPath' does not exist."
    }
    $variables.EvtxMapsPath = $mapsFullPath
}
if ($BaselineHostPath) {
    $variables.BaselineHostPath = [IO.Path]::GetFullPath($BaselineHostPath)
}
if ($PSEventViewerPath) {
    $moduleFullPath = [IO.Path]::GetFullPath($PSEventViewerPath)
    if (-not (Test-Path -LiteralPath $moduleFullPath -PathType Leaf)) {
        throw "The PSEventViewer module '$moduleFullPath' does not exist."
    }
    $variables.PSEventViewerPath = $moduleFullPath
}
if ($BaselineModulePath) {
    $variables.BaselineModulePath = [IO.Path]::GetFullPath($BaselineModulePath)
}
if ($EventViewerXCliPath) {
    $variables.EventViewerXCliPath = [IO.Path]::GetFullPath($EventViewerXCliPath)
}
if ($EventViewerXPortableCliPath) {
    $portableFullPath = [IO.Path]::GetFullPath($EventViewerXPortableCliPath)
    if (-not (Test-Path -LiteralPath $portableFullPath -PathType Leaf)) {
        throw "The portable EventViewerX CLI '$portableFullPath' does not exist."
    }
    $variables.EventViewerXPortableCliPath = $portableFullPath
}

$parameters = @{
    Path           = $specPath
    Variable       = $variables
    WarmupCount    = $WarmupCount
    IterationCount = $IterationCount
    Plan           = $Plan
}
if ($Case) {
    $parameters.Case = $Case
}
if ($Engine) {
    $parameters.Engine = $Engine
}
if ($OutputRoot) {
    $parameters.OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
}

$benchmarkResult = Invoke-BenchmarkSuite @parameters
if (-not $Plan) {
    [array] $failedSamples = foreach ($sample in $benchmarkResult.Samples) {
        if ($sample.Status -eq 'Failed') {
            $sample
        }
    }
    if ($failedSamples.Count -gt 0) {
        [array] $failureSummary = foreach ($sample in $failedSamples) {
            '{0}/{1}/iteration-{2}: {3}' -f $sample.Scenario, $sample.Engine, $sample.Iteration, $sample.Reason
        }
        throw "The benchmark completed with $($failedSamples.Count) failed sample(s):`n$($failureSummary -join "`n")"
    }

    $readmePath = Join-Path $repositoryRoot 'README.md'
    if ($ReadmeTable -eq 'Common') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-common-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'Scale') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-scale-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'ColdStart') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-cold-start-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'Reporting') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-reporting-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'ExactOutput') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-exact-output-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'NativeOutput') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-native-output-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    } elseif ($ReadmeTable -eq 'EvtxNative') {
        Update-BenchmarkDocument `
            -Path $readmePath `
            -BlockId 'event-log-evtx-native-benchmark' `
            -ComparisonPath $benchmarkResult.Artifacts['comparison.json'] `
            -Renderer ComparisonTable `
            -Confirm:$false | Out-Null
    }
}

$benchmarkResult
