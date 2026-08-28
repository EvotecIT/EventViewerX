# Event watcher burst benchmark

This PowerForge benchmark measures the persistent `evx watch` path from an
atomic ready signal through native Windows Event Log delivery and exact JSONL
accounting. Each sample owns a disposable log/source and removes it afterward.

Run from an elevated Windows PowerShell 7 session:

```powershell
.\Invoke-EventWatcherBurstBenchmark.ps1 -BurstCount 100,1000,10000 -IterationCount 3
```

The validation rejects event loss, duplicate record IDs, non-zero host exits,
missing readiness/completion artifacts, and partial JSONL output. The measured
duration includes producing the native events and waiting for the portable host
to receive the complete burst; it is not a parser-only throughput claim.

## Current smoke evidence

The portable `net10.0` CLI delivered a 100-event burst in 95.5 milliseconds
(approximately 1,168 events/second) with 100 received, 100 processed, 100 JSONL
rows, 100 unique source record IDs, and a zero exit code. This run exposed and
then verified a compatibility fix for `EventBookmark(string)` when the Windows
subscription engine is consumed through the netstandard assembly from a
platform-neutral host. Release evidence still requires the documented rotated
multi-iteration matrix.
