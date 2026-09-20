# Performance regression gates

This document describes the current 4.0 source tree. Version 4.0 is not yet
published or released.

Performance claims are tied to exact workloads, fixtures, SDK/runtime identity,
and correctness checks. PowerForge owns the PowerShell benchmark orchestration,
artifact provenance, normalized summaries, and baseline comparison. The
BenchmarkDotNet detection suite retains its native measurement engine and uses
a thin adapter to PowerForge gates.

## Checked-in budgets

| Area | Matrix | Gate |
| --- | --- | --- |
| Persistent watcher | 100, 1,000, and 10,000-event bursts | Exact delivery/loss/duplicate validation plus median wall-clock baseline. |
| Local history | 100,000 rows across write, managed query, SQL query, daily summary, and typed CSV | Workload correctness plus median wall-clock baseline. |
| Typed reporting | Exact 5,000-record Security EVTX, 1,000-report-row window, HTML/Excel/email/all | Exact typed count and renderer validation plus median wall-clock baseline. The sensitive lab fixture is not committed. |
| Detection candidate index | 1, 10, 100, and 1,000 enabled rules | Complete matrix, median time, and allocation per operation. |
| Detection streaming | Three lanes at 1K, 10K, 100K, and 1M observations | Complete matrix, median time, and allocation per event. |

Timing tolerances are intentionally broad: 50% relative plus a small absolute
allowance. These gates catch large regressions without turning workstation
noise into a product failure. Correctness, complete matrices, zero failed
samples, bytes per event, and scale slope are stronger invariants.
Each expected detection parameter tuple must occur exactly once; missing rows
and duplicate rows both fail before a timing or allocation comparison runs.

## Run the gates

```powershell
# Elevated Windows session; creates and removes disposable logs.
.\Benchmarks\EventWatcher\Invoke-EventWatcherBurstBenchmark.ps1 `
    -BurstCount 100,1000,10000 -IterationCount 3 `
    -BaselinePath .\Benchmarks\EventWatcher\watcher-baseline.json

.\Benchmarks\EventStore\Invoke-EventStoreBenchmark.ps1 `
    -RowCount 100000 -WarmupCount 1 -IterationCount 3 `
    -BaselinePath .\Benchmarks\EventStore\event-store-100000-baseline.json

.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -Case Typed-Report-Html,Typed-Report-Excel,Typed-Report-Email,Typed-Report-All `
    -Engine EventViewerXReport `
    -TypedFixturePath C:\Evidence\Security-5000.evtx `
    -ExpectedTypedCount 5000 -ReportSampleCount 1000 -IterationCount 3 `
    -BaselinePath .\Benchmarks\EventLogParsing\reporting-5000-baseline.json

.\Benchmarks\EventDetection\Test-DetectionBenchmarkBudget.ps1 `
    -CandidateResultPath .\BenchmarkDotNet.Artifacts\results\EventViewerX.DetectionBenchmarks.DetectionCandidateIndexBenchmarks-report-full-compressed.json `
    -ThroughputResultPath .\BenchmarkDotNet.Artifacts\results\EventViewerX.DetectionBenchmarks.DetectionThroughputBenchmarks-report-full-compressed.json
```

## Updating a baseline

Use `-UpdateBaseline` only after the workload, fixture identity, correctness
checks, and measured regression/improvement have been reviewed. A faster run is
not sufficient if it dropped events, weakened output, skipped a matrix row, or
changed the fixture. Keep superseded heavy artifacts out of the repository;
commit only the small baseline and the contract/documentation change that
explains why it moved.
