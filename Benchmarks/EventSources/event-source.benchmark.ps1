$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$eventViewerXPath = Get-BenchmarkInput -Name EventViewerXPath -Default (Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll')
$machineName = Get-BenchmarkInput -Name MachineName
$logName = Get-BenchmarkInput -Name LogName -Default Security
$maximumRecordId = Get-BenchmarkInput -Name MaximumRecordId -Int -Default 0
$sampleCountsText = Get-BenchmarkInput -Name SampleCounts -Default '100,1000'
$remoteConnectionTimeoutMilliseconds = Get-BenchmarkInput -Name RemoteConnectionTimeoutMilliseconds -Int -Default 5000
$remoteReadTimeoutMilliseconds = Get-BenchmarkInput -Name RemoteReadTimeoutMilliseconds -Int -Default 30000
[int[]] $sampleCounts = @($sampleCountsText.Split(',') | ForEach-Object {
        [int] $value = 0
        if (-not [int]::TryParse($_.Trim(), [ref] $value) -or $value -le 0) {
            throw "SampleCounts must contain positive 32-bit values. Received '$($_)'."
        }
        $value
    } | Sort-Object -Unique)

$coreHash = (Get-FileHash -LiteralPath $eventViewerXPath -Algorithm SHA256).Hash
$identitySignatures = @{}
$boundaryXPath = "*[System[EventRecordID <= $maximumRecordId]]"

New-BenchmarkSuite 'event-source-query' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventSources') {
    Add-BenchmarkCaseSource @($sampleCounts | ForEach-Object {
            [pscustomobject]@{
                Name = "Remote-$($machineName)-$logName-Latest-$_-Metadata"
                MachineName = $machineName
                LogName = $logName
                EventCount = $_
            }
        })
    Set-BenchmarkPolicy -Warmup 0 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata EventViewerXSha256 $coreHash
    Add-BenchmarkMetadata Contract 'Same channel, fixed latest-record boundary, metadata window, and ordered record identity set'
    Add-BenchmarkMetadata TargetMachine ([string] $machineName)
    Add-BenchmarkMetadata TargetLog $logName
    Add-BenchmarkMetadata MaximumRecordId ([string] $maximumRecordId)

    Add-BenchmarkEngine EventViewerX {
        Add-BenchmarkOperation Query {
            param($case, $run)

            $query = [EventViewerX.EventLogChannelQuery]::new($case.LogName)
            $query.MachineName = $case.MachineName
            $query.XPath = $boundaryXPath
            $query.ReadMode = [EventViewerX.EventReadMode]::Metadata
            $query.MaxEvents = $case.EventCount
            $query.RemoteConnectionTimeoutMilliseconds = $remoteConnectionTimeoutMilliseconds
            $query.RemoteReadTimeoutMilliseconds = $remoteReadTimeoutMilliseconds
            [long] $count = 0
            [long] $idSum = 0
            [long] $recordIdSum = 0
            [long] $timeTicksXor = 0
            [long] $orderSignature = 0
            [long] $firstRecordId = 0
            [long] $lastRecordId = 0
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            foreach ($eventRecord in [EventViewerX.EventLogEngine]::ReadChannel($query)) {
                [long] $recordId = if ($null -ne $eventRecord.RecordId) { $eventRecord.RecordId } else { 0 }
                [long] $ticks = if ($null -ne $eventRecord.TimeCreated) { $eventRecord.TimeCreated.Ticks } else { 0 }
                $count++
                $idSum += $eventRecord.Id
                $recordIdSum += $recordId
                $timeTicksXor = $timeTicksXor -bxor $ticks
                $orderSignature = (($orderSignature * 16777619) + $recordId) % 1000000007
                if ($count -eq 1) { $firstRecordId = $recordId }
                $lastRecordId = $recordId
                $null = $eventRecord.ProviderName
                $null = $eventRecord.MachineName
                $null = $eventRecord.LogName
            }
            $stopwatch.Stop()
            $run.Result = [pscustomobject]@{
                Count = $count; IdSum = $idSum; RecordIdSum = $recordIdSum
                TimeTicksXor = $timeTicksXor; OrderSignature = $orderSignature
                FirstRecordId = $firstRecordId; LastRecordId = $lastRecordId
                ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }
        }
    }

    Add-BenchmarkEngine PSEventViewer {
        Add-BenchmarkOperation Query {
            param($case, $run)

            [long] $count = 0
            [long] $idSum = 0
            [long] $recordIdSum = 0
            [long] $timeTicksXor = 0
            [long] $orderSignature = 0
            [long] $firstRecordId = 0
            [long] $lastRecordId = 0
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            Get-EVXEvent -LogName $case.LogName -MachineName $case.MachineName -FilterXPath $boundaryXPath -ReadMode Metadata -MaxEvents $case.EventCount `
                -SessionTimeoutMs $remoteReadTimeoutMilliseconds |
                ForEach-Object {
                    [long] $recordId = if ($null -ne $_.RecordId) { $_.RecordId } else { 0 }
                    [long] $ticks = if ($null -ne $_.TimeCreated) { $_.TimeCreated.Ticks } else { 0 }
                    $count++
                    $idSum += $_.Id
                    $recordIdSum += $recordId
                    $timeTicksXor = $timeTicksXor -bxor $ticks
                    $orderSignature = (($orderSignature * 16777619) + $recordId) % 1000000007
                    if ($count -eq 1) { $firstRecordId = $recordId }
                    $lastRecordId = $recordId
                    $null = $_.ProviderName
                    $null = $_.MachineName
                    $null = $_.LogName
                }
            $stopwatch.Stop()
            $run.Result = [pscustomobject]@{
                Count = $count; IdSum = $idSum; RecordIdSum = $recordIdSum
                TimeTicksXor = $timeTicksXor; OrderSignature = $orderSignature
                FirstRecordId = $firstRecordId; LastRecordId = $lastRecordId
                ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }
        }
    }

    Add-BenchmarkEngine GetWinEvent {
        Add-BenchmarkOperation Query {
            param($case, $run)

            [long] $count = 0
            [long] $idSum = 0
            [long] $recordIdSum = 0
            [long] $timeTicksXor = 0
            [long] $orderSignature = 0
            [long] $firstRecordId = 0
            [long] $lastRecordId = 0
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            Get-WinEvent -ComputerName $case.MachineName -LogName $case.LogName -FilterXPath $boundaryXPath -MaxEvents $case.EventCount |
                ForEach-Object {
                    [long] $recordId = if ($null -ne $_.RecordId) { $_.RecordId } else { 0 }
                    [long] $ticks = if ($null -ne $_.TimeCreated) { $_.TimeCreated.Ticks } else { 0 }
                    $count++
                    $idSum += $_.Id
                    $recordIdSum += $recordId
                    $timeTicksXor = $timeTicksXor -bxor $ticks
                    $orderSignature = (($orderSignature * 16777619) + $recordId) % 1000000007
                    if ($count -eq 1) { $firstRecordId = $recordId }
                    $lastRecordId = $recordId
                    $null = $_.ProviderName
                    $null = $_.MachineName
                    $null = $_.LogName
                }
            $stopwatch.Stop()
            $run.Result = [pscustomobject]@{
                Count = $count; IdSum = $idSum; RecordIdSum = $recordIdSum
                TimeTicksXor = $timeTicksXor; OrderSignature = $orderSignature
                FirstRecordId = $firstRecordId; LastRecordId = $lastRecordId
                ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)

        Assert-BenchmarkValue -Actual ([long] $run.Result.Count) -Expected ([long] $case.EventCount) -Message 'Every engine must return the requested remote event count.'
        if ($run.Result.RecordIdSum -le 0 -or $run.Result.FirstRecordId -le 0 -or $run.Result.LastRecordId -le 0) {
            throw 'The remote benchmark did not capture a usable ordered record identity signature.'
        }
        $signature = '{0}|{1}|{2}|{3}|{4}|{5}|{6}' -f
            $run.Result.Count, $run.Result.IdSum, $run.Result.RecordIdSum,
            $run.Result.TimeTicksXor, $run.Result.OrderSignature,
            $run.Result.FirstRecordId, $run.Result.LastRecordId
        if ($identitySignatures.ContainsKey($case.Scenario)) {
            Assert-BenchmarkValue -Actual $signature -Expected $identitySignatures[$case.Scenario] -Message 'Every engine must return the same ordered remote event identity set.'
        } else {
            $identitySignatures[$case.Scenario] = $signature
        }
    }

    Add-BenchmarkMetric EventsPerSecond {
        param($case, $run)
        [Math]::Round($run.Result.Count / ($run.Result.ElapsedMilliseconds / 1000), 2)
    }
    Add-BenchmarkMetric Events { param($case, $run) [long] $run.Result.Count }
    Add-BenchmarkMetric RecordIdSum { param($case, $run) [long] $run.Result.RecordIdSum }
    Add-BenchmarkMetric OrderSignature { param($case, $run) [long] $run.Result.OrderSignature }
    Add-BenchmarkComparison Engine -Baseline PSEventViewer -Metric MedianMs -TieTolerance 0.05
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
