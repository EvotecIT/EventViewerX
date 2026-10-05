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

The historical HTML measurements above precede the shared render buffer described below. Timing results are observations from a potentially busy development host, not release performance guarantees.

The before snapshot uses the unchanged renderer at commit `7793aa5997bf016b0f56c33e8ae01abd68846ccd`. The after snapshot includes bounded email projection, single-pass constant-column classification, and HtmlForgeX 1.1.0 for descriptive dashboard captions. The email dependency remains HtmlForgeX.Email 1.6.0 in both measurements.

## Mobile email baseline

`after-mobile.json` records the same fixture with published HtmlForgeX.Email
2.0.0 and mobile stacked records at commit
`2c4904dff24deb130c4da5fa1fa3e5fc4f79ca67`. All twelve measured samples pass.
The runner uses one warmup, three measured iterations and rotated case order,
with processor affinity `0xFFFF` and BelowNormal priority requested.

| Case | Mean renderer allocated bytes | Output bytes |
| --- | ---: | ---: |
| Email, 1,000 events | 11,899,384 | 215,215 |
| Email, 10,000 events | 11,899,581 | 215,219 |
| HTML, 1,000 events | 147,577,709 | 2,961,045 |
| HTML, 10,000 events | 1,156,132,952 | 16,569,102 |

Email allocation stays approximately constant as the input grows because the
digest still displays 25 records. The newer dependency and stacked presentation
produce more markup and allocate more than the historical 1.6 fixture above.
Use this baseline when measuring the mobile email presentation; the two dependency
versions and output layouts do not form a comparison of projection changes alone.

## Shared render buffer

HtmlForgeX 1.2.0 renders nested monitoring containers and deferred record JSON
into the owning buffer. It avoids copying complete child strings at each level
and reuses monitoring IDs that are already normalized. HTML retains every record;
email continues to display the first 25.

A comparison of unchanged HtmlForgeX source at
`6787b52211d1159bf0e9510451454bea79a4c967` with the shared-buffer implementation
uses the same fixture and readable assets on both sides:

| Case | Before allocated bytes | After allocated bytes | Reduction |
| --- | ---: | ---: | ---: |
| HTML, 1,000 events | 147,991,211 | 41,959,016 | 71.6% |
| HTML, 10,000 events | 1,158,312,323 | 223,370,648 | 80.7% |

The output has the same length and content apart from its generated timestamp.
Wide and compact browser checks retain all 10,000 records and can filter and
inspect the final record.

`after-render-buffer.json` records the signed 1.2.0 package with its release
assets, which are minified. All twelve measured samples pass with the same SDK,
affinity, priority, warmup, iteration count and case order as the mobile baseline.

| Case | Mean renderer allocated bytes | Output bytes |
| --- | ---: | ---: |
| Email, 1,000 events | 11,899,384 | 215,215 |
| Email, 10,000 events | 11,899,480 | 215,219 |
| HTML, 1,000 events | 38,704,584 | 2,827,233 |
| HTML, 10,000 events | 220,107,507 | 16,435,290 |

These are cumulative allocations during the warmed rendering operation, not peak
working-set measurements. The shared-buffer comparison isolates the renderer
change; the package baseline also reflects its minified assets. A large report
still projects every event and embeds its data, so callers should bound inputs
for large investigations. These fixtures do not establish an unlimited-report
memory bound or a release throughput guarantee.
