# Checkpoint lookup measurements

This optional PowerForge suite measures ten reads of the last checkpoint in a
database containing 100, 1,000, or 10,000 watcher checkpoints. Every read uses
Unicode case variants of the consumer and computer names and checks the record
ID. The retained-store case reuses an initialized `EventStore`; the reopened-store
case constructs a new store for each read and includes its schema validation.

Run it from PowerShell 7.6 on Windows with the .NET 10 SDK and PSPublishModule
3.0.134 or later:

```powershell
./Benchmarks/EventCheckpoints/Invoke-EventCheckpointBenchmark.ps1
```

`-OutputRoot` selects the task output directory. The default is
`Ignore/Benchmarks/EventCheckpoints` under the checkout. Database population is
outside the measured operation, and the fixture removes each database after
the read sample. This project is not packed or included in product builds.

The 2026-10-04 comparison uses the same 32 logical processors, normal process
priority, one warmup, three measured iterations, and rotated engine order on an
AMD Ryzen 9 9950X3D2. At 10,000 checkpoints, ten retained-store reads allocated
33,112,336 bytes before early identity filtering and 12,316,640 bytes afterward,
a reduction of about 63%. Reopened-store allocations fell from 83,685,928 to
62,909,664 bytes, about 25%; initialization still inspects all checkpoint
identities to coalesce legacy Unicode aliases.

The retained-store operation median was 211.1 ms before the change and 119.0 ms
afterward. The smaller samples and reopened cases show timing variance on the
busy host, so these are comparison observations rather than throughput targets.
`lookup-before.json` and `lookup-after.json` retain the samples and summaries.

## Indexed identity lookup

Checkpoint identities now use a versioned derived SHA-256 key and a SQLite index.
Managed `OrdinalIgnoreCase` verification remains authoritative. Legacy rows are
coalesced and indexed during initialization; uncached legacy identities and misses
retain a compatibility fallback. Ordinary successful lookups avoid reading the whole
checkpoint table. Older backups are accepted and receive the derived index when opened.

The 2026-10-05 comparison used the same host, logical-processor affinity mask 3,
BelowNormal priority, one warmup, three measured samples, and rotated retained/reopened
order. Population and initial migration are outside the measured operation. At 10,000
checkpoints, ten retained-store reads allocated 12,320,400 bytes before indexing and
123,680 bytes afterward. Reopened reads allocated about 62,921,325 bytes before and
693,120 bytes afterward. All samples validated the expected record IDs.

The ten-read operation medians were 304.3 ms before and 25.5 ms after for retained
stores, and 626.3 ms before and 46.8 ms after for reopened stores. Timing varied on
the busy host; these are local comparisons, not throughput guarantees or a ranking
against other libraries. `indexed-lookup-before.json` and `indexed-lookup-after.json`
retain the summaries, sample counts, allocation metrics, and timing spread.

## Absence completion matching

`absence-completion.benchmark.ps1` uses the same fixture project to measure 10,000
late completions after 10,000 expired starts in one correlation group. Setup and final
absence evaluation are outside the measured operation. Every sample verifies that all
10,000 expired triggers remain available as evidence.

```powershell
dotnet build ./Benchmarks/EventCheckpoints/EventCheckpoints.BenchmarkFixture.csproj -c Release
Add-Type -Path ./Benchmarks/EventCheckpoints/bin/Release/net10.0-windows/EventViewerX.CheckpointBenchmark.dll
Import-Module PSPublishModule
Invoke-BenchmarkSuite -Path ./Benchmarks/EventCheckpoints/absence-completion.benchmark.ps1 `
    -OutputRoot ./Ignore/Benchmarks/AbsenceCompletion -ProcessorAffinityMask 3 -ProcessPriority BelowNormal
```

The 2026-10-05 comparison used one warmup and three measured samples on the same
host and affinity as the indexed lookup comparison. Median matching time fell from
914.1 ms to 10.2 ms after replacing repeated list scans and shifts with a monotonic
completion cursor and linked pending evidence. The before samples ranged from 789.9
to 1702.5 ms; after samples ranged from 9.6 to 14.2 ms. These local measurements isolate
completion matching and do not predict complete investigation throughput.
`absence-before.json` and `absence-after.json` retain the runner summaries.
