$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$rowCountsText = Get-BenchmarkInput -Name RowCounts -Default '1000,10000'
[int[]] $rowCounts = @($rowCountsText.Split(',') | ForEach-Object {
        [int] $value = 0
        if (-not [int]::TryParse($_.Trim(), [ref] $value) -or $value -le 0 -or $value % 10 -ne 0) {
            throw "RowCounts must contain positive multiples of ten. Received '$($_)'."
        }
        $value
    } | Sort-Object -Unique)

New-BenchmarkSuite 'event-store' -OutputRoot (Join-Path $repositoryRoot 'Ignore\Benchmarks\EventStore') {
    Add-BenchmarkCaseSource @(foreach ($rowCount in $rowCounts) {
            foreach ($operation in 'Write', 'SqlQuery', 'ManagedQuery', 'DailySummary', 'TypedCsv') {
                [pscustomobject]@{
                    Name = "$operation-$rowCount"
                    Workload = $operation
                    RowCount = $rowCount
                }
            }
        })
    Set-BenchmarkPolicy -Warmup 1 -Iterations 3 -Order Rotated -OutlierMode None
    Set-BenchmarkProfile Current -Cleanup Always
    Add-BenchmarkMetadata Contract 'Identical normalized typed rows, exact result counts, and reusable EventViewerX public APIs'

    Set-BenchmarkSetup {
        param($case, $run)

        $run.WorkRoot = Join-Path ([IO.Path]::GetTempPath()) "evx-store-benchmark-$([Guid]::NewGuid().ToString('N'))"
        [IO.Directory]::CreateDirectory($run.WorkRoot) | Out-Null
        $run.StorePath = Join-Path $run.WorkRoot 'events.db'
        $run.CsvPath = Join-Path $run.WorkRoot 'events.csv'
        $run.Report = [EventStoreBenchmarkFixture]::CreateReport($case.RowCount)
        $run.Store = [EventViewerX.Storage.EventStore]::new($run.StorePath)
        if ($case.Workload -ne 'Write') {
            $run.Store.WriteAsync($run.Report).GetAwaiter().GetResult() | Out-Null
        }
    }

    Add-BenchmarkEngine EventViewerXStorage {
        Add-BenchmarkOperation Execute {
            param($case, $run)

            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            if ($case.Workload -eq 'Write') {
                $result = $run.Store.WriteAsync($run.Report).GetAwaiter().GetResult()
                $run.ResultCount = $result.Inserted
                $run.WorkItems = $case.RowCount
            } elseif ($case.Workload -eq 'SqlQuery') {
                $query = [EventViewerX.Storage.EventStoreQuery]@{
                    EventIds = [int[]] @(41000)
                    MaxEvents = [long] ($case.RowCount / 10)
                }
                $result = $run.Store.ReadReportAsync($query).GetAwaiter().GetResult()
                $run.ResultCount = $result.Rows.Count
                $run.WorkItems = $result.Rows.Count
            } elseif ($case.Workload -eq 'ManagedQuery') {
                $query = [EventViewerX.Storage.EventStoreQuery]@{
                    Predicate = [EventViewerX.EventPredicate]::Compare(
                        'User',
                        [EventViewerX.EventPredicateOperator]::Equal,
                        [object[]] @('user-9'))
                    MaxCandidates = [long] ($case.RowCount + 1)
                }
                $result = $run.Store.ReadReportAsync($query).GetAwaiter().GetResult()
                $run.ResultCount = $result.Rows.Count
                $run.WorkItems = $result.EventsScanned
            } elseif ($case.Workload -eq 'DailySummary') {
                $result = $run.Store.SummarizeAsync(
                    [EventViewerX.Storage.EventStoreQuery]::new(),
                    [EventViewerX.Storage.EventStoreSummaryPeriod]::Day).GetAwaiter().GetResult()
                $run.ResultCount = ($result.Rows | Measure-Object Count -Sum).Sum
                $run.WorkItems = $result.EventsScanned
            } else {
                [EventViewerX.Reporting.EventReportCsvRenderer]::Save($run.Report, $run.CsvPath) | Out-Null
                $run.ResultCount = $run.Report.Rows.Count
                $run.WorkItems = $run.Report.Rows.Count
            }
            $stopwatch.Stop()
            $run.ElapsedMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            $run.CsvExists = Test-Path -LiteralPath $run.CsvPath -PathType Leaf
            if ($run.WorkRoot -and (Test-Path -LiteralPath $run.WorkRoot)) {
                Remove-Item -LiteralPath $run.WorkRoot -Recurse -Force
            }
            # PowerForge retains scalar sample state for artifacts. Do not retain the
            # complete typed report or provider graph after those scalars are captured.
            $run.Store = $null
            $run.Report = $null
        }
    }

    Add-BenchmarkValidation {
        param($case, $run)

        [long] $expected = if ($case.Workload -in 'SqlQuery', 'ManagedQuery') {
            $case.RowCount / 10
        } else {
            $case.RowCount
        }
        Assert-BenchmarkValue -Actual ([long] $run.ResultCount) -Expected $expected -Message 'The operation must preserve its exact row-count contract.'
        if ($case.Workload -eq 'TypedCsv') {
            Assert-BenchmarkValue -Actual ([bool] $run.CsvExists) -Expected $true -Message 'Typed CSV output must exist before cleanup.'
        }
    }

    Add-BenchmarkMetric RowsPerSecond {
        param($case, $run)
        [Math]::Round($run.WorkItems / ($run.ElapsedMilliseconds / 1000), 2)
    }
    Add-BenchmarkMetric ResultRows { param($case, $run) [long] $run.ResultCount }
    Add-BenchmarkMetric WorkItems { param($case, $run) [long] $run.WorkItems }
    Set-BenchmarkArtifacts Json, Csv, Markdown
}
