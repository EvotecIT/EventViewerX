$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$eventViewerXPath = Get-BenchmarkInput -Name EventViewerXPath -Default (Join-Path $repositoryRoot 'Sources\EventViewerX\bin\Release\net10.0-windows\EventViewerX.dll')
$targetsJson = Get-BenchmarkInput -Name TargetsJson
$logName = Get-BenchmarkInput -Name LogName -Default Security
$sampleCountsText = Get-BenchmarkInput -Name SampleCounts -Default '100,1000'
$maxConcurrency = Get-BenchmarkInput -Name MaxConcurrency -Int -Default 8
$remoteConnectionTimeoutMilliseconds = Get-BenchmarkInput -Name RemoteConnectionTimeoutMilliseconds -Int -Default 5000
$remoteReadTimeoutMilliseconds = Get-BenchmarkInput -Name RemoteReadTimeoutMilliseconds -Int -Default 30000

[array] $targets = @($targetsJson | ConvertFrom-Json)
if ($targets.Count -lt 2) {
    throw 'Remote fan-out requires at least two target machines.'
}
[int[]] $sampleCounts = @($sampleCountsText.Split(',') | ForEach-Object {
        [int] $value = 0
        if (-not [int]::TryParse($_.Trim(), [ref] $value) -or $value -le 0) {
            throw "SampleCounts must contain positive 32-bit values. Received '$($_)'."
        }
        $value
    } | Sort-Object -Unique)

$coreHash = (Get-FileHash -LiteralPath $eventViewerXPath -Algorithm SHA256).Hash
$identitySignatures = @{}

function Invoke-EventSourceFanOut {
    param(
        [Parameter(Mandatory)] $Case,
        [Parameter(Mandatory)] $Run,
        [Parameter(Mandatory)] [int] $Concurrency
    )

    [array] $queries = foreach ($target in $targets) {
        $query = [EventViewerX.EventLogChannelQuery]::new($Case.LogName)
        $query.MachineName = [string] $target.MachineName
        $query.XPath = "*[System[EventRecordID <= $([long] $target.MaximumRecordId)]]"
        $query.ReadMode = [EventViewerX.EventReadMode]::Metadata
        $query.MaxEvents = $Case.EventsPerMachine
        $query.RemoteConnectionTimeoutMilliseconds = $remoteConnectionTimeoutMilliseconds
        $query.RemoteReadTimeoutMilliseconds = $remoteReadTimeoutMilliseconds
        $query
    }
    $batch = [EventViewerX.EventLogBatchQuery]::ForChannels(
        [EventViewerX.EventLogChannelQuery[]] $queries)
    $batch.MaxConcurrency = $Concurrency

    $counts = @{}
    $recordIdSums = @{}
    [long] $totalCount = 0
    [long] $orderSignature = 0
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    foreach ($eventRecord in [EventViewerX.EventLogBatchEngine]::Read($batch)) {
        $machine = [string] $eventRecord.QueriedMachine
        if (-not $counts.ContainsKey($machine)) {
            $counts[$machine] = [long] 0
            $recordIdSums[$machine] = [long] 0
        }
        [long] $recordId = if ($null -ne $eventRecord.RecordId) { $eventRecord.RecordId } else { 0 }
        $counts[$machine] = [long] $counts[$machine] + 1
        $recordIdSums[$machine] = [long] $recordIdSums[$machine] + $recordId
        $totalCount++
        $orderSignature = (($orderSignature * 16777619) + $recordId) % 1000000007
        $null = $eventRecord.Id
        $null = $eventRecord.ProviderName
        $null = $eventRecord.MachineName
        $null = $eventRecord.LogName
    }
    $stopwatch.Stop()

    [array] $sourceSignatures = foreach ($machine in $counts.Keys | Sort-Object) {
        '{0}:{1}:{2}' -f $machine.ToUpperInvariant(), $counts[$machine], $recordIdSums[$machine]
    }
    $Run.Result = [pscustomobject]@{
        TotalCount = $totalCount
        SourceCount = $counts.Count
        SourceSignatures = $sourceSignatures -join '|'
        OrderSignature = $orderSignature
        ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
    }
}

New-BenchmarkSuite 'event-source-fanout' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventSources\FanOut') {
    Add-BenchmarkCaseSource @($sampleCounts | ForEach-Object {
            [pscustomobject]@{
                Name = "FanOut-$($targets.Count)x$_-$logName"
                LogName = $logName
                EventsPerMachine = $_
            }
        })
    Set-BenchmarkPolicy -Warmup 0 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata EventViewerXSha256 $coreHash
    Add-BenchmarkMetadata Contract 'Same fixed per-machine record boundaries, exact per-machine counts, and deterministic merged identity set'
    Add-BenchmarkMetadata TargetMachines (($targets.MachineName | Sort-Object) -join ',')
    Add-BenchmarkMetadata TargetLog $logName

    Add-BenchmarkEngine Sequential {
        Add-BenchmarkOperation Query {
            param($case, $run)
            Invoke-EventSourceFanOut -Case $case -Run $run -Concurrency 1
        }
    }
    Add-BenchmarkEngine Parallel {
        Add-BenchmarkOperation Query {
            param($case, $run)
            Invoke-EventSourceFanOut -Case $case -Run $run -Concurrency ([Math]::Min($maxConcurrency, $targets.Count))
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)

        [long] $expectedCount = [long] $case.EventsPerMachine * $targets.Count
        Assert-BenchmarkValue -Actual ([long] $run.Result.TotalCount) -Expected $expectedCount -Message 'Fan-out must return the requested count from every target.'
        Assert-BenchmarkValue -Actual ([int] $run.Result.SourceCount) -Expected $targets.Count -Message 'Fan-out must return records from every target machine.'
        $signature = '{0}|{1}|{2}|{3}' -f
            $run.Result.TotalCount,
            $run.Result.SourceCount,
            $run.Result.SourceSignatures,
            $run.Result.OrderSignature
        if ($identitySignatures.ContainsKey($case.Scenario)) {
            Assert-BenchmarkValue -Actual $signature -Expected $identitySignatures[$case.Scenario] -Message 'Sequential and parallel fan-out must return the same merged identity set.'
        } else {
            $identitySignatures[$case.Scenario] = $signature
        }
    }

    Add-BenchmarkMetric EventsPerSecond {
        param($case, $run)
        [Math]::Round($run.Result.TotalCount / ($run.Result.ElapsedMilliseconds / 1000), 2)
    }
    Add-BenchmarkMetric Events { param($case, $run) [long] $run.Result.TotalCount }
    Add-BenchmarkMetric Sources { param($case, $run) [int] $run.Result.SourceCount }
    Add-BenchmarkMetric OrderSignature { param($case, $run) [long] $run.Result.OrderSignature }
    Add-BenchmarkComparison Engine -Baseline Sequential -Metric MedianMs -TieTolerance 0.05
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
