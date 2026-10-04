# Report rendering benchmark

This fixture measures rendering after report creation. It uses deterministic generic events, retains every row in HTML, and displays the first 25 rows in email. Each child process warms the renderer before the measured call. The fixture and its dependencies are used only by this optional benchmark and are excluded from shipped packages.

Run the benchmark from the repository root with .NET 10 and PSPublishModule 3.0.134 or newer:

```powershell
./Benchmarks/EventReportRendering/Invoke-EventReportRenderingBenchmark.ps1 -RowCount 1000,10000
```

The shared PowerForge runner uses one warmup, three measured iterations, rotated case order, and no outlier removal. `before.json` and `after.json` contain the retained summaries. Both measurements used processor affinity `0xFFFF` and BelowNormal priority on the same Windows host with SDK 10.0.112.

| Case | Before allocated bytes | After allocated bytes | Reduction |
| --- | ---: | ---: | ---: |
| Email, 1,000 events | 4,919,752 | 3,088,720 | 37.2% |
| Email, 10,000 events | 21,767,960 | 3,088,928 | 85.8% |
| HTML, 1,000 events | 147,869,973 | 147,577,205 | 0.2% |
| HTML, 10,000 events | 1,159,374,592 | 1,156,124,688 | 0.3% |

The email change limits projection to the rows actually displayed. Its allocation stays approximately constant as the snapshot grows. The complete snapshot and source counts remain available in the report.

The HTML renderer still projects and renders every event. Its allocation improvement is small, and the 10,000-event output is about 16.6 MB. These measurements do not establish an HTML throughput improvement or a bound on large interactive reports. Timing results are observations from a potentially busy development host, not release performance guarantees.

The before snapshot uses the unchanged renderer at commit `7793aa5997bf016b0f56c33e8ae01abd68846ccd`. The after snapshot includes bounded email projection, single-pass constant-column classification, and HtmlForgeX 1.1.0 for descriptive dashboard captions. The email dependency remains HtmlForgeX.Email 1.6.0 in both measurements.
