$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$counts = (Get-BenchmarkInput -Name EventCounts -Default '1000,10000,100000').Split(',')
$workRoot = Get-BenchmarkInput -Name WorkRoot -Default (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventReportStreaming\work')

New-BenchmarkSuite 'event-report-streaming' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventReportStreaming') {
    Add-BenchmarkCaseSource @(foreach ($text in $counts) {
            [int] $count = 0
            if (-not [int]::TryParse($text.Trim(), [ref] $count) -or $count -le 0) {
                throw "EventCounts must contain positive 32-bit values. Received '$text'."
            }
            [pscustomobject]@{ Name = "Rows-$count"; EventCount = $count }
        })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract 'Identical lazy source, normalized payload, identity, order, count and record-id checksum; no JSON serialization or EVTX parsing'

    Set-BenchmarkSetup {
        param($case, $run)
        $run.Fixture = [EventViewerX.Benchmarks.EventReportStreamingBenchmarkFixture]::new($case.EventCount, $workRoot)
    }
    Add-BenchmarkEngine Snapshot {
        Add-BenchmarkOperation Read {
            param($case, $run)
            try { $run.Result = $run.Fixture.RunSnapshot() } finally { $run.Fixture.Dispose() }
        }
    }
    Add-BenchmarkEngine Stream {
        Add-BenchmarkOperation Read {
            param($case, $run)
            try { $run.Result = $run.Fixture.RunStream() } finally { $run.Fixture.Dispose() }
        }
    }
    Add-BenchmarkValidation {
        param($case, $run)
        Assert-BenchmarkValue -Actual $run.Result.Rows -Expected ([long] $case.EventCount) -Message 'Every source row must be emitted.'
        Assert-BenchmarkValue -Actual $run.Result.Checksum -Expected ([long] $case.EventCount * ($case.EventCount + 1) / 2) -Message 'Record identities must be preserved.'
        Assert-BenchmarkValue -Actual $run.Result.IsComplete -Expected $true -Message 'Input must be exhausted without loss or a limit.'
        $run.Fixture.Dispose()
        $run.Fixture = $null
    }
    Add-BenchmarkMetric FirstRowMs { param($case, $run) $run.Result.FirstRowMs }
    Add-BenchmarkMetric QueryMs { param($case, $run) $run.Result.QueryMs }
    Add-BenchmarkMetric ReadAtFirstRow { param($case, $run) $run.Result.ReadAtFirstRow }
    Add-BenchmarkMetric EngineAllocatedBytes { param($case, $run) $run.Result.AllocatedBytes }
    Add-BenchmarkMetric RetainedManagedBytes { param($case, $run) $run.Result.RetainedManagedBytes }
    Add-BenchmarkComparison Engine -Baseline Snapshot -Metric MedianMs -TieTolerance 0.03
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
