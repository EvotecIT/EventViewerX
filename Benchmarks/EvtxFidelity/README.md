# EVTX portability fidelity gate

This executable compares `EventViewerX.Evtx` with the Windows Eventing API for
the same saved EVTX file. On Linux and macOS it exercises only the portable
path. It reports record count, throughput, allocations, parser diagnostics,
and identity parity for event ID, record ID, provider, channel, computer, and
timestamp.

```powershell
dotnet run --project .\Benchmarks\EvtxFidelity\EventViewerX.EvtxFidelity.csproj -- C:\Fixtures\Security.evtx 0 C:\Tools\evtx_dump.exe
```

For a repeatable regression gate, select the materialization contract, run
rotated iterations, and set explicit budgets. The command exits with code 5
when a configured throughput or allocation budget fails; identity loss keeps
its existing non-zero failure codes.

```powershell
dotnet run --project .\Benchmarks\EvtxFidelity\EventViewerX.EvtxFidelity.csproj `
    --configuration Release -- `
    C:\Fixtures\Security.evtx 10000 `
    --read-mode StructuredData `
    --warmup 1 `
    --iterations 3 `
    --minimum-identity-ratio 1 `
    --minimum-exact-timestamp-ratio 1 `
    --min-events-per-second 15000 `
    --max-bytes-per-event 100000 `
    --output .\Ignore\Benchmarks\EvtxFidelity\structured-data.json
```

The JSON records fixture SHA-256, runtime and architecture, parser assembly
versions, the resolved external-parser path and SHA-256 when configured, every
measured iteration, medians, fidelity minima, diagnostics, and the evaluated
budget. It hashes the fixture before and after the run and rejects results when
the bytes changed or only some requested Windows reference iterations completed.
`Metadata`, `Message`, `StructuredData`, `RawXml`,
`StructuredDataAndMessage`, and `Full` can be measured independently; do not
compare unlike read modes as if they were equivalent work.
The `--output` path must name a new file. Existing files are never overwritten,
which protects forensic fixtures and their filesystem aliases.

The gate fails when the portable reader returns no records, a configured
command reader returns no records, or Windows comparison finds identity loss,
missing records, or extra records. Provider
message text is intentionally excluded because non-Windows systems normally
do not have the originating provider message DLLs. An explicitly configured
exact-timestamp gate exits with code 6 when no Windows reference result is
available; it never reports an unmeasured fidelity requirement as passing.

The command-backed adapter is compared with a one-microsecond timestamp
tolerance and reports exact timestamp parity separately. Set
`--minimum-exact-timestamp-ratio 1` when validating a parser build intended for
lossless use. Released `evtx_dump` 0.12.2 renders FILETIME values with six
fractional digits, so it does not expose the final 100-nanosecond digit until
that upstream renderer is corrected.

## Current evidence

The clean 31.5 MB Security fixture contains 62,031 records. On the Windows
validation host, the portable adapter preserved every record with 100 percent
identity parity. It measured approximately 5,364 events/second and 720 KB
allocated/event. The Windows Eventing API measured approximately 41,719
events/second and 3.5 KB/event on the equivalent workload. This keeps the
portable adapter opt-in until its allocation profile is replaced or improved.

A clean small fixture and the same CLI query also passed on Ubuntu under WSL.
A missing-header-checksum fixture preserved 17 of 17 records and emitted
`EVXEVTX001`. A sparse/bad-chunk fixture recovered 269 records while Windows
rejected the file and emitted explicit chunk/parser diagnostics. A dirty
12.7 MB Security file preserved all 14,621 records with 100 percent identity
parity.

A small fixture truncated by 100 bytes retained all seven complete records,
emitted `EVXEVTX002`, and was rejected by the Windows Eventing API. This proves
the recovery path without claiming that arbitrary truncation is lossless.

The archived ForwardedEvents fixture stores literal BinXML records produced by
a rendered WEC subscription. The managed dependency alone returned none of the
653 Windows-readable records, so EVX now detects that shape and uses its bounded
literal reader. The combined managed adapter preserved 653 of 653 records with
complete identity parity at a three-run median of approximately 4,501
events/second and 81.4 KB allocated/event; the Windows path measured a median
of approximately 15,801 events/second and 9.2 KB/event. A sanitized one-record
derivative and a meaningfully truncated fixture are retained in the
cross-platform CI suite.
The caller-supplied `evtx_dump` 0.12.2 adapter also preserved 653 of 653 records
with complete identity parity.

On a fresh 21.0 MB Security snapshot, a five-iteration 1,000-record comparison
measured the final bounded compact-XML command adapter at approximately 8,308
events/second and 49.6 KB allocated/event. The existing managed dependency
measured approximately 2,755 events/second and 841.3 KB/event; the Windows
Eventing API measured approximately 50,320 events/second and 4.2 KB/event.
All three paths preserved every compared event identity. Released
`evtx_dump` 0.12.2 retained one-microsecond timestamp parity but only 10.6
percent exact timestamp parity because its renderer emits six fractional
digits.

A local proof build changing that renderer from six FILETIME digits to seven
passed the exact-fidelity gate on all 5,000 comparisons across five measured
iterations. Its final bounded-reader median was approximately 8,308
events/second and 49.6 KB/event. The proof binary is not a product dependency
or release; production
adoption remains gated on an upstream correction or an owned native package
with the same fidelity evidence.

`python-evtx` 0.8.1 supplied the second independent recovery comparison. It
rendered 7 of 7 clean records, 270 of 270 sparse/bad-chunk records, and all
62,031 large records. Full XML rendering of the large file took 165.9 seconds
(about 374 events/second). It enumerated 653 archive records but rendered none
to XML on that fixture, and returned zero records for the truncated sample.
Record enumeration is therefore reported separately from usable event
projection.

The engines have different recovery envelopes. The managed EVX adapter
retained all seven records from the 100-byte-truncated sample; both independent
parsers returned zero. On the sparse/bad-chunk fixture, both EVX adapters
retained 269 valid records. The command adapter rejected one timestamp-less
placeholder and emitted `EVXEVTX304`, while preserving the valid stream.
