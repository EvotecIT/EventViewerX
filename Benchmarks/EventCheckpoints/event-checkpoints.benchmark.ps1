$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$counts = (Get-BenchmarkInput -Name CheckpointCounts -Default '100,1000,10000').Split(',')
$workRoot = Get-BenchmarkInput -Name WorkRoot -Default (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventCheckpoints\work')

New-BenchmarkSuite 'event-checkpoints' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventCheckpoints') {
    Add-BenchmarkCaseSource @(foreach ($text in $counts) {
            [int] $count = 0
            if (-not [int]::TryParse($text.Trim(), [ref] $count) -or $count -le 0) {
                throw "CheckpointCounts must contain positive 32-bit values. Received '$text'."
            }
            [pscustomobject]@{ Name = "Checkpoints-$count"; CheckpointCount = $count }
        })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract 'Ten reads of the final Unicode case-insensitive identity; database population outside measured operation'
    Set-BenchmarkSetup {
        param($case, $run)
        $run.Fixture = [EventViewerX.Benchmarks.EventCheckpointBenchmarkFixture]::new($case.CheckpointCount, $workRoot)
    }
    Add-BenchmarkEngine Retained {
        Add-BenchmarkOperation Lookup {
            param($case, $run)
            try { $run.Result = $run.Fixture.ReadRetained() } finally { $run.Fixture.Dispose() }
        }
    }
    Add-BenchmarkEngine Reopened {
        Add-BenchmarkOperation Lookup {
            param($case, $run)
            try { $run.Result = $run.Fixture.ReadReopened() } finally { $run.Fixture.Dispose() }
        }
    }
    Add-BenchmarkValidation {
        param($case, $run)
        Assert-BenchmarkValue -Actual $run.Result.Checksum -Expected ([long] $case.CheckpointCount * 10) -Message 'Every read must preserve the Unicode checkpoint identity and record id.'
        $run.Fixture = $null
    }
    Add-BenchmarkMetric LookupMs { param($case, $run) $run.Result.LookupMs }
    Add-BenchmarkMetric EngineAllocatedBytes { param($case, $run) $run.Result.AllocatedBytes }
    Add-BenchmarkComparison Engine -Baseline Retained -Metric MedianMs -TieTolerance 0.03
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
