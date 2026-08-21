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
