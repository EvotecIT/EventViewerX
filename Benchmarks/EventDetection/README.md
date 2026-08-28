# Detection candidate-index benchmark

This permanent BenchmarkDotNet suite measures the public detection engine with
1, 10, 100, and 1,000 enabled rules. Every case evaluates the same 1,000
observations and produces the same number of findings. Each observation has one
matching indexed candidate, so growth exposes accidental scanning of unrelated
rules rather than useful matching work.

Validate the matrix before a measured run:

```powershell
dotnet run --project .\EventViewerX.DetectionBenchmarks.csproj -c Release -- `
    --filter "*DetectionCandidateIndexBenchmarks*" --job Dry --noOverwrite
```

Use `--job Short` for development or omit `--job` for final release evidence.
BenchmarkDotNet artifacts are temporary; retain the result summary and remove
the generated artifact directory before finishing the release candidate.

The initial benchmark exposed a full-rule scan: the 1,000-rule case took
39.30 ms and allocated 78.98 MB. After candidate selection was changed to seed
from the smallest selector posting list and intersect all selector dimensions,
the same case took 0.435 ms and allocated 1.80 MB. The 1, 10, 100, and 1,000-rule
cases now remain within 0.401-0.435 ms on the measured host.

| Enabled rules | Mean | Operations/second | Allocated |
| ---: | ---: | ---: | ---: |
| 1 | 400.9 us | 2,494.6 | 1.80 MB |
| 10 | 415.6 us | 2,406.3 | 1.80 MB |
| 100 | 409.8 us | 2,440.0 | 1.80 MB |
| 1,000 | 434.8 us | 2,300.1 | 1.80 MB |

These are short-run development measurements on .NET 10.0.11, Windows 11, and
an AMD Ryzen 9 9950X3D. Release claims should use the default BenchmarkDotNet
job and record the repository head and fixture hash.
