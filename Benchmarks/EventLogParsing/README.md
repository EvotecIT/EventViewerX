# Event Log Parsing Benchmark

This PowerForge suite measures event enumeration, projection, and export costs against identical EVTX bytes. It
replaces the old `Example.ComparePerformance.ps1` stopwatch script with declared cases, rotated execution order,
validation, provenance, normalized artifacts, and generated README tables.

The committed smoke fixture contains 184 events. Large fixtures remain external because real Security logs can exceed
1 GB and may contain sensitive data.

## Comparison contract

The suite keeps four kinds of evidence separate:

| Class | What is held equal | What the result means |
| --- | --- | --- |
| Apples to apples: exact output | Same EVTX, event order, schema, encoding, row/event count, byte count, and SHA-256 | A direct end-to-end comparison. The engines must create the same metadata CSV or raw XML document byte for byte. |
| Apples to apples: common user job | Same EVTX, direction, maximum event count, read-mode category, streaming consumer, and event order and identity checks | A comparison of the natural public APIs for the same user job. PSEventViewer may return additional parsed fields, and those extra counters are reported. |
| Apples to oranges: complete native output | Same EVTX, validated event count, and broadly similar output purpose, but EventViewerX and EvtxECmd use different schemas and output volumes | Useful operational evidence, but not an interchangeable-output or unqualified speed claim. |
| EvtxECmd-native workflow | EvtxECmd's own parse and export modes, measured without pretending another engine performs the same work | A reproducible description of the external tool's native workflows. |

Do not combine rows from different classes into a single “faster than” claim. In particular, comparing a five-column
metadata CSV with EvtxECmd's forensic CSV compares different work and different output volumes.

## Engines

- `DotNet`: direct `System.Diagnostics.Eventing.Reader.EventLogReader` enumeration. This is a lower bound, not a
  PowerShell-module surface.
- `PropertySelector`: direct `EventLogPropertySelector` projection of eighteen core metadata fields.
- `EventViewerX`: the reusable `EventLogEngine.ReadFile` projection engine.
- `EventViewerXExport`: the reusable `EventLogExporter` path, which writes directly without a PowerShell object pipeline.
- `EventViewerXTyped`: built-in typed projections over definition-owned event sources.
- `EventViewerXReport`: one query and normalized snapshot rendered as HTML, Excel, email, or all three outputs.
- `EventViewerXCli`: the framework-dependent `evx.exe` process used for command cold-start measurements.
- `EventViewerXCliPortable`: the optional self-contained `evx.exe` artifact, measured separately because it carries
  the .NET runtime with it.
- `PSEventViewer`: the public `Get-EVXEvent` cmdlet consumed by a streaming PowerShell process block.
- `GetWinEvent`: `Get-WinEvent` consumed by the same streaming process-block shape.
- `EvtxECmd`: Eric Zimmerman's parser, run as an external process with its version and SHA-256 captured.
- Optional baseline engines use pinned pre-change EventViewerX or PSEventViewer binaries supplied by path.

## Common public work

`Metadata`, `Message`, `StructuredData`, `StructuredDataAndMessage`, and `Full` are run through the same event window and streaming accumulator:

- `Metadata` touches core system fields without messages, XML, properties, attachments, or bookmarks.
- `Message` touches core metadata, the provider-formatted message, and provider display names.
- `StructuredData` touches metadata, properties, and raw XML. PSEventViewer also parses its named `Data` dictionary.
  It does not format the message or decode binary attachments.
- `StructuredDataAndMessage` is the default typed/reporting fast path: it touches the formatted message, properties,
  raw XML, and parsed `Data` dictionary without decoding binary attachments or the legacy message-field map.
- `Full` requests message and structured data together. PSEventViewer additionally materializes its parsed
  `MessageData` fields and decodes binary attachments.
- `MetadataCsv` writes the five fields shown in the public README. The three successful engines must produce the same
  row count, byte count, and SHA-256.
- `ExactRawXml` writes every native event XML fragment inside the same UTF-8 `Events` document. Raw .NET,
  EventViewerX, `Export-EVXEvent`, and `Get-WinEvent` must produce the same bytes and SHA-256.

The `Message`, `StructuredData`, and `Full` cases are therefore common-user-job comparisons rather than promises that
every internal allocation or returned type is identical. Result artifacts expose message characters, XML characters,
property count, structured-field count, message-field count, and attachment bytes. The benchmark does not request
bookmarks; both public APIs keep that optional cost outside these cases.

## What “full” means in EvtxECmd

EvtxECmd does have a full export, but its term does not map to PSEventViewer's `Full` read mode:

- `--json <directory> --jsonf <file> --fj true` exports all available raw event data by converting the event XML to
  JSON. It does not add the Windows provider-formatted `Message` that PSEventViewer `Full` requests.
- `--xml` writes the raw event XML representation.
- The normal EvtxECmd CSV is a fixed 25-column forensic schema. It includes core metadata, map-derived fields such as
  `MapDescription`, `UserName`, `RemoteHost`, `PayloadData1` through `PayloadData6`, and the payload.
- A metrics-only EvtxECmd run still parses every record and converts its payload internally. It is not a metadata-only
  equivalent to `-ReadMode Metadata`.

These behaviors are visible in the
[EvtxECmd source used by the pinned 2026.5.0 build](https://github.com/EricZimmerman/evtx/blob/bfc7f47ccbf65ffc9a3777cde5498db2fdd94664/EvtxECmd/Program.cs).
The benchmark declares separate `Evtx-NativeParse`, `Evtx-ForensicCsv`, `Evtx-FullJson`, and `Evtx-Xml` cases and
reports them in a separate README table.

EvtxECmd is downloaded only as a pinned external benchmark executable. EventViewerX does not reference its parser,
repository, packages, or Rust code.

## Validation and provenance

Every successful lane must process the expected event count with no parser errors. Common-work lanes also require
non-empty event identity checks, an order-sensitive rolling signature, first and last record IDs, and mode-specific
materialization rules. Output lanes must create a non-empty file and record its size and SHA-256 outside the timed
operation in a retained `output-validation.json` sidecar. After validation succeeds, the generated event payload is
deleted so repeated large-log samples do not consume unbounded disk space. A failed lane keeps its output for
diagnosis.

PowerForge records:

- end-to-end duration and engine-reported duration;
- events per second, managed allocation, and peak working set;
- event identity sums, order signature, and first/last record IDs;
- message, XML, property, parsed-field, attachment, and output sizes;
- repository head and dirty state;
- fixture path, size, and SHA-256;
- built host, module, EventViewerX, benchmark-script, optional baseline, and EvtxECmd hashes;
- the explicit EvtxECmd map directory's sorted per-file manifest and aggregate manifest hash;
- .NET, PowerShell, and EvtxECmd versions.

Every generated public table requires at least three rotated iterations. A one-off diagnostic run remains useful while
developing, but the wrapper refuses to publish it into the README.

The scale matrix runs 1,000, 10,000, 100,000, and 1,000,000 event windows when the supplied fixture contains enough
records. The cold-start matrix measures a fresh `evx.exe`, a fresh PowerShell process importing PSEventViewer, and a
fresh PowerShell process running `Get-WinEvent`. Pass `-PSEventViewerPath` with the manifest from the unpacked module
artifact when publishing that table so the PowerShell row includes the real bootstrap and dependency-loading behavior
rather than importing only the raw cmdlet DLL. The matrix is deliberately reported separately because the CLI emits
JSON while the PowerShell lanes consume objects in-process; it answers scheduled-task startup cost, not interchangeable
output throughput.

## Run

Inspect the resolved smoke matrix:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -Case Smoke-Common-Scan-Metadata `
    -Engine DotNet, EventViewerX, GetWinEvent, PSEventViewer `
    -Plan
```

Run a smoke comparison:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -Case Smoke-Common-Scan-Metadata, Smoke-Common-Scan-Message, Smoke-Exact-Export-RawXml `
    -Engine DotNet, EventViewerX, EventViewerXExport, GetWinEvent, PSEventViewer `
    -IterationCount 3
```

Generate the common-work table in the main README from an external large fixture:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -ExpensiveSampleCount 100000 `
    -IterationCount 3 `
    -ReadmeTable Common
```

Generate the scale table:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -ScaleSampleCount 1000, 10000, 100000, 1000000 `
    -IterationCount 3 `
    -ReadmeTable Scale
```

Generate the command cold-start table:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -IterationCount 5 `
    -PSEventViewerPath .\Artefacts\Unpacked\Modules\PSEventViewer\PSEventViewer.psd1 `
    -EventViewerXPortableCliPath .\Artefacts\Cli\Artifacts\DotNetPublish\EventViewerX.Cli\win-x64\net10.0\PortableCompat\evx.exe `
    -ReadmeTable ColdStart
```

Generate HTML, Excel, email, and combined report measurements from the same
typed query and normalized snapshot:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -ExpensiveSampleCount 1000 `
    -IterationCount 3 `
    -ReadmeTable Reporting
```

Generate the byte-identical metadata CSV and raw XML table:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -IterationCount 3 `
    -ReadmeTable ExactOutput
```

Generate the different-schema EventViewerX/EvtxECmd export table:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -EvtxECmdPath C:\Tools\EvtxECmd.exe `
    -EvtxMapsPath C:\Tools\Maps `
    -IterationCount 3 `
    -ReadmeTable NativeOutput
```

Generate the separate EvtxECmd-native workflow table:

```powershell
.\Benchmarks\EventLogParsing\Invoke-EventLogParsingBenchmark.ps1 `
    -LargeFixturePath C:\Temp\Security.evtx `
    -ExpectedLargeCount 1000000 `
    -EvtxECmdPath C:\Tools\EvtxECmd.exe `
    -EvtxMapsPath C:\Tools\Maps `
    -IterationCount 3 `
    -ReadmeTable EvtxNative
```

`-ReadmeTable` owns its curated case and engine matrix so a partial run cannot silently replace a public table. The
wrapper calls PowerForge's document updater only after every measured sample succeeds; failed runs keep their
diagnostic artifacts and leave the last validated table unchanged. Artifacts are written below
`Ignore\Benchmarks\EventLogParsing\Runs` unless `-OutputRoot` is supplied. Keep the small summary and provenance needed
for review, then delete large fixtures and generated CSV, JSON, and XML files.

Use [the event-source benchmark](../EventSources/README.md) for same-boundary
remote `EventViewerX`/`Get-EVXEvent`/`Get-WinEvent` comparisons and
[the local history benchmark](../EventStore/README.md) for transactional write,
indexed/managed query, calendar summary, and typed CSV costs, and
[the watcher burst benchmark](../EventWatcher/README.md) for persistent-host
delivery, loss, duplication, and burst-throughput evidence.
