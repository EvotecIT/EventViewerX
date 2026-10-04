# Normalized query streaming benchmark

Run this test-only suite with the repository's .NET 10 SDK and PowerShell 7.6 or
later. It uses the same PowerForge benchmark tooling as the other repository suites.

This suite compares `EventReportEngine.QueryAsync` with `StreamRowsAsync` over the
same lazy, ordered saved-event source. It checks payload values, source identity,
row counts, record-ID checksums, and complete exhaustion. It isolates normalized
query retention and first-result delivery; it does not measure EVTX parsing,
provider-message formatting, JSON serialization, or console throughput.

```powershell
.\Benchmarks\EventReportStreaming\Invoke-EventReportStreamingBenchmark.ps1 -Plan
.\Benchmarks\EventReportStreaming\Invoke-EventReportStreamingBenchmark.ps1 `
    -EventCount 1000,10000,100000 -WarmupCount 1 -IterationCount 3
```

PowerForge controls warmup, rotated sampling, validation, comparisons, and
artifacts. `FirstRowMs` and `QueryMs` measure the query and row consumer, excluding
the forced collections used for retained-memory measurement. The suite's outer
duration includes those collections; use the query metrics when assessing query
time. `RetainedManagedBytes` measures live managed memory with the completed
snapshot rooted, or after the stream has returned without retained rows. It is
not process peak working set. `AllocatedBytes` includes all managed threads and
can include unrelated runtime activity in the benchmark process.

`ReadAtFirstRow` distinguishes full input materialization from bounded reader
prefetch. The engine retains its existing 64-row merge buffer and one detached
head per source; the report layer retains schemas and completion evidence.
Multiple-source queries must prime source cursors to establish deterministic
merge order. A slow consumer is awaited before delivering its next row.

Recorded evidence is in [query-retention-baseline.json](query-retention-baseline.json)
and [query-schema-cache-baseline.json](query-schema-cache-baseline.json). The
4 October 2026 runs used .NET 10.0.12 and PowerShell 7.6.6 on Windows, with all
32 logical processors available at normal process priority, one warmup and three
rotated samples per case. The second run caches the common generic schema rather
than rebuilding it for each event. At 100,000 rows, mean managed allocation for
streaming fell from 561.8 MB to 490.1 MB. Both runs preserved every row, payload,
source identity, record-ID checksum, and complete exhaustion.

The cached-schema run retained about 94.4 MB for the completed snapshot and
38.4 KB after streaming. Snapshot delivery began after all 100,000 inputs had
been read; streaming began while fewer than 68 inputs had been read. These are
managed retention and input-progress measurements. Timing varies with machine
load, and these runs do not establish native or portable EVTX throughput.
