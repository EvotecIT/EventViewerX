$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$hostDll = Get-BenchmarkInput -Name BenchmarkHostPath -Default (Join-Path $PSScriptRoot 'EventLogParsing.BenchmarkHost\bin\Release\net10.0-windows\EventLogParsing.BenchmarkHost.dll')
$modulePath = Get-BenchmarkInput -Name PSEventViewerPath -Default (Join-Path $repositoryRoot 'Sources\PSEventViewer\bin\Release\net10.0-windows\PSEventViewer.dll')
$cliPath = Get-BenchmarkInput -Name EventViewerXCliPath -Default (Join-Path $repositoryRoot 'Sources\EventViewerX.Cli\bin\Release\net10.0-windows\evx.exe')
$portableCliPath = Get-BenchmarkInput -Name EventViewerXPortableCliPath
$baselineHostPath = Get-BenchmarkInput -Name BaselineHostPath
$baselineModulePath = Get-BenchmarkInput -Name BaselineModulePath
$evtxECmdPath = Get-BenchmarkInput -Name EvtxECmdPath
$evtxMapsPath = Get-BenchmarkInput -Name EvtxMapsPath
$largeFixturePath = Get-BenchmarkInput -Name LargeFixturePath
$expectedLargeCount = Get-BenchmarkInput -Name ExpectedLargeCount -Int -Default 0
$expensiveSampleCount = Get-BenchmarkInput -Name ExpensiveSampleCount -Int -Default 100000
$scaleSampleCountsText = Get-BenchmarkInput -Name ScaleSampleCounts -Default '1000,10000,100000,1000000'
$reportSampleCount = Get-BenchmarkInput -Name ReportSampleCount -Int -Default 1000
$typedFixturePath = Get-BenchmarkInput -Name TypedFixturePath
$expectedTypedCount = Get-BenchmarkInput -Name ExpectedTypedCount -Int -Default 0
$typedEventTypes = Get-BenchmarkInput -Name TypedEventTypes -Default 'ADUserLogon,ADUserLogonFailed,ADUserLockouts'
$readmeTable = Get-BenchmarkInput -Name ReadmeTable -Default None
$smokeFixturePath = Join-Path $repositoryRoot 'Tests\Logs\NamedFilterExamples.evtx'
$powerShellRunner = Join-Path $PSScriptRoot 'Invoke-PowerShellEventLogBenchmark.ps1'
$evtxRunner = Join-Path $PSScriptRoot 'Invoke-EvtxECmdBenchmark.ps1'
$benchmarkWrapper = Join-Path $PSScriptRoot 'Invoke-EventLogParsingBenchmark.ps1'
$benchmarkSpec = Join-Path $PSScriptRoot 'event-log-parsing.benchmark.ps1'
$pwshPath = [string] (Get-Command pwsh -ErrorAction Stop).Source
$dotnetPath = [string] (Get-Command dotnet -ErrorAction Stop).Source
if ($readmeTable -notin 'None', 'Common', 'Scale', 'ColdStart', 'Reporting', 'ExactOutput', 'NativeOutput', 'EvtxNative') {
    throw "ReadmeTable must be None, Common, Scale, ColdStart, Reporting, ExactOutput, NativeOutput, or EvtxNative. Received '$readmeTable'."
}
[long[]] $scaleSampleCounts = @($scaleSampleCountsText.Split(',') |
        ForEach-Object {
            [long] $parsed = 0
            if (-not [long]::TryParse($_.Trim(), [ref] $parsed) -or $parsed -le 0 -or $parsed -gt [int]::MaxValue) {
                throw "ScaleSampleCounts must contain positive 32-bit event counts. Received '$($_)'."
            }
            $parsed
        } | Sort-Object -Unique)

[array] $fixtures = & {
    [pscustomobject] @{
        Name          = 'Smoke'
        Path          = $smokeFixturePath
        ExpectedCount = 184
    }
    if ($largeFixturePath) {
        [pscustomobject] @{
            Name          = 'Large'
            Path          = [IO.Path]::GetFullPath($largeFixturePath)
            ExpectedCount = $expectedLargeCount
        }
    }
}

[array] $definitions = foreach ($fixture in $fixtures) {
    foreach ($mode in 'Metadata', 'Message', 'StructuredData', 'StructuredDataAndMessage', 'Full') {
        [pscustomobject] @{
            Name          = "$($fixture.Name)-Common-First-$mode"
            Fixture       = $fixture.Name
            FixturePath   = $fixture.Path
            ExpectedCount = 1
            MaxEvents     = 1
            Workload      = $mode
        }
        [pscustomobject] @{
            Name          = "$($fixture.Name)-Common-Scan-$mode"
            Fixture       = $fixture.Name
            FixturePath   = $fixture.Path
            ExpectedCount = $fixture.ExpectedCount
            MaxEvents     = 0
            Workload      = $mode
        }
    }

    if ($fixture.Name -eq 'Large' -and $expensiveSampleCount -gt 0) {
        $sampleCount = if ($fixture.ExpectedCount -gt 0) {
            [Math]::Min([long] $fixture.ExpectedCount, [long] $expensiveSampleCount)
        } else {
            $expensiveSampleCount
        }
        foreach ($mode in 'Metadata', 'Message', 'StructuredData', 'StructuredDataAndMessage', 'Full') {
            [pscustomobject] @{
                Name          = "$($fixture.Name)-Common-Sample-$mode"
                Fixture       = $fixture.Name
                FixturePath   = $fixture.Path
                ExpectedCount = $sampleCount
                MaxEvents     = $sampleCount
                Workload      = $mode
            }
        }
    }

    if ($fixture.Name -eq 'Large' -and $fixture.ExpectedCount -gt 0) {
        foreach ($scaleCount in $scaleSampleCounts | Where-Object { $_ -le $fixture.ExpectedCount }) {
            foreach ($mode in 'Metadata', 'StructuredDataAndMessage', 'Full') {
                [pscustomobject] @{
                    Name          = "$($fixture.Name)-Scale-$scaleCount-$mode"
                    Fixture       = $fixture.Name
                    FixturePath   = $fixture.Path
                    ExpectedCount = $scaleCount
                    MaxEvents     = $scaleCount
                    Workload      = $mode
                }
            }
        }
    }

    [pscustomobject] @{
        Name          = "$($fixture.Name)-Exact-Export-MetadataCsv"
        Fixture       = $fixture.Name
        FixturePath   = $fixture.Path
        ExpectedCount = $fixture.ExpectedCount
        MaxEvents     = 0
        Workload      = 'MetadataCsv'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Exact-Export-RawXml"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'ExactRawXml'
        ReadMode        = 'StructuredData'
        OutputFormat    = 'Xml'
        OutputExtension = 'xml'
    }
    [pscustomobject] @{
        Name          = "$($fixture.Name)-Evtx-NativeParse"
        Fixture       = $fixture.Name
        FixturePath   = $fixture.Path
        ExpectedCount = $fixture.ExpectedCount
        MaxEvents     = 0
        Workload      = 'EvtxNativeParse'
        OutputExtension = $null
    }
    [pscustomobject] @{
        Name          = "$($fixture.Name)-Evtx-ForensicCsv"
        Fixture       = $fixture.Name
        FixturePath   = $fixture.Path
        ExpectedCount = $fixture.ExpectedCount
        MaxEvents     = 0
        Workload      = 'EvtxForensicCsv'
        OutputExtension = 'csv'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Evtx-FullJson"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'EvtxFullJson'
        OutputExtension = 'json'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Evtx-Xml"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'EvtxXml'
        OutputExtension = 'xml'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Native-Output-Csv"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'NativeOutputCsv'
        ReadMode        = 'Full'
        OutputFormat    = 'Csv'
        OutputExtension = 'csv'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Native-Output-FullJson"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'NativeOutputFullJson'
        ReadMode        = 'Full'
        OutputFormat    = 'JsonLines'
        OutputExtension = 'jsonl'
    }
    [pscustomobject] @{
        Name            = "$($fixture.Name)-Native-Output-Xml"
        Fixture         = $fixture.Name
        FixturePath     = $fixture.Path
        ExpectedCount   = $fixture.ExpectedCount
        MaxEvents       = 0
        Workload        = 'NativeOutputXml'
        ReadMode        = 'StructuredData'
        OutputFormat    = 'Xml'
        OutputExtension = 'xml'
    }
    $reportCount = if ($fixture.ExpectedCount -gt 0) {
        [Math]::Min([long] $fixture.ExpectedCount, [long] $reportSampleCount)
    } else {
        $reportSampleCount
    }
    foreach ($format in 'Html', 'Excel', 'Email', 'All') {
        [pscustomobject] @{
            Name            = "$($fixture.Name)-Report-$format"
            Fixture         = $fixture.Name
            FixturePath     = $fixture.Path
            ExpectedCount   = $reportCount
            MaxEvents       = $reportCount
            Workload        = "Report$format"
            ReadMode        = 'StructuredDataAndMessage'
            ReportFormat    = $format
            OutputExtension = switch ($format) {
                'Excel' { 'xlsx' }
                default { 'html' }
            }
        }
    }
}

[array] $typedDefinitions = if ($typedFixturePath) {
    if ($expectedTypedCount -le 0) {
        throw 'TypedFixturePath requires a positive ExpectedTypedCount so typed projection identity cannot silently regress.'
    }
    [pscustomobject] @{
        Name          = 'Typed-Scan-StructuredDataAndMessage'
        Fixture       = 'Typed'
        FixturePath   = [IO.Path]::GetFullPath($typedFixturePath)
        ExpectedCount = $expectedTypedCount
        MaxEvents     = 0
        Workload      = 'TypedScan'
        ReadMode      = 'StructuredDataAndMessage'
        Types         = $typedEventTypes
    }
    $typedReportCount = [Math]::Min([long] $expectedTypedCount, [long] $reportSampleCount)
    foreach ($format in 'Html', 'Excel', 'Email', 'All') {
        [pscustomobject] @{
            Name            = "Typed-Report-$format"
            Fixture         = 'Typed'
            FixturePath     = [IO.Path]::GetFullPath($typedFixturePath)
            ExpectedCount   = $typedReportCount
            MaxEvents       = $typedReportCount
            Workload        = "TypedReport$format"
            ReadMode        = 'StructuredDataAndMessage'
            Types           = $typedEventTypes
            ReportFormat    = $format
            OutputExtension = switch ($format) {
                'Excel' { 'xlsx' }
                default { 'html' }
            }
        }
    }
}
$definitions = @($definitions) + @($typedDefinitions | Where-Object { $null -ne $_ })
$definitions += [pscustomobject] @{
    Name          = 'Smoke-Command-Cold-StructuredDataAndMessage'
    Fixture       = 'Smoke'
    FixturePath   = $smokeFixturePath
    ExpectedCount = 1
    MaxEvents     = 1
    Workload      = 'CommandCold'
    ReadMode      = 'StructuredDataAndMessage'
}

$caseDefinitions = @{}
[array] $cases = foreach ($definition in $definitions) {
    $caseDefinitions[$definition.Name] = $definition
    [pscustomobject] @{ Name = $definition.Name }
}
$commonIdentitySignatures = @{}
$exactOutputHashes = @{}

New-BenchmarkSuite 'event-log-parsing' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventLogParsing\Runs') {
    Add-BenchmarkMetadata RepositoryHead ([string] (git -C $repositoryRoot rev-parse HEAD))
    [array] $repositoryStatus = @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=normal)
    $repositoryStatusText = if ($repositoryStatus.Count -gt 0) {
        [string] ($repositoryStatus -join "`n")
    } else {
        '<clean>'
    }
    Add-BenchmarkMetadata RepositoryDirty ([string] ($repositoryStatus.Count -gt 0))
    Add-BenchmarkMetadata RepositoryStatus $repositoryStatusText
    Add-BenchmarkMetadata DotNetVersion ([string] (& $dotnetPath --version))
    Add-BenchmarkMetadata PowerShellVersion $PSVersionTable.PSVersion.ToString()
    Add-BenchmarkMetadata BenchmarkHostSha256 (Get-FileHash -LiteralPath $hostDll -Algorithm SHA256).Hash
    Add-BenchmarkMetadata PSEventViewerSha256 (Get-FileHash -LiteralPath $modulePath -Algorithm SHA256).Hash
    Add-BenchmarkMetadata EventViewerXCliSha256 (Get-FileHash -LiteralPath $cliPath -Algorithm SHA256).Hash
    if ($portableCliPath) {
        Add-BenchmarkMetadata EventViewerXPortableCliSha256 (Get-FileHash -LiteralPath $portableCliPath -Algorithm SHA256).Hash
    }
    Add-BenchmarkMetadata BenchmarkHostEventViewerXSha256 (Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $hostDll) 'EventViewerX.dll') -Algorithm SHA256).Hash
    $moduleDirectory = Split-Path -Parent $modulePath
    $moduleEventViewerXPath = @(
        Join-Path $moduleDirectory 'EventViewerX.dll'
        Join-Path $moduleDirectory 'Lib\Core\EventViewerX.dll'
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ($moduleEventViewerXPath) {
        Add-BenchmarkMetadata PSEventViewerEventViewerXSha256 (Get-FileHash -LiteralPath $moduleEventViewerXPath -Algorithm SHA256).Hash
    }
    Add-BenchmarkMetadata BenchmarkScriptManifest ([string] (@(
        foreach ($scriptPath in $benchmarkWrapper, $benchmarkSpec, $powerShellRunner, $evtxRunner) {
            [ordered] @{
                Path   = $scriptPath
                Sha256 = (Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash
            }
        }
    ) | ConvertTo-Json -Compress))
    Add-BenchmarkMetadata FixtureManifest ([string] (@(
        foreach ($fixture in $fixtures) {
            $fixtureFile = Get-Item -LiteralPath $fixture.Path
            [ordered] @{
                Name   = $fixture.Name
                Path   = $fixtureFile.FullName
                Bytes  = $fixtureFile.Length
                Sha256 = (Get-FileHash -LiteralPath $fixtureFile.FullName -Algorithm SHA256).Hash
            }
        }
    ) | ConvertTo-Json -Compress))
    Add-BenchmarkMetadata ComparisonContract 'Exact output, common public work, different-schema native output, and EvtxECmd-native workflows are reported separately.'
    Add-BenchmarkMetadata ReadmeTable $readmeTable
    if ($baselineHostPath) {
        Add-BenchmarkMetadata BaselineHostSha256 (Get-FileHash -LiteralPath $baselineHostPath -Algorithm SHA256).Hash
        $baselineHostEventViewerXPath = Join-Path (Split-Path -Parent $baselineHostPath) 'EventViewerX.dll'
        if (Test-Path -LiteralPath $baselineHostEventViewerXPath -PathType Leaf) {
            Add-BenchmarkMetadata BaselineHostEventViewerXSha256 (Get-FileHash -LiteralPath $baselineHostEventViewerXPath -Algorithm SHA256).Hash
        }
    }
    if ($baselineModulePath) {
        Add-BenchmarkMetadata BaselineModuleSha256 (Get-FileHash -LiteralPath $baselineModulePath -Algorithm SHA256).Hash
        $baselineModuleEventViewerXPath = Join-Path (Split-Path -Parent $baselineModulePath) 'EventViewerX.dll'
        if (Test-Path -LiteralPath $baselineModuleEventViewerXPath -PathType Leaf) {
            Add-BenchmarkMetadata BaselineModuleEventViewerXSha256 (Get-FileHash -LiteralPath $baselineModuleEventViewerXPath -Algorithm SHA256).Hash
        }
    }
    if ($evtxECmdPath) {
        Add-BenchmarkMetadata EvtxECmdVersion ([Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($evtxECmdPath)).ProductVersion)
        Add-BenchmarkMetadata EvtxECmdSha256 (Get-FileHash -LiteralPath $evtxECmdPath -Algorithm SHA256).Hash
    }
    if ($evtxMapsPath) {
        $mapsFullPath = [IO.Path]::GetFullPath($evtxMapsPath)
        [array] $mapManifest = Get-ChildItem -LiteralPath $mapsFullPath -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                [ordered] @{
                    Path   = [IO.Path]::GetRelativePath($mapsFullPath, $_.FullName).Replace('\', '/')
                    Bytes  = $_.Length
                    Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                }
            }
        $mapManifestJson = [string] ($mapManifest | ConvertTo-Json -Compress)
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            $mapManifestSha256 = [BitConverter]::ToString(
                $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($mapManifestJson))
            ).Replace('-', '')
        } finally {
            $sha256.Dispose()
        }
        Add-BenchmarkMetadata EvtxMapsFileCount ([string] $mapManifest.Count)
        Add-BenchmarkMetadata EvtxMapsManifestSha256 $mapManifestSha256
        Add-BenchmarkMetadata EvtxMapsManifest $mapManifestJson
    }

    Set-BenchmarkPolicy -Warmup 0 -Iterations 1 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup KeepOnFailure
    Add-BenchmarkCaseSource $cases

    Set-BenchmarkSetup {
        param($case, $run)

        $run.Definition = $caseDefinitions[$case.Scenario]
        $run.ResultPath = Join-Path $run.OutputDirectory 'result.json'
        $run.StandardOutputPath = Join-Path $run.OutputDirectory 'stdout.txt'
        $outputExtension = if ($run.Definition.Workload -eq 'MetadataCsv') {
            'csv'
        } else {
            $run.Definition.OutputExtension
        }
        $run.EventOutputPath = if ($outputExtension) {
            Join-Path $run.OutputDirectory "events.$outputExtension"
        } else {
            $null
        }
        [array] $cleanupPaths = foreach ($cleanupPath in $run.ResultPath, $run.StandardOutputPath, $run.EventOutputPath) {
            if (-not [string]::IsNullOrWhiteSpace([string] $cleanupPath)) {
                $cleanupPath
            }
        }
        if ($run.Definition.Workload -like '*Report*' -and $run.EventOutputPath) {
            foreach ($extension in '.html', '.xlsx', '.email.html', '.email.txt', '.txt') {
                [IO.Path]::ChangeExtension($run.EventOutputPath, $extension)
            }
        }
        Remove-Item -LiteralPath $cleanupPaths -Force -ErrorAction SilentlyContinue
    }

    Add-BenchmarkSkipRule {
        param($case)

        $definition = $caseDefinitions[$case.Scenario]
        if ($definition.Workload -eq 'CommandCold') {
            if ($case.Engine -eq 'EventViewerXCliPortable') {
                return -not $portableCliPath
            }
            return $case.Engine -notin 'EventViewerXCli', 'PSEventViewer', 'GetWinEvent'
        }
        if ($definition.Workload -eq 'TypedScan') {
            return $case.Engine -ne 'EventViewerXTyped'
        }
        if ($definition.Workload -like '*Report*') {
            return $case.Engine -ne 'EventViewerXReport'
        }
        if ($case.Engine -in 'EventViewerXTyped', 'EventViewerXReport', 'EventViewerXCli', 'EventViewerXCliPortable') {
            return $true
        }
        if ($definition.Workload -like 'Evtx*') {
            return $case.Engine -ne 'EvtxECmd' -or -not $evtxECmdPath -or -not $evtxMapsPath
        }
        if ($definition.Workload -like 'NativeOutput*') {
            if ($case.Engine -eq 'EvtxECmd') {
                return -not $evtxECmdPath -or -not $evtxMapsPath
            }
            return $case.Engine -ne 'EventViewerXExport'
        }
        if ($definition.Workload -eq 'ExactRawXml') {
            return $case.Engine -notin 'DotNet', 'EventViewerXExport', 'PSEventViewer', 'GetWinEvent'
        }
        if ($definition.Workload -eq 'MetadataCsv') {
            if ($case.Engine -eq 'PSEventViewerBaseline') {
                return -not $baselineModulePath
            }
            return $case.Engine -ne 'DotNet' -and $case.Engine -ne 'PSEventViewer' -and $case.Engine -ne 'GetWinEvent'
        }
        if ($case.Engine -eq 'PropertySelector' -and $definition.Workload -ne 'Metadata') {
            return $true
        }
        if ($case.Engine -eq 'EvtxECmd') {
            return $true
        }
        if ($case.Engine -eq 'EventViewerXBaseline' -and -not $baselineHostPath) {
            return $true
        }
        if ($case.Engine -eq 'PSEventViewerBaseline' -and -not $baselineModulePath) {
            return $true
        }
        return $false
    }

    Add-BenchmarkEngine DotNet {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            $readMode = if ($definition.Workload -eq 'MetadataCsv') {
                'Metadata'
            } elseif ($definition.Workload -in 'ExactRawXml', 'CommandCold') {
                $definition.ReadMode
            } else {
                $definition.Workload
            }
            [array] $arguments = @(
                $hostDll
                '--engine'
                'dotnet'
                '--path'
                $definition.FixturePath
                '--mode'
                $readMode
                '--result'
                $run.ResultPath
                '--max-events'
                $definition.MaxEvents
                if ($definition.Workload -eq 'MetadataCsv') {
                    '--output-path'
                    $run.EventOutputPath
                } elseif ($definition.Workload -eq 'ExactRawXml') {
                    '--format'
                    $definition.OutputFormat
                    '--output-path'
                    $run.EventOutputPath
                }
            )
            & $dotnetPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "Raw .NET benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine PropertySelector {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            & $dotnetPath $hostDll `
                --engine propertyselector `
                --path $definition.FixturePath `
                --mode Metadata `
                --result $run.ResultPath `
                --max-events $definition.MaxEvents *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "EventLogPropertySelector benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EventViewerX {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            & $dotnetPath $hostDll `
                --engine eventviewerx `
                --path $definition.FixturePath `
                --mode $definition.Workload `
                --result $run.ResultPath `
                --max-events $definition.MaxEvents *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "EventViewerX benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EventViewerXTyped {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            [array] $arguments = @(
                $hostDll
                '--engine'
                'eventviewerxtyped'
                '--path'
                $definition.FixturePath
                '--mode'
                'StructuredDataAndMessage'
                '--result'
                $run.ResultPath
                '--max-events'
                $definition.MaxEvents
                '--type'
                $definition.Types
            )
            & $dotnetPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "EventViewerX typed benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EventViewerXReport {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            [array] $arguments = @(
                $hostDll
                '--engine'
                'eventviewerxreport'
                '--path'
                $definition.FixturePath
                '--mode'
                'StructuredDataAndMessage'
                '--result'
                $run.ResultPath
                '--max-events'
                $definition.MaxEvents
                '--output-path'
                $run.EventOutputPath
                '--report-format'
                $definition.ReportFormat
                if ($definition.Types) {
                    '--type'
                    $definition.Types
                }
            )
            & $dotnetPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "EventViewerX report benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EventViewerXCli {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            [array] $lines = @(& $cliPath query `
                    --path $definition.FixturePath `
                    --max $definition.MaxEvents `
                    --oldest 2> $run.StandardOutputPath)
            if ($LASTEXITCODE -ne 0) {
                throw "EventViewerX CLI benchmark process exited with code $LASTEXITCODE."
            }
            [array] $rows = @($lines | ForEach-Object { $_ | ConvertFrom-Json })
            [ordered] @{
                Engine               = 'eventviewerxcli'
                ReadMode             = $definition.ReadMode
                FixturePath          = [IO.Path]::GetFullPath($definition.FixturePath)
                RuntimeVersion       = [Environment]::Version.ToString()
                ProductVersion       = [Diagnostics.FileVersionInfo]::GetVersionInfo($cliPath).ProductVersion
                Count                = $rows.Count
                IdSum                = 0
                RecordIdSum          = 0
                TimeTicksXor         = 0
                OrderSignature       = 0
                FirstRecordId        = $null
                LastRecordId         = $null
                MetadataTouch        = 0
                MessageCharacters    = 0
                XmlCharacters        = 0
                PropertyCount        = 0
                StructuredFieldCount = 0
                MessageFieldCount    = 0
                AttachmentBytes      = 0
                AllocatedBytes       = 0
                PeakWorkingSetBytes  = 0
                Gen0Collections      = 0
                Gen1Collections      = 0
                Gen2Collections      = 0
                ElapsedMilliseconds  = 0
                OutputPath           = $null
                OutputBytes          = [Text.Encoding]::UTF8.GetByteCount([string] ($lines -join "`n"))
                OutputSha256         = $null
            } | ConvertTo-Json | Set-Content -LiteralPath $run.ResultPath -Encoding utf8
        }
    }

    Add-BenchmarkEngine EventViewerXCliPortable {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            [array] $lines = @(& $portableCliPath query `
                    --path $definition.FixturePath `
                    --max $definition.MaxEvents `
                    --oldest 2> $run.StandardOutputPath)
            if ($LASTEXITCODE -ne 0) {
                throw "Portable EventViewerX CLI benchmark process exited with code $LASTEXITCODE."
            }
            [array] $rows = @($lines | ForEach-Object { $_ | ConvertFrom-Json })
            [ordered] @{
                Engine               = 'eventviewerxcliportable'
                ReadMode             = $definition.ReadMode
                FixturePath          = [IO.Path]::GetFullPath($definition.FixturePath)
                RuntimeVersion       = [Environment]::Version.ToString()
                ProductVersion       = [Diagnostics.FileVersionInfo]::GetVersionInfo($portableCliPath).ProductVersion
                Count                = $rows.Count
                IdSum                = 0
                RecordIdSum          = 0
                TimeTicksXor         = 0
                OrderSignature       = 0
                FirstRecordId        = $null
                LastRecordId         = $null
                MetadataTouch        = 0
                MessageCharacters    = 0
                XmlCharacters        = 0
                PropertyCount        = 0
                StructuredFieldCount = 0
                MessageFieldCount    = 0
                AttachmentBytes      = 0
                AllocatedBytes       = 0
                PeakWorkingSetBytes  = 0
                Gen0Collections      = 0
                Gen1Collections      = 0
                Gen2Collections      = 0
                ElapsedMilliseconds  = 0
                OutputPath           = $null
                OutputBytes          = [Text.Encoding]::UTF8.GetByteCount([string] ($lines -join "`n"))
                OutputSha256         = $null
            } | ConvertTo-Json | Set-Content -LiteralPath $run.ResultPath -Encoding utf8
        }
    }

    Add-BenchmarkEngine EventViewerXExport {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            & $dotnetPath $hostDll `
                --engine eventviewerxexport `
                --path $definition.FixturePath `
                --mode $definition.ReadMode `
                --format $definition.OutputFormat `
                --output-path $run.EventOutputPath `
                --culture en-US `
                --result $run.ResultPath `
                --max-events $definition.MaxEvents *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "EventViewerX direct-export benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EventViewerXBaseline {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            & $dotnetPath $baselineHostPath `
                --engine eventviewerx `
                --path $definition.FixturePath `
                --mode $definition.Workload `
                --result $run.ResultPath `
                --max-events $definition.MaxEvents *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "Baseline EventViewerX benchmark host exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine PSEventViewer {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            $readMode = if ($definition.Workload -eq 'MetadataCsv') {
                'Metadata'
            } elseif ($definition.Workload -in 'ExactRawXml', 'CommandCold') {
                $definition.ReadMode
            } else {
                $definition.Workload
            }
            [array] $arguments = @(
                '-NoLogo'
                '-NoProfile'
                '-NonInteractive'
                '-File'
                $powerShellRunner
                '-Engine'
                'PSEventViewer'
                '-Path'
                $definition.FixturePath
                '-ReadMode'
                $readMode
                '-ResultPath'
                $run.ResultPath
                '-MessageCulture'
                'en-US'
                '-ModulePath'
                $modulePath
                '-MaxEvents'
                $definition.MaxEvents
                if ($definition.Workload -eq 'MetadataCsv') {
                    '-CsvOutputPath'
                    $run.EventOutputPath
                } elseif ($definition.Workload -eq 'ExactRawXml') {
                    '-XmlOutputPath'
                    $run.EventOutputPath
                }
            )
            & $pwshPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "PSEventViewer benchmark process exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine PSEventViewerBaseline {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            $readMode = if ($definition.Workload -eq 'MetadataCsv') {
                'Metadata'
            } elseif ($definition.Workload -in 'ExactRawXml', 'CommandCold') {
                $definition.ReadMode
            } else {
                $definition.Workload
            }
            [array] $arguments = @(
                '-NoLogo'
                '-NoProfile'
                '-NonInteractive'
                '-File'
                $powerShellRunner
                '-Engine'
                'PSEventViewer'
                '-Path'
                $definition.FixturePath
                '-ReadMode'
                $readMode
                '-ResultPath'
                $run.ResultPath
                '-MessageCulture'
                'en-US'
                '-ModulePath'
                $baselineModulePath
                '-MaxEvents'
                $definition.MaxEvents
                if ($definition.Workload -eq 'MetadataCsv') {
                    '-CsvOutputPath'
                    $run.EventOutputPath
                } elseif ($definition.Workload -eq 'ExactRawXml') {
                    '-XmlOutputPath'
                    $run.EventOutputPath
                }
            )
            & $pwshPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "Baseline PSEventViewer benchmark process exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine GetWinEvent {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            $readMode = if ($definition.Workload -eq 'MetadataCsv') {
                'Metadata'
            } elseif ($definition.Workload -in 'ExactRawXml', 'CommandCold') {
                $definition.ReadMode
            } else {
                $definition.Workload
            }
            [array] $arguments = @(
                '-NoLogo'
                '-NoProfile'
                '-NonInteractive'
                '-File'
                $powerShellRunner
                '-Engine'
                'GetWinEvent'
                '-Path'
                $definition.FixturePath
                '-ReadMode'
                $readMode
                '-ResultPath'
                $run.ResultPath
                '-MessageCulture'
                'en-US'
                '-MaxEvents'
                $definition.MaxEvents
                if ($definition.Workload -eq 'MetadataCsv') {
                    '-CsvOutputPath'
                    $run.EventOutputPath
                } elseif ($definition.Workload -eq 'ExactRawXml') {
                    '-XmlOutputPath'
                    $run.EventOutputPath
                }
            )
            & $pwshPath @arguments *> $run.StandardOutputPath
            if ($LASTEXITCODE -ne 0) {
                throw "Get-WinEvent benchmark process exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkEngine EvtxECmd {
        Add-BenchmarkOperation Scan {
            param($case, $run)

            $definition = $run.Definition
            $workload = switch ($definition.Workload) {
                'EvtxForensicCsv' { 'ForensicCsv' }
                'NativeOutputCsv' { 'ForensicCsv' }
                'EvtxFullJson' { 'FullJson' }
                'NativeOutputFullJson' { 'FullJson' }
                'EvtxXml' { 'Xml' }
                'NativeOutputXml' { 'Xml' }
                default { 'NativeParse' }
            }
            [array] $arguments = @(
                '-NoLogo'
                '-NoProfile'
                '-NonInteractive'
                '-File'
                $evtxRunner
                '-ExecutablePath'
                $evtxECmdPath
                '-MapsPath'
                $evtxMapsPath
                '-Path'
                $definition.FixturePath
                '-ResultPath'
                $run.ResultPath
                '-StandardOutputPath'
                $run.StandardOutputPath
                '-Workload'
                $workload
                if ($run.EventOutputPath) {
                    '-OutputPath'
                    $run.EventOutputPath
                }
            )
            & $pwshPath @arguments
            if ($LASTEXITCODE -ne 0) {
                throw "EvtxECmd benchmark process exited with code $LASTEXITCODE."
            }
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)

        $definition = $run.Definition
        Assert-BenchmarkPath $run.ResultPath
        $run.Result = Get-Content -LiteralPath $run.ResultPath -Raw | ConvertFrom-Json
        if ($definition.ExpectedCount -gt 0) {
            Assert-BenchmarkValue -Actual ([long] $run.Result.Count) -Expected ([long] $definition.ExpectedCount) -Message 'Every engine must process the expected event count.'
        } elseif ([long] $run.Result.Count -le 0) {
            throw 'The benchmark engine did not process any events.'
        }
        if ($run.Result.PSObject.Properties.Name -contains 'Errors') {
            Assert-BenchmarkValue -Actual ([long] $run.Result.Errors) -Expected ([long] 0) -Message 'The parser must not report EVTX errors.'
        }

        $requiresOutput = $definition.Workload -eq 'MetadataCsv' -or
            $definition.Workload -eq 'ExactRawXml' -or
            ($definition.Workload -like 'Evtx*' -and $definition.Workload -ne 'EvtxNativeParse') -or
            $definition.Workload -like 'NativeOutput*' -or
            $definition.Workload -like '*Report*'
        if ($requiresOutput) {
            Assert-BenchmarkPath $run.EventOutputPath
            $outputFile = Get-Item -LiteralPath $run.EventOutputPath
            if ($outputFile.Length -le 0) {
                throw "The $($definition.Workload) benchmark did not produce a non-empty output file."
            }
            if ($definition.Workload -notlike '*Report*') {
                $run.Result.OutputBytes = [long] $outputFile.Length
            }
            $run.Result.OutputSha256 = [string] (Get-FileHash -LiteralPath $outputFile.FullName -Algorithm SHA256).Hash
            [ordered] @{
                FileName = $outputFile.Name
                Bytes    = [long] $run.Result.OutputBytes
                Sha256   = [string] $run.Result.OutputSha256
            } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.OutputDirectory 'output-validation.json') -Encoding utf8
        }

        if ($definition.Workload -in 'Metadata', 'Message', 'StructuredData', 'StructuredDataAndMessage', 'Full', 'MetadataCsv') {
            if ([long] $run.Result.IdSum -le 0 -or [long] $run.Result.RecordIdSum -le 0) {
                throw 'The common-work benchmark did not record non-empty event identity checks.'
            }
            if ($null -eq $run.Result.FirstRecordId -or $null -eq $run.Result.LastRecordId) {
                throw 'The common-work benchmark did not record its first and last record IDs.'
            }

            $readMode = if ($definition.Workload -eq 'MetadataCsv') { 'Metadata' } else { $definition.Workload }
            $identityKey = '{0}|{1}' -f $definition.Fixture, $definition.MaxEvents
            $identitySignature = '{0}|{1}|{2}|{3}|{4}|{5}|{6}' -f
                [long] $run.Result.Count,
                [long] $run.Result.IdSum,
                [long] $run.Result.RecordIdSum,
                [long] $run.Result.TimeTicksXor,
                [long] $run.Result.OrderSignature,
                [long] $run.Result.FirstRecordId,
                [long] $run.Result.LastRecordId
            if ($commonIdentitySignatures.ContainsKey($identityKey)) {
                Assert-BenchmarkValue -Actual $identitySignature -Expected $commonIdentitySignatures[$identityKey] -Message 'Common-work engines must process the same ordered event identity set.'
            } else {
                $commonIdentitySignatures[$identityKey] = $identitySignature
            }

            if ($definition.Workload -eq 'MetadataCsv') {
                $outputKey = '{0}|{1}' -f $definition.Fixture, $definition.ExpectedCount
                $outputHash = [string] $run.Result.OutputSha256
                if ($exactOutputHashes.ContainsKey($outputKey)) {
                    Assert-BenchmarkValue -Actual $outputHash -Expected $exactOutputHashes[$outputKey] -Message 'Exact-output engines must produce a byte-identical metadata CSV.'
                } else {
                    $exactOutputHashes[$outputKey] = $outputHash
                }
            }

            if ($readMode -eq 'Metadata') {
                Assert-BenchmarkValue -Actual ([long] $run.Result.MessageCharacters) -Expected ([long] 0) -Message 'Metadata mode must not format messages.'
                Assert-BenchmarkValue -Actual ([long] $run.Result.XmlCharacters) -Expected ([long] 0) -Message 'Metadata mode must not materialize XML.'
                Assert-BenchmarkValue -Actual ([long] $run.Result.PropertyCount) -Expected ([long] 0) -Message 'Metadata mode must not materialize event properties.'
            } elseif ($readMode -eq 'Message') {
                if ([long] $run.Result.MessageCharacters -le 0) {
                    throw 'Message mode did not format any provider messages.'
                }
                Assert-BenchmarkValue -Actual ([long] $run.Result.XmlCharacters) -Expected ([long] 0) -Message 'Message mode must not materialize XML.'
                Assert-BenchmarkValue -Actual ([long] $run.Result.PropertyCount) -Expected ([long] 0) -Message 'Message mode must not materialize event properties.'
            } elseif ($readMode -eq 'StructuredData') {
                Assert-BenchmarkValue -Actual ([long] $run.Result.MessageCharacters) -Expected ([long] 0) -Message 'StructuredData mode must not format messages.'
                if ([long] $run.Result.XmlCharacters -le 0 -or [long] $run.Result.PropertyCount -le 0) {
                    throw 'StructuredData mode did not materialize XML and event properties.'
                }
            } elseif (($readMode -in @('StructuredDataAndMessage', 'Full')) -and (
                [long] $run.Result.MessageCharacters -le 0 -or
                [long] $run.Result.XmlCharacters -le 0 -or
                [long] $run.Result.PropertyCount -le 0)) {
                throw "$readMode mode did not format messages or materialize XML and event properties."
            }
        }

        if ($definition.Workload -eq 'ExactRawXml') {
            $outputKey = '{0}|{1}|RawXml' -f $definition.Fixture, $definition.ExpectedCount
            $outputHash = [string] $run.Result.OutputSha256
            if ($exactOutputHashes.ContainsKey($outputKey)) {
                Assert-BenchmarkValue -Actual $outputHash -Expected $exactOutputHashes[$outputKey] -Message 'Exact-output engines must produce byte-identical raw XML.'
            } else {
                $exactOutputHashes[$outputKey] = $outputHash
            }
        }

        # Keep the validated size/hash sidecar and metrics, not repeated multi-gigabyte
        # payloads. KeepOnFailure still preserves the output when an assertion above fails.
        if ($requiresOutput -and (Test-Path -LiteralPath $run.EventOutputPath -PathType Leaf)) {
            Remove-Item -LiteralPath $run.EventOutputPath -Force
        }
        if ($definition.Workload -like '*Report*' -and $run.EventOutputPath) {
            foreach ($extension in '.xlsx', '.email.html', '.email.txt', '.txt') {
                $sidecarPath = [IO.Path]::ChangeExtension($run.EventOutputPath, $extension)
                if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
                    Remove-Item -LiteralPath $sidecarPath -Force
                }
            }
        }
    }

    Add-BenchmarkMetric Events {
        param($case, $run)
        [long] $run.Result.Count
    }

    Add-BenchmarkMetric EventsPerSecond {
        param($case, $run)
        if ([double] $run.Result.ElapsedMilliseconds -le 0) {
            0
        } else {
            [math]::Round(([double] $run.Result.Count * 1000) / [double] $run.Result.ElapsedMilliseconds, 3)
        }
    }

    Add-BenchmarkMetric InternalMs {
        param($case, $run)
        [double] $run.Result.ElapsedMilliseconds
    }

    Add-BenchmarkMetric IdSum {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'IdSum') {
            [long] $run.Result.IdSum
        } else {
            0
        }
    }

    Add-BenchmarkMetric RecordIdSum {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'RecordIdSum') {
            [long] $run.Result.RecordIdSum
        } else {
            0
        }
    }

    Add-BenchmarkMetric OrderSignature {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'OrderSignature') {
            [long] $run.Result.OrderSignature
        } else {
            0
        }
    }

    Add-BenchmarkMetric FirstRecordId {
        param($case, $run)
        if ($null -ne $run.Result.FirstRecordId) {
            [long] $run.Result.FirstRecordId
        } else {
            0
        }
    }

    Add-BenchmarkMetric LastRecordId {
        param($case, $run)
        if ($null -ne $run.Result.LastRecordId) {
            [long] $run.Result.LastRecordId
        } else {
            0
        }
    }

    Add-BenchmarkMetric EngineAllocatedBytes {
        param($case, $run)
        [long] $run.Result.AllocatedBytes
    }

    Add-BenchmarkMetric EnginePeakWorkingSetBytes {
        param($case, $run)
        [long] $run.Result.PeakWorkingSetBytes
    }

    Add-BenchmarkMetric MessageCharacters {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'MessageCharacters') {
            [long] $run.Result.MessageCharacters
        } else {
            0
        }
    }

    Add-BenchmarkMetric XmlCharacters {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'XmlCharacters') {
            [long] $run.Result.XmlCharacters
        } else {
            0
        }
    }

    Add-BenchmarkMetric PropertyCount {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'PropertyCount') {
            [long] $run.Result.PropertyCount
        } else {
            0
        }
    }

    Add-BenchmarkMetric StructuredFieldCount {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'StructuredFieldCount') {
            [long] $run.Result.StructuredFieldCount
        } else {
            0
        }
    }

    Add-BenchmarkMetric MessageFieldCount {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'MessageFieldCount') {
            [long] $run.Result.MessageFieldCount
        } else {
            0
        }
    }

    Add-BenchmarkMetric AttachmentBytes {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'AttachmentBytes') {
            [long] $run.Result.AttachmentBytes
        } else {
            0
        }
    }

    Add-BenchmarkMetric OutputBytes {
        param($case, $run)
        if ($run.Result.PSObject.Properties.Name -contains 'OutputBytes') {
            [long] $run.Result.OutputBytes
        } else {
            0
        }
    }

    if ($readmeTable -in 'Common', 'Scale', 'ExactOutput') {
        Add-BenchmarkComparison Engine -Baseline PSEventViewer -Metric MedianMs -TieTolerance 0.03
        if ($readmeTable -eq 'ExactOutput') {
            Add-BenchmarkComparison Engine -Baseline PSEventViewer -Metric OutputBytes
        }
    } elseif ($readmeTable -eq 'ColdStart') {
        Add-BenchmarkComparison Engine -Baseline EventViewerXCli -Metric MedianMs -TieTolerance 0.03
    } elseif ($readmeTable -eq 'Reporting') {
        Add-BenchmarkComparison Engine -Baseline EventViewerXReport -Metric MedianMs -TieTolerance 0.03
    } elseif ($readmeTable -eq 'NativeOutput') {
        Add-BenchmarkComparison Engine -Baseline EventViewerXExport -Metric MedianMs -TieTolerance 0.03
        Add-BenchmarkComparison Engine -Baseline EventViewerXExport -Metric OutputBytes
    } elseif ($readmeTable -eq 'EvtxNative') {
        Add-BenchmarkComparison Engine -Baseline EvtxECmd -Metric MedianMs -TieTolerance 0.03
        Add-BenchmarkComparison Engine -Baseline EvtxECmd -Metric OutputBytes
    }
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
