$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$eventCountsText = Get-BenchmarkInput -Name EventCounts -Default '1000,10000'
[int[]] $eventCounts = @($eventCountsText.Split(',') | ForEach-Object {
        [int] $value = 0
        if (-not [int]::TryParse($_.Trim(), [ref] $value) -or $value -le 0) {
            throw "EventCounts must contain positive 32-bit values. Received '$($_)'."
        }
        $value
    } | Sort-Object -Unique)

New-BenchmarkSuite 'event-projection' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventProjection') {
    Add-BenchmarkCaseSource @($eventCounts | ForEach-Object {
            [pscustomobject]@{
                Name = "Authentication-$_"
                EventCount = $_
            }
        })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract 'Identical specialized typed results; only selection-plan reuse differs'

    Set-BenchmarkSetup {
        param($case, $run)
        $run.State = [EventViewerX.Benchmarks.EventProjectionBenchmarkFixture]::Create($case.EventCount)
    }

    Add-BenchmarkEngine CompilePerEvent {
        Add-BenchmarkOperation Project {
            param($case, $run)
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            $run.Result = [EventViewerX.Benchmarks.EventProjectionBenchmarkFixture]::RunCompilePerEvent($run.State)
            $stopwatch.Stop()
            $run.ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
        }
    }

    Add-BenchmarkEngine ReusablePlan {
        Add-BenchmarkOperation Project {
            param($case, $run)
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            $run.Result = [EventViewerX.Benchmarks.EventProjectionBenchmarkFixture]::RunReusablePlan($run.State)
            $stopwatch.Stop()
            $run.ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)
        Assert-BenchmarkValue -Actual ([int] $run.Result.ProjectedCount) -Expected ([int] $case.EventCount) -Message 'Every input must produce one typed event.'
        Assert-BenchmarkValue -Actual ([int] $run.Result.Checksum) -Expected ([int] $run.State.ExpectedChecksum) -Message 'Both paths must produce identical typed results.'
        Assert-BenchmarkValue -Actual ([string] $run.Result.ProjectedType) -Expected ([string] $run.State.ExpectedType) -Message 'Specific rule selection must remain identical.'
        $run.ProjectedCount = $run.Result.ProjectedCount
        $run.ProjectedType = $run.Result.ProjectedType
        $run.AllocatedBytes = $run.Result.AllocatedBytes
        $run.Result = $null
        $run.State = $null
    }

    Add-BenchmarkMetric EventsPerSecond {
        param($case, $run)
        [Math]::Round($case.EventCount / ($run.ElapsedMilliseconds / 1000), 2)
    }
    Add-BenchmarkMetric ProjectedEvents { param($case, $run) [int] $run.ProjectedCount }
    Add-BenchmarkMetric ProjectedType { param($case, $run) [string] $run.ProjectedType }
    Add-BenchmarkMetric ProjectionAllocatedBytes { param($case, $run) [long] $run.AllocatedBytes }
    Add-BenchmarkMetric BytesPerEvent {
        param($case, $run)
        [Math]::Round($run.AllocatedBytes / $case.EventCount, 2)
    }
    Add-BenchmarkComparison Engine -Baseline CompilePerEvent -Metric MedianMs -TieTolerance 0.03
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
