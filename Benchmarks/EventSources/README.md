# Event source benchmark

This PowerForge suite captures a latest-record boundary, then measures the same
stable newest-first metadata window through
the reusable EventViewerX C# engine, the thin `Get-EVXEvent` cmdlet, and
`Get-WinEvent`. Every sample validates count, record IDs, timestamps, and
order before it is accepted.

Remote results include RPC/session establishment and network behavior. They
describe the named lab target at the recorded time; they are not substituted
for local EVTX scale evidence.

```powershell
.\Benchmarks\EventSources\Invoke-EventSourceBenchmark.ps1 `
    -MachineName AD0 `
    -LogName Security `
    -SampleCount 100, 1000 `
    -IterationCount 3 `
    -UpdateReadme
```

For Windows Event Collector throughput, first establish a collector whose
source computers report an active subscription in `wecutil gr` and whose
`ForwardedEvents` channel receives a known disposable source. A successful
subscription create/read/delete cycle is not forwarding proof. The benchmark
must pin source record IDs and validate their arrival at the collector before
publishing latency or events-per-second numbers; an unconfigured WinRM/WEC
source is reported as an environmental prerequisite failure, not a slow or
zero-throughput product result.
