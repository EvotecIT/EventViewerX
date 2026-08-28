# EVTX portability fidelity gate

This executable compares `EventViewerX.Evtx` with the Windows Eventing API for
the same saved EVTX file. On Linux and macOS it exercises only the portable
path. It reports record count, throughput, allocations, parser diagnostics,
and identity parity for event ID, record ID, provider, channel, computer, and
timestamp.

```powershell
dotnet run --project .\Benchmarks\EvtxFidelity\EventViewerX.EvtxFidelity.csproj -- C:\Fixtures\Security.evtx 0 C:\Tools\evtx_dump.exe
```

The gate fails when the portable reader returns no records or, on Windows,
when identity parity is below 99 percent or records are missing. Provider
message text is intentionally excluded because non-Windows systems normally
do not have the originating provider message DLLs.

The command-backed adapter is compared with a one-microsecond timestamp
tolerance and reports exact timestamp parity separately. `evtx_dump` JSONL
serializes timestamps to microsecond precision, so its interchange format does
not expose the final 100-nanosecond FILETIME digit.

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

The archived ForwardedEvents fixture fails the portable gate: Windows reads
653 records while the managed dependency returns none. The caller-supplied
`evtx_dump` 0.12.2 adapter preserved 653 of 653 records with complete identity
parity; it is the preferred portable engine for that archive shape.

On the 31.5 MB Security fixture, the command adapter normalized 62,031 records
at approximately 18,854 events/second and 63.9 KB allocated/event in one full
run. A subsequent 10,000-record comparison measured approximately 9,566
events/second, with 100 percent event ID, provider, channel, computer, and
one-microsecond timestamp parity. Exact timestamp parity was lower because the
JSONL format omits the final 100-nanosecond digit.

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
