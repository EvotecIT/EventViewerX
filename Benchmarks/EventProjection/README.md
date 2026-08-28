# Event projection benchmark

This PowerForge benchmark compares the compatibility path that compiles typed
rule selection for every event with the immutable `EventTypeProjectionPlan`
used by the core engine, watcher, CLI, and PowerShell surfaces. Both engines
process the same specialized NTLMv1 event and must produce the same CLR type,
event count, and deterministic checksum.

```powershell
.\Invoke-EventProjectionBenchmark.ps1 -EventCount 1000,10000 -IterationCount 3
```

Use `-Plan` to inspect the matrix. Artifacts are written under the ignored
`Ignore\Benchmarks\EventProjection` folder by default; callers may provide an
isolated `-OutputRoot` for smoke runs.

<!-- event-projection-comparison:start -->
| Scenario | Variables | Host | Operation | CompilePerEvent | ReusablePlan | Result |
| --- | --- | --- | --- | ---: | ---: | --- |
| Authentication-1000 | EventCount=1000 | Core-7.6.4 | Project | 1.00x (22ms) | 0.38x (8ms) | CompilePerEvent slower than ReusablePlan |
| Authentication-10000 | EventCount=10000 | Core-7.6.4 | Project | 1.00x (133ms) | 0.37x (49ms) | CompilePerEvent slower than ReusablePlan |
<!-- event-projection-comparison:end -->

<!-- event-projection-summary:start -->
| Scenario | Variables | Operation | Host | OS | RunMode | Engine | Samples | Failures | Median | Mean | P95 | StdDev | Status |
| --- | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Authentication-1000 | EventCount=1000 | Project | Core-7.6.4 | Windows | standard | CompilePerEvent | 3 | 0 | 21.9096 | 19.8739333333333 | 22.60548 | 4.21326322146307 | Succeeded |
| Authentication-1000 | EventCount=1000 | Project | Core-7.6.4 | Windows | standard | ReusablePlan | 3 | 0 | 8.4221 | 9.11893333333333 | 11.58344 | 2.54007845220051 | Succeeded |
| Authentication-10000 | EventCount=10000 | Project | Core-7.6.4 | Windows | standard | CompilePerEvent | 3 | 0 | 133.0807 | 132.438233333333 | 140.21824 | 8.91168587043626 | Succeeded |
| Authentication-10000 | EventCount=10000 | Project | Core-7.6.4 | Windows | standard | ReusablePlan | 3 | 0 | 48.8795 | 39.2440333333333 | 50.77787 | 18.5458369900992 | Succeeded |
<!-- event-projection-summary:end -->
