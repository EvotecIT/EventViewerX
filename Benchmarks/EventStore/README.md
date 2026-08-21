# Event Store Benchmark

This PowerForge suite measures the optional `EventViewerX.Storage` path without
mixing storage costs into native event-log enumeration claims. Every case uses
the same homogeneous typed rows and validates its exact result count before the
sample is accepted.

## Contract

The suite keeps five operations separate:

| Workload | Measured boundary | Validation |
| --- | --- | --- |
| `Write` | Transactional definition, event, and index persistence into a new SQLite store | Every input row is inserted |
| `SqlQuery` | Indexed event-ID selection and normalized report rehydration | Exactly one tenth of the rows is returned |
| `ManagedQuery` | Indexed candidate read plus exact typed `User` predicate verification | Exactly one tenth of the rows is returned |
| `DailySummary` | Exhaustive UTC calendar aggregation through the SQL fast path | Bucket counts sum to every stored row |
| `TypedCsv` | Homogeneous domain-only CSV projection from one normalized report | The file exists and contains every report row |

These rows are not a `Get-WinEvent` comparison. Native event-log reading,
typed projection, local persistence, and presentation are different product
boundaries. Use the [event query benchmark](../EventLogParsing/README.md) for
same-input `EventViewerX`, `Get-EVXEvent`, and `Get-WinEvent` comparisons.

## Run

Run the public 1,000/10,000-row matrix with rotated order, one warmup, and three
measured iterations:

```powershell
.\Benchmarks\EventStore\Invoke-EventStoreBenchmark.ps1 `
    -RowCount 1000,10000 `
    -WarmupCount 1 `
    -IterationCount 3 `
    -UpdateReadme
```

Inspect the resolved plan without executing it:

```powershell
.\Benchmarks\EventStore\Invoke-EventStoreBenchmark.ps1 `
    -RowCount 1000,10000 `
    -IterationCount 3 `
    -Plan
```

Artifacts are written below `Ignore\Benchmarks\EventStore` unless
`-OutputRoot` is supplied. PowerForge records the repository state, runtime,
rotated sample order, per-sample duration, work items, rows per second, result
count, validation, and JSON/CSV/Markdown summaries. The wrapper deliberately
creates one PowerForge run per row count so large typed fixture graphs cannot
contaminate another scale's host state. One-iteration diagnostic runs are
useful while developing but must not be published as stable evidence.

## Interpretation

`SqlQuery` and `DailySummary` demonstrate the indexed fast paths.
`ManagedQuery` deliberately includes exact domain-predicate evaluation and
reports candidate work rather than pretending every typed field is a SQLite
column. `Write` includes durable SQLite transaction work. `TypedCsv` starts
from an already-normalized report, so it measures renderer cost rather than
event-log I/O. Results describe the recorded machine and package versions;
the validation contract is the reusable evidence.

### 1,000 rows

<!-- event-store-1000:start -->
| Scenario | Variables | Operation | Host | OS | RunMode | Engine | Samples | Failures | Median | Mean | P95 | StdDev | Status |
| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| DailySummary-1000 | RowCount=1000, Workload=DailySummary | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 9.4869 | 11.9344333333333 | 16.10496 | 4.24860859262575 | Succeeded |
| ManagedQuery-1000 | RowCount=1000, Workload=ManagedQuery | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 26.8777 | 25.5684666666667 | 28.50616 | 3.93992189051171 | Succeeded |
| SqlQuery-1000 | RowCount=1000, Workload=SqlQuery | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 12.7203 | 12.8848333333333 | 15.66573 | 3.02925308010627 | Succeeded |
| TypedCsv-1000 | RowCount=1000, Workload=TypedCsv | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 18.4651 | 15.6335666666667 | 18.65869 | 5.09177763883433 | Succeeded |
| Write-1000 | RowCount=1000, Workload=Write | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 66.8054 | 68.91 | 74.8784 | 6.09213734907545 | Succeeded |
<!-- event-store-1000:end -->

### 10,000 rows

<!-- event-store-10000:start -->
| Scenario | Variables | Operation | Host | OS | RunMode | Engine | Samples | Failures | Median | Mean | P95 | StdDev | Status |
| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| DailySummary-10000 | RowCount=10000, Workload=DailySummary | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 25.7821 | 26.3153 | 27.63394 | 1.33988339791192 | Succeeded |
| ManagedQuery-10000 | RowCount=10000, Workload=ManagedQuery | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 119.3066 | 116.635966666667 | 119.32352 | 4.64196341684565 | Succeeded |
| SqlQuery-10000 | RowCount=10000, Workload=SqlQuery | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 23.774 | 23.8035666666667 | 27.91121 | 4.55262200751757 | Succeeded |
| TypedCsv-10000 | RowCount=10000, Workload=TypedCsv | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 22.2964 | 22.5926333333333 | 28.23802 | 6.16279207367353 | Succeeded |
| Write-10000 | RowCount=10000, Workload=Write | Execute | Core-7.6.4 | Windows | standard | EventViewerXStorage | 3 | 0 | 377.1787 | 378.261266666667 | 392.69254 | 15.6418717551108 | Succeeded |
<!-- event-store-10000:end -->
