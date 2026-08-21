# PSEventViewer and EventViewerX

High-performance Windows Event Log tooling for PowerShell and .NET.

PSEventViewer is the thin PowerShell surface. EventViewerX is the reusable C#
engine underneath it. Both use the Windows Event Log API directly for live,
remote, and offline EVTX work; there is no bundled EVTX parser, EvtxECmd
runtime, Rust engine, or provider-specific parsing dependency.

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/PSEventViewer.svg)](https://www.powershellgallery.com/packages/PSEventViewer)
[![PowerShell Gallery downloads](https://img.shields.io/powershellgallery/dt/PSEventViewer.svg)](https://www.powershellgallery.com/packages/PSEventViewer)
[![Test .NET](https://github.com/EvotecIT/EventViewerX/actions/workflows/test-dotnet.yml/badge.svg)](https://github.com/EvotecIT/EventViewerX/actions/workflows/test-dotnet.yml)
[![Test PowerShell](https://github.com/EvotecIT/EventViewerX/actions/workflows/test-powershell.yml/badge.svg)](https://github.com/EvotecIT/EventViewerX/actions/workflows/test-powershell.yml)
[![License](https://img.shields.io/github/license/EvotecIT/EventViewerX.svg)](https://github.com/EvotecIT/EventViewerX)

## One project, one home

PSWinReporting began as an Active Directory event-reporting module. Its second
generation, PSWinReportingV2, broadened that work, while PSEventViewer grew
into the focused PowerShell module and reusable EventViewerX .NET engine used
for modern Windows Event Log automation.

Those projects increasingly solved the same problems from different starting
points. In August 2026, the PSWinReporting and PSWinReportingV2 lines were
frozen and folded into the PSEventViewer/EventViewerX home. Their Git histories
were preserved rather than copied as a source-only snapshot, and the former
`EvotecIT/PSWinReporting` repository was renamed to
[`EvotecIT/EventViewerX`](https://github.com/EvotecIT/EventViewerX). The old
PSWinReporting URL redirects here; the former PSEventViewer repository is now
an archive that points here.

The names users install have not changed:

- PowerShell users continue to install `PSEventViewer`.
- .NET users continue to reference the `EventViewerX` packages.
- `PSWinReporting` and `PSWinReportingV2` remain available as frozen packages
  for existing deployments, but new development belongs here.

The final PSWinReporting 1.x source is retained on the
[`Legacy` branch](https://github.com/EvotecIT/EventViewerX/tree/Legacy), and the
final PSWinReportingV2 source is retained in the
[pre-consolidation snapshot](https://github.com/EvotecIT/EventViewerX/commit/01c0514e7d5058e61dec265abab0f8f287367377).

## Why use it

- Stream local channels, remote channels, offline EVTX files, or structured
  QueryList XML without accumulating the complete result.
- Push event ID, provider, time, record ID, level, keyword, user, and event-data
  filtering into the Windows query engine.
- Choose exactly how much work each record needs: metadata, formatted message,
  structured payload, or the complete projection.
- Request deterministic provider messages such as `en-US`, with explicit
  fallback and render status.
- Query several hosts, channels, or files concurrently and merge results in a
  deterministic order with bounded memory.
- Export directly to CSV, JSON Lines, XML, or native EVTX without passing one
  PowerShell object per event through a file pipeline.
- Use native bookmarks, durable record checkpoints, subscriptions, watchers,
  provider and channel catalogs, classic log management, WEC subscription
  management, and both classic and manifest event writing.
- Query 80 built-in typed event definitions and 10 composite workflows such as
  failed logons, lockouts, group changes, Kerberos failures, AAD Connect
  health, IIS failures, and OS crashes.
- Turn the same normalized result into responsive HTML, an Excel workbook, or
  an email package without querying the event log again.

## Install

```powershell
Install-Module -Name PSEventViewer -Scope CurrentUser
Import-Module PSEventViewer
```

The module supports Windows PowerShell 5.1 and PowerShell 7+. EventViewerX
targets .NET Framework 4.7.2, .NET 8 for Windows, and .NET 10 for Windows.

## Documentation

- [PowerShell guide](Docs/PowerShell-Guide.md): local, remote, offline, large
  logs, export, checkpoints, watchers, administration, WEC, script recovery,
  and writes.
- [EventViewerX .NET guide](Docs/EventViewerX-Guide.md): typed synchronous and
  asynchronous reads, batching, subscriptions, exports, administration, and
  writes.
- [Custom provider guide](Docs/Custom-Providers.md): PowerShell hashtables,
  JSON, typed C#, build/install, signing/trust, named writes, upgrades, repair,
  rollback, and removal.
- [Custom event definitions](Docs/Event-Definitions.md): one portable typed
  schema shared by query, reports, watchers, WEC, C#, and `evx.exe`.
- [Troubleshooting](Docs/Troubleshooting.md): performance, permissions,
  remoting, message resources, EVTX, checkpoints, and provider deployment.
- [Documentation index](Docs/README.md),
  [event query benchmark contract](Benchmarks/EventLogParsing/README.md), and
  [local history benchmark contract](Benchmarks/EventStore/README.md).

## PowerShell quick start

```powershell
# Fast system-field scan. No provider message or XML is materialized.
Get-EVXEvent -LogName Security -EventId 4624, 4625 `
    -TimePeriod Last24Hours -ReadMode Metadata -MaxEvents 1000

# Deterministic English messages.
Get-EVXEvent -LogName System -Level 1, 2 `
    -ReadMode Message -MessageCulture en-US -MaxEvents 100

# Provider-only discovery searches all channels linked to the provider.
Get-EVXEvent -ProviderName Microsoft-Windows-Kernel-General `
    -EventId 12 -ReadMode Metadata -MaxEvents 10

# Get-WinEvent-compatible hashtable, including a named EventData key.
Get-EVXEvent -FilterHashtable @{
    LogName       = 'Security'
    Id            = 4625
    StartTime     = (Get-Date).AddHours(-1)
    TargetUserName = 'alice'
} -MaxEvents 100

# Offline EVTX, oldest first.
Get-EVXEvent -Path C:\Logs\Security.evtx -Oldest `
    -ReadMode StructuredData `
    -NamedDataFilter @{ TargetUserName = 'alice' }

# Query several remote hosts. Healthy targets can continue when one fails.
Get-EVXEvent -LogName Security -MachineName DC01, DC02 `
    -EventId 4740 -MaxConcurrency 4 -ContinueOnError -MaxEvents 500

# Query reusable event types. The type owns its logs, providers, IDs, and projection.
Get-EVXEvent -Type ADUserLogonFailed, ADUserLockouts `
    -MachineName DC01, DC02 -TimePeriod Last24Hours -MaxEvents 500

# Discover the domain fields, build an exact typed predicate, and inspect its fast path.
$typed = New-EVXFilter -Type ADUserLogonFailed
$typed.Fields | Get-Member -MemberType Property
$typed.Fields.Who |
    Select-Object Name, Description, ValueType, FilterStage, SupportedOperators
Get-EVXEvent -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' -and $_.IpAddress -notin @('-', '::1') } `
    -TimePeriod Last24Hours -Explain

# One query, two polished files, and the same typed rows for downstream automation.
$report = Show-EVXEvent -Type ActiveDirectoryAuthentication `
    -Collector WEC01 -TimePeriod Last24Hours `
    -HtmlPath .\Authentication.html -ExcelPath .\Authentication.xlsx `
    -PassThru

# Build a typed filter once and reuse it across query, export, watcher, and WEC.
$failedLogon = New-EVXFilter -EventId 4625 -TimePeriod Last24Hours `
    -NamedDataExcludeFilter @{ TargetUserName = 'svc_legacy' }
Get-EVXEvent -LogName Security -Filter $failedLogon -ReadMode StructuredData
```

See the focused scripts in [Examples](Examples/) for live, remote, offline,
export, watcher, catalog, administration, collector, event-writing, and
PowerShell-script-recovery workflows.

## Read modes

The general query default is `Message`: it gives interactive users readable
events without paying for XML and structured payload parsing. Choose an
explicit mode for automation and benchmarks.

| Read mode | Materialized work | Best use |
| --- | --- | --- |
| `Metadata` | System fields only. No message, XML, payload dictionary, attachments, or bookmark unless requested. | Counts, timelines, filtering, record IDs, compact scans. |
| `Message` | Metadata, provider display values, provider-formatted message, and lazy parsed message fields. | Human-readable triage and text search. |
| `StructuredData` | Metadata, typed properties, raw XML, and named/unnamed payload data. No formatted message. | Field automation, `-ExpandData`, and schema-preserving analysis. |
| `RawXml` | Metadata and raw event XML without provider formatting or typed payload projection. | Lowest-cost XML streaming and custom downstream parsers. |
| `Full` | Message and structured data together, including decoded attachments when present. | Consumers that genuinely need every projection. |

Bookmarks are opt-in with `-IncludeBookmark`. The returned event's
`BookmarkXml` property is portable across Windows PowerShell 5.1 and
PowerShell 7 and can be passed directly to a later `-BookmarkXml` query.
Provider formatting is often the largest per-record cost; use `Metadata`,
`RawXml`, or `StructuredData` when a formatted message is not needed.

```powershell
# English first, then another installed resource culture if English is absent.
Get-EVXEvent -LogName Application -ReadMode Message `
    -MessageCulture en-US -FallbackMessageCulture de-DE
```

An event exposes message-render status, so a missing provider resource is not
silently treated as a valid empty message.

## Filtering parity

`Get-EVXEvent` supports the natural `Get-WinEvent` query forms:

- `-LogName`, `-ProviderName`, `-Path`, `-FilterXPath`,
  `-FilterHashtable`, and `-FilterXml`;
- arrays and wildcards for channels, providers, and files;
- ID, record ID, time, level, keyword, user, unnamed `Data`, and named
  EventData keys;
- QueryList `Select` and `Suppress` clauses, with truthful per-query
  diagnostics through `-TolerateQueryErrors`;
- credentials and authentication for remote Windows Event Log sessions;
- newest-first or `-Oldest`, `-MaxEvents` as a 64-bit count, and cancellation.

PSEventViewer additionally provides reusable `EventFilter` objects,
`-NamedDataFilter`,
`-NamedDataExcludeFilter`, `-MessageRegex`, time-period shortcuts,
multi-source concurrency, per-source failure continuation, output expansion,
native bookmarks, and durable checkpoints.

Named-data exclusions are emitted as native QueryList `Suppress` clauses.
This keeps events that do not contain the named field—something the Windows
Event Log raw XPath subset cannot express safely with `!=`. Consequently,
`New-EVXFilter -NamedDataExcludeFilter -LogName ...` returns QueryList XML.
The `-AsXPath` form rejects native suppressions rather than producing a
subtly incorrect filter.

The canonical filter workflow is typed first and text only when another tool
requires it:

```powershell
$filter = New-EVXFilter -EventId 4624, 4625 -TimePeriod Last24Hours

# Reuse the same object in PSEventViewer.
Get-EVXEvent -LogName Security -Filter $filter
Export-EVXEvent -LogName Security -Filter $filter `
    -OutputPath C:\Exports\Security.jsonl -Format JsonLines

# Compile it for a native consumer when needed.
Get-WinEvent -LogName Security -FilterXPath (
    New-EVXFilter -EventId 4624, 4625 -TimePeriod Last24Hours -AsXPath
)
```

Typed definitions add a discoverable predicate layer above native event-log
filters. `New-EVXFilter -Type` and `-Definition` return a builder whose
`Fields` properties are the actual domain schema, including aliases,
descriptions, value kinds, supported operators, and whether each comparison
can be pushed down. PowerShell tab completion therefore exposes `Who`,
`IpAddress`, `Action`, and other meaningful fields without requiring users to
invent a hashtable key or memorize provider XML.

```powershell
$auth = New-EVXFilter -Type ActiveDirectoryAuthentication
$auth.AllOf(
    $auth.Fields.Who.MatchesWildcard('CONTOSO\*'),
    $auth.Fields.IpAddress.NotIn('-', '::1'))

Get-EVXEvent -Filter $auth -TimePeriod Last24Hours

# The script-block form accepts only a restricted comparison expression. It is
# parsed, never invoked, so commands and unrelated variables are rejected.
Get-EVXEvent -Type ActiveDirectoryAuthentication `
    -Where { $_.Who -like 'CONTOSO\*' -and $_.IpAddress -notin @('-', '::1') }

# Inspect the complete schema or plan without reading event sources.
Get-EVXEvent -Type ADUserLogonFailed -Describe
New-EVXFilter -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' } -Explain
```

`-Explain` returns the planned native/managed stages without reading events.
Common metadata is pushed into Windows Event Log or indexed SQLite columns;
the complete predicate is always verified against the normalized typed row.

The predicate model is portable JSON. Generate it from the discoverable
builder instead of hand-authoring a provider hashtable, then use the same file
from the low-startup CLI:

```powershell
$auth = New-EVXFilter -Type ADUserLogonFailed
$auth.Fields.Who.MatchesWildcard('CONTOSO\*').ToJson() |
    Set-Content -LiteralPath .\failed-logons.filter.json -Encoding utf8

evx query --type ADUserLogonFailed `
    --where .\failed-logons.filter.json --explain
```

```powershell
$xml = @'
<QueryList>
  <Query Id="0">
    <Select Path="Security">*[System[(EventID=4624 or EventID=4625)]]</Select>
    <Suppress Path="Security">*[EventData[Data[@Name="TargetUserName"]="svc-noisy"]]</Suppress>
  </Query>
</QueryList>
'@

Get-EVXEvent -FilterXml $xml -ReadMode StructuredData -MaxEvents 1000
```

## Large logs and direct export

`Get-EVXEvent` streams detached records. `Export-EVXEvent` is faster for a
durable file because the compiled cmdlet connects the shared engine directly
to the writer.

```powershell
# Lowest-overhead, byte-stable interchange representation.
Export-EVXEvent -Path C:\Logs\Security.evtx `
    -OutputPath C:\Exports\Security.xml -Format Xml -Oldest -Force

# Complete structured output with deterministic English messages.
Export-EVXEvent -Path C:\Logs\Security.evtx `
    -OutputPath C:\Exports\Security.jsonl -Format JsonLines `
    -ReadMode Full -MessageCulture en-US -Oldest -Force

# Bounded remote export written on the caller.
Export-EVXEvent -LogName System -MachineName DC01 `
    -OutputPath C:\Exports\DC01-System.csv -Format Csv `
    -ReadMode Message -MessageCulture en-US -BufferCapacity 64 -Force

# Native EVTX export, with provider resources archived for portability.
Export-EVXEvent -LogName System `
    -OutputPath C:\Exports\System.evtx -Format Evtx `
    -ArchiveResources -Force
```

CSV and JSON Lines honor `ReadMode`. XML streams raw native event XML inside
one well-formed `Events` document. Native EVTX export is local-only because
Windows creates the file in the target session; remote CSV, JSON Lines, and XML
are supported.

Exports write a temporary sibling, flush and optionally hash it, and atomically
promote it only after success. Cancellation or a corrupt input does not replace
an existing destination. Use `-SkipHash` only when another layer already
validates integrity.

## Checkpoints, bookmarks, and real-time events

```powershell
# Durable polling checkpoint. Progress is scoped by source and generation.
Get-EVXEvent -LogName Security -EventId 4625 `
    -RecordIdFile "$env:TEMP\failed-logons.state" `
    -RecordIdKey security-failures

Reset-EVXEventCheckpoint `
    -Path "$env:TEMP\failed-logons.state" `
    -Key security-failures -PassThru

# Bounded native subscription exposed as a PowerShell watcher.
$watcher = Start-EVXWatcher -Name FailedLogons `
    -LogName Security -EventId 4625 -Start Future `
    -StopAfter 10 -TimeOut (New-TimeSpan -Minutes 30) `
    -Action { param($Event) $Event | Select-Object Id, TimeCreated, Data }

Stop-EVXWatcher -Id $watcher.Id -Confirm:$false
```

When `-Key` (or its `-RecordIdKey` alias) is supplied, the reset covers both
the base key and every existing per-source checkpoint derived from it.

The C# `EventLogSubscription` uses native `EvtSubscribe`, bounded channels,
real backpressure, explicit start/bookmark behavior, cancellation, and
terminal/non-terminal failure reporting.

## Provider, channel, classic log, and collector administration

```powershell
# Detached provider metadata; -IncludeEvents adds template definitions.
Get-EVXProvider -Name Microsoft-Windows-PowerShell -IncludeEvents
Get-EVXProvider -Name 'Microsoft-Windows-Kernel-*' -NameOnly

# Channel inventory and health probe.
Get-EVXLog -LogName 'Microsoft-Windows-PowerShell/*' -Force
Test-EVXLog -LogName System -MaxEventsToScan 100

# Manifest channel policy.
Set-EVXLog -LogName Microsoft-Windows-PowerShell/Operational `
    -Enabled $true -MaximumSizeMB 64 -Mode Circular

# Classic log and source lifecycle.
New-EVXLog -LogName Contoso-App -ProviderName Contoso-App-Source `
    -MaximumKilobytes 20480 -OverflowAction OverwriteAsNeeded
New-EVXSource -LogName Application -SourceName Contoso-App
Clear-EVXLog -LogName Contoso-App -BackupPath C:\EventBackups
Remove-EVXSource -LogName Application -SourceName Contoso-App
Remove-EVXLog -LogName Contoso-App

# Windows Event Collector readiness, source-initiated definitions, and runtime health.
Get-EVXCollectorSubscription -Readiness
$domainControllersSid = (Get-ADGroup 'Domain Controllers').SID.Value
$subscription = New-EVXCollectorSubscription `
    -Name 'Domain controller authentication' `
    -SubscriptionType SourceInitiated `
    -CollectorHostName WEC01.ad.contoso.com `
    -AllowedSourceSid $domainControllersSid `
    -Type ActiveDirectoryAuthentication `
    -Description 'Typed authentication events from domain controllers'
$subscription | Set-EVXCollectorSubscription `
    -InitializeCollector -Confirm:$false
$subscription.SourceSubscriptionManagerValue
Get-EVXCollectorSubscription -Name $subscription.SubscriptionId `
    -IncludeRuntimeStatus
Set-EVXCollectorSubscription -Name 'Domain Controllers' `
    -Enabled $true -Confirm:$false
```

Collector inventory can target a remote collector. Updates are deliberately
local-only because the Windows Event Collector write API does not define a
remote session contract. Source-initiated forwarding additionally requires the
returned `SourceSubscriptionManagerValue` in the sources' Event Forwarding
SubscriptionManager policy. Security-log forwarding runs as Network Service,
so preserve the channel's existing access descriptor and grant that identity
read access where it is not already present. Domain controllers require their
Domain Controllers group SID (RID 516) or explicit computer SIDs; the generic
Domain Computers ACE is not sufficient. Runtime status exposes processed-event
counters, source heartbeats, and the exact Windows error codes.
Affected Windows Server 2025 builds can crash the Event Log service while
evaluating any filtered native XPath against `ForwardedEvents`. EventViewerX
therefore opens that collector channel once with `*` and applies the complete
typed selection through a bounded ordered streaming path. Direct live logs and
EVTX files retain their selective native-query fast path. Raw filtered XPath
and structured `QueryList` input for `ForwardedEvents` are rejected before they
can trigger the operating-system defect. Use `-MaxEventsScanned` for an
explicit generic collector scan ceiling; typed/custom queries apply their
`MaxCandidates` ceiling to the raw collector stream on this compatibility path.

## Writing events

Classic Event Log sources and manifest/ETW providers are different Windows
contracts, so the module keeps them explicit.

```powershell
# Classic log write. Source creation is an explicit administrative opt-in.
Write-EVXEvent -LogName Application -ProviderName Contoso-App `
    -Id 1000 -Message 'Service started' -CreateSource

# Registered manifest provider write. Values are validated against the
# provider template and converted to the declared native types.
$result = Write-EVXEvent `
    -ProviderName Microsoft-Windows-PowerShell `
    -Id 4100 `
    -Payload @('Context', 'User data', 'Payload')

$result | Select-Object Success, NativeStatus, PayloadCount, Definition
```

`Write-EVXEvent` resolves an exact provider event/version, rejects ambiguous
versions, validates payload count and types, enforces native ETW size limits,
and owns `EventRegister`, `EventWrite`, and `EventUnregister`. Windows still
decides whether a provider's target channel is enabled.

## Custom providers without SDK work on target machines

Describe named, typed fields once in a PowerShell hashtable, JSON file, or C#
model. Build one signed `.evxprovider` on any Windows host that has the module
or library, then install and write by field name on ordinary Windows machines.
Neither builder nor target needs the Windows SDK, Visual Studio, a native
compiler, generated source, or a package repository.

```powershell
$provider = @{
    ProviderName = 'Contoso.Scanner'
    ProviderGuid = '7a87f315-4b5e-40a2-b748-b0cdd8adab41'
    Version      = '1.0.0'
    Events       = @{
        Name    = 'ScanCompleted'
        Id      = 1000
        Message = 'Scan of {ComputerName} found {FindingCount} issues.'
        Fields  = [ordered] @{
            ComputerName = 'String'
            FindingCount = 'UInt32'
        }
    }
}

New-EVXProviderPackage `
    -Definition $provider `
    -OutputPath .\Contoso.Scanner-1.0.0.evxprovider

# Elevated once per target; no build tools are invoked.
Install-EVXProviderPackage `
    .\Contoso.Scanner-1.0.0.evxprovider `
    -Confirm:$false

Write-EVXEvent `
    -ProviderName Contoso.Scanner `
    -EventName ScanCompleted `
    -Data @{
        ComputerName = $env:COMPUTERNAME
        FindingCount = 7
    } `
    -Confirm:$false
```

The [custom provider guide](Docs/Custom-Providers.md) covers PowerShell, JSON,
typed C#, advanced schemas, signing/trust, remote deployment, named writes,
version compatibility, transactional upgrades, repair, rollback, inventory,
uninstall, security boundaries, and CI/CD. Runnable starting points are
[`Build-CustomProvider.ps1`](Examples/Build-CustomProvider.ps1) and
[`CustomProvider.definition.json`](Examples/CustomProvider.definition.json).

## C# quick start

```csharp
using EventViewerX;
using System.Globalization;

var filter = new EventFilter {
    EventIds = new[] { 4624, 4625 },
    StartTime = DateTime.UtcNow.AddHours(-1),
    NamedData = new Dictionary<string, IReadOnlyList<string>> {
        ["TargetUserName"] = new[] { "alice" }
    }
};

EventLogBatchQuery query = EventQueryPlanner.CreateBatch(
    new EventQueryDefinition {
        LogNames = new[] { "Security" },
        Filter = filter,
        Options = new EventLogQueryOptions {
            ReadMode = EventReadMode.StructuredData,
            MaxEvents = 1_000
        }
    });

await foreach (EventObject item in EventLogEngine.ReadBatchAsync(query)) {
    Console.WriteLine($"{item.RecordId}: {item.Id} {item.ProviderName}");
}

var offline = new EventLogFileQuery(@"C:\Logs\Security.evtx") {
    Oldest = true,
    ReadMode = EventReadMode.Message,
    MessageCulture = CultureInfo.GetCultureInfo("en-US")
};

EventExportResult exported = EventLogExporter.ExportFile(
    offline,
    @"C:\Exports\Security.jsonl",
    EventExportFormat.JsonLines,
    overwrite: true);
```

Multi-source code uses `EventLogBatchQuery` with
`EventLogEngine.ReadBatchAsync`. Scenario code uses `EventTypeQuery` with
`EventTypeEngine.ReadAsync`. Both reuse the same query, native reader,
projection, cancellation, culture, and failure contracts.

```csharp
var typedQuery = new EventTypeQuery(new[] {
    EventType.ADUserLogonFailed,
    EventType.ADUserLockouts
}) {
    MachineNames = new string?[] { "DC01", "DC02" },
    TimePeriod = TimePeriod.Last24Hours,
    MaxConcurrency = 4,
    MaxEvents = 500
};

await foreach (EventTypeRecord item in
               EventTypeEngine.ReadAsync(typedQuery)) {
    Console.WriteLine(
        $"{item.TimeCreated:u} {item.TypeName} {item.MachineName}");
}
```

## Typed reports, Excel, HTML, and email

`Show-EVXEvent` is the single report command. It can query a built-in `-Type`,
a custom `-Definition`, a generic `-LogName`, an offline `-Path`, or consume
existing pipeline objects. It performs the query once and creates every chosen
format from one immutable report snapshot.

```powershell
# A built-in type owns its Security channel and event IDs.
Show-EVXEvent -Type ADUserLogonFailed -TimePeriod Last24Hours

# Generic log browsing remains available when typed semantics are not needed.
Show-EVXEvent -LogName System -EventId 41, 6008 `
    -StartTime (Get-Date).AddDays(-7) -HtmlPath .\Startup.html

# Typed semantics can be applied to an offline file. -LogName is not required.
Show-EVXEvent -Type ActiveDirectoryAuthentication `
    -Path C:\Logs\ForwardedEvents.evtx `
    -HtmlPath .\Authentication.html -ExcelPath .\Authentication.xlsx `
    -CsvPath .\Authentication.zip

# Reuse an existing stream; Show-EVXEvent does not query again.
Get-EVXEvent -LogName Application -Level 1, 2 -MaxEvents 500 |
    Show-EVXEvent -HtmlPath .\Application.html -EmailPackage -PassThru
```

HTML uses typed HtmlForgeX components and is a self-contained, searchable,
responsive, theme-aware report workspace. Its overview is first, report types
are direct navigation pages, and each homogeneous type has column filters,
paging, a column chooser, expandable rows, and a selected-record drawer.
Punctuation-only Windows placeholders are suppressed instead of filling the
screen with dashes. Excel uses
OfficeIMO with an overview, coverage, filters, frozen headers, formatting, and
useful column sizing. A typed leaf definition gets its own domain table and
worksheet. A composite such as `ActiveDirectoryAuthentication` gets one table
and worksheet for each populated leaf type, so logons, privilege use, and
Kerberos events never share an incompatible column set. Those tables contain
the definition fields—account, action, IP address, logon type, GPO name, and so
on—not generic provider or event-ID columns. Excel keeps that technical context
in a separate `Event Provenance` worksheet for audit and troubleshooting.

Typed CSV follows the same homogeneous schema. A single leaf produces one
domain-only `.csv`; a composite produces a `.zip` containing one CSV per
populated leaf plus provenance, coverage, and a manifest. Values that could be
interpreted as spreadsheet formulas are escaped. Generic event queries retain
their technical event columns.

`-LogName` and untyped pipeline input intentionally produce a generic event
view with time, event ID, level, provider, source, message, and provider data.
Only a very wide generic provider payload may be collapsed into `Details`;
typed and custom definitions always retain their declared columns.

`-EmailPackage` returns `Subject`, responsive `Html`, `PlainText`, inline
resources, attachments, and estimated size. That transport-neutral object can
be handed to Mailozaurr, Microsoft Graph, TeamsX/PSTeams, or another delivery
adapter without making those modules dependencies of PSEventViewer. The
portable `evx.exe` includes Mailozaurr and can deliver directly from a JSON
SMTP profile whose password is read from an environment variable.

```powershell
# Mailozaurr stays optional for interactive PowerShell users.
$email = Show-EVXEvent -Type ADUserLogonFailed `
    -TimePeriod Last24Hours -EmailPackage

Send-EmailMessage -Server 'smtp.contoso.com' -Port 587 `
    -From 'events@contoso.com' -To 'operations@contoso.com' `
    -Subject $email.Subject -HTML $email.Html -Text $email.PlainText `
    -Credential $credential -SecureSocketOptions StartTls
```

```json
{
  "Server": "smtp.contoso.com",
  "Port": 587,
  "SecureSocketOptions": "StartTls",
  "From": "events@contoso.com",
  "To": [ "operations@contoso.com" ],
  "UserName": "events@contoso.com",
  "PasswordEnvironmentVariable": "EVX_SMTP_PASSWORD",
  "Subject": "{Title}"
}
```

```powershell
$env:EVX_SMTP_PASSWORD = '<secret supplied by the scheduler or secret store>'
evx report --type ActiveDirectoryAuthentication --collector WEC01 `
    --since 1.00:00:00 --html .\Authentication.html `
    --drawer-placement Auto `
    --excel .\Authentication.xlsx --mail-profile .\smtp.json
```

For C#, reporting is a separate, intentional package boundary:

```csharp
using EventViewerX;
using EventViewerX.Reporting;

var request = EventReportRequest.ForTypes(
    EventType.ADUserLogonFailed,
    EventType.ADUserLockouts);
request.TimePeriod = TimePeriod.Last24Hours;
request.Collectors = new string?[] { "WEC01" };

EventReport report = await EventReportEngine.QueryAsync(request);
foreach (EventReportSection section in report.Sections) {
    Console.WriteLine($"{section.DisplayName}: {section.Rows.Count} rows");
}
EventReportHtmlRenderer.Save(report, "Authentication.html", new EventReportHtmlOptions {
    RecordDrawerPlacement = HtmlForgeX.MonitoringRecordDrawerPlacement.Auto
});
EventReportExcelRenderer.Save(report, "Authentication.xlsx");
EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
```

Interactive HTML supports `Auto`, `Top`, and `Right` selected-record placement.
`Auto` uses the available report width. `Right` prefers a split view but still
falls back above the table below the narrow-screen safety breakpoint. PowerShell
exposes the same contract through `Show-EVXEvent -DrawerPlacement`.

## Optional local event history

`Show-EVXEvent` can persist its already-normalized snapshot into a local
SQLite store and later build typed reports or calendar summaries without
rereading a month of event logs. Storage is optional and is owned by the
`EventViewerX.Storage` package over DbaClientX; it does not add another query
engine or another PowerShell cmdlet family.

```powershell
$store = 'C:\ProgramData\EventViewerX\events.db'

# A scheduled collector run. Overlap is safe: event provenance is deduplicated.
Show-EVXEvent -Type ActiveDirectoryAuthentication `
    -Collector WEC01 -TimePeriod Last15Minutes `
    -StorePath $store

# Query exact typed fields from history and create every desired format once.
Show-EVXEvent -FromStore $store -Type ADUserLogonFailed `
    -Where { $_.Who -like 'CONTOSO\*' } `
    -StartTime (Get-Date).AddDays(-7) `
    -HtmlPath .\FailedLogons.html -ExcelPath .\FailedLogons.xlsx `
    -CsvPath .\FailedLogons.csv

# Exhaustive UTC calendar aggregation uses SQL unless a domain predicate needs
# managed verification. MaxCandidates remains the explicit scan safety bound.
Show-EVXEvent -FromStore $store -Type ActiveDirectoryAuthentication `
    -StartTime (Get-Date).AddMonths(-1) -SummaryPeriod Day `
    -HtmlPath .\Authentication-Daily.html -ExcelPath .\Authentication-Daily.xlsx
```

Writes, schema registration, and an optional checkpoint commit are one
transaction. Repeated ingestion is idempotent. Typed/custom schema changes
fail closed while old rows exist; generic provider payloads remain dynamic.
Stored composite selectors expand to their leaf definitions, so the same
`-Type ActiveDirectoryAuthentication` selector works against live channels,
ForwardedEvents, and retained history. Direct and WEC copies of the same
source event share one provenance identity instead of inflating summaries.
Use `evx store prune --path events.db --before 2026-01-01T00:00:00Z` for an
explicit retention boundary. EventViewerX intentionally does not own alert
escalation, incident assignment, fleet policy, or delivery credentials.

## Portable host and event-triggered automation

The optional `evx.exe` is the low-startup, no-module host for Task Scheduler,
event-triggered tasks, services, containers, and portable automation. It ships
as both a smaller framework-dependent build and a runtime-bundled
`PortableCompat` build. Both provide `types`, `query`, `report`, `watch`,
`store`, `collector`, and `provider` workflows over the same EventViewerX engines; they
do not introduce a second query or reporting implementation.

```powershell
# Process the exact record that started a scheduled task.
evx report --type ADUserLogonFailed --record-id 123456 `
    --html C:\Reports\FailedLogon.html --mail-profile C:\EVX\smtp.json

# Continuously batch matching events and create an outbox or send mail.
evx watch --type ActiveDirectoryAuthentication --collector WEC01 `
    --interval 00:05:00 --mail-profile C:\EVX\smtp.json `
    --ready-file C:\EVX\ready --summary-file C:\EVX\last-run.json

# Inspect or manage collector definitions without adding more PowerShell cmdlets.
evx collector create --name FailedLogons --source DC01,DC02 `
    --type ADUserLogonFailed --output .\FailedLogons.xml --apply
evx collector remove --name FailedLogons

# Ingest once, summarize later, and prune explicitly.
evx query --type ActiveDirectoryAuthentication --collector WEC01 `
    --since 00:15:00 --write-store C:\EVX\events.db
evx report --store C:\EVX\events.db --type ADUserLogonFailed `
    --summary Day --html C:\Reports\FailedLogons-Daily.html
evx store prune --path C:\EVX\events.db --before 2026-01-01T00:00:00Z
```

In the exact-artifact, five-launch cold-start matrix, the framework-dependent
and portable hosts reached their command in 115 ms and 116 ms median. Importing
the unpacked PSEventViewer module and a fresh `Get-WinEvent` process both took
590 ms median. Use the module for interactive composition and the CLI—about
5.1 times faster in this startup workload—where task-trigger latency matters.

## Native PowerShell parity

The goal is parity with the event-log automation contracts that belong in a
library and module, then a stronger reusable surface. A GUI clone of
`Show-EventLog` is intentionally outside that scope.

| Native surface | PSEventViewer/EventViewerX status | Added capability |
| --- | --- | --- |
| `Get-WinEvent` live, remote, path, XPath, hashtable, XML, list log/provider | Covered | Bounded multi-source engine, named data filters, deterministic culture, typed filters, explicit diagnostics, direct exports. |
| `Get-WinEvent` provider messages and raw event data | Covered | Five explicit read modes, render status, event-specific fallback culture, lazy expensive projections. |
| `New-WinEvent` manifest provider writes | Exceeded by `Write-EVXEvent` | Positional compatibility plus named dictionaries, typed payloads, cached registration, package event names, strict schema conversion, structured result. |
| Custom manifest provider authoring and deployment | Additional capability | Typed/hashtable definitions, localization/maps, deterministic SDK build, signed portable package, SDK-free transactional install/upgrade/rollback/uninstall, immutable schema checks. |
| `Get/New/Remove/Clear/Limit/Write-EventLog` classic APIs | Covered by canonical EVX administration cmdlets | Explicit source ownership, verification results, backup-before-clear, consistent local/remote boundaries. |
| `EventLogWatcher` / native subscription | Covered | Bounded backpressure, cancellation, bookmarks, watcher lifecycle and PowerShell actions. |
| `wevtutil` channel/export work | Covered for query, policy, archive, and export | Atomic output, hashes, culture/projection choices, compiled streaming. |
| Windows Event Collector subscriptions | Additional capability | Typed inventory and local mutation with truthful remote limits. |
| Scenario interpretation | Additional capability | Reusable event-type rules and optional bounded DnsClientX enrichment. |

## PowerShell command surface

Version 4 intentionally exposes one canonical command for each responsibility:

| Area | Commands |
| --- | --- |
| Query, report, and export | `Get-EVXEvent`, `Show-EVXEvent`, `New-EVXFilter`, `Export-EVXEvent` |
| Catalog and diagnostics | `Get-EVXLog`, `Get-EVXProvider`, `Test-EVXLog` |
| Watchers and checkpoints | `Start-EVXWatcher`, `Get-EVXWatcher`, `Stop-EVXWatcher`, `Reset-EVXEventCheckpoint` |
| Forensic script recovery | `Get-EVXPowerShellScript` |
| Log and source administration | `New-EVXLog`, `Set-EVXLog`, `Clear-EVXLog`, `Remove-EVXLog`, `New-EVXSource`, `Remove-EVXSource`, `Update-EVXLogArchive` |
| Event writing | `Write-EVXEvent` |
| Custom providers | `Test-EVXProviderDefinition`, `New-EVXProviderPackage`, `Get-EVXProvider`, `Install-EVXProviderPackage`, `Uninstall-EVXProviderPackage` |
| Collector subscriptions | `New-EVXCollectorSubscription`, `Get-EVXCollectorSubscription`, `Set-EVXCollectorSubscription` |
| PowerShell recovery | `Get-EVXPowerShellScript` (`-Execution` selects execution records) |

The 27 cmdlets and 80 leaf event types are intentionally different counts.
Cmdlets are reusable workflows; event types are catalog values within those
workflows. Adding a type does not add another query, report, watcher, or WEC
cmdlet. Ten composite types select coherent groups of leaf types for common
operational reports.

Three migration aliases remain: `Find-WinEvent` maps to `Get-EVXEvent`,
`Get-EVXFilter` maps to `New-EVXFilter`, and `Write-EVXEntry` maps to the
classic parameter set of `Write-EVXEvent`. Existing classic calls such as
`Write-EVXEntry -LogName Application -Source LegacyApp -EventId 1000 -Message 'Started'`
continue to bind; `-Source` is an alias of `-ProviderName`.

## Version 4 migration

Version 4 is a deliberate API cleanup:

- C# callers use `EventLogEngine`, `EventTypeEngine`,
  `ClassicEventLogManager`, `EventLogCatalog`, `EventLogSubscription`,
  `EventLogExporter`, and `ManifestEventWriter`; the monolithic
  `SearchEvents` API is removed.
- PowerShell uses the canonical commands above. Provider package inventory is
  part of `Get-EVXProvider`; script and execution recovery share
  `Get-EVXPowerShellScript`; classic and manifest writes share
  `Write-EVXEvent`.
- Typed results are `EventTypeRecord` objects with a stable
  `SourceEvent`, `TypeName`, `EventId`, `RecordId`, `MachineName`, and
  `SourceLogName` envelope. These detached records can be selected, serialized,
  or handed to mail and Teams adapters without adding either dependency.
- General queries default to `ReadMode Message`, not eager `Full`.
- Bookmarks are opt-in. Durable polling uses explicit checkpoint files/keys.
- `MaxEvents` and counters are 64-bit.
- Payload and parsed message projections are lazy where the chosen mode allows
  it.
- Native EVTX export is local-only; remote CSV, JSON Lines, and XML are
  supported and written locally.

These are breaking changes intended to remove duplicate behavior and make cost,
ownership, and failure boundaries predictable.

## Performance evidence

The [event query PowerForge suite](Benchmarks/EventLogParsing/README.md)
separates byte-identical comparisons from common public jobs and
different-schema native exports. The independent
[local history suite](Benchmarks/EventStore/README.md) measures transactional
ingestion, indexed and managed typed queries, UTC calendar summaries, and typed
CSV from identical normalized rows. Every published result requires at least
three rotated iterations plus contract validation.

The current v4 scale, typed-report, and cold-start runs use a
225,513,472-byte Security EVTX containing 201,672 readable events
(`4F61E29AEAC9D3D7DDE4EE74CF8EE7AB9C5A4BF21FBE610E326A91566CC2A383`).
The remote matrix pins a live AD0 record boundary so all three engines read
the same records. Those runs used the same 32-logical-processor Windows host
with .NET SDK 10.0.400 and PowerShell 7.6.4.

The earlier common-work, byte-identical export, and EvtxECmd-native tables use
a 231,804,928-byte Security EVTX containing 190,645 readable events
(`FF2F428E0D7DD59EEEA3A5D87477AFFECD87C6541DF417261F21E4B144E7D6AD`)
and .NET SDK 10.0.302. EvtxECmd was pinned to
`2026.5.0+bfc7f47ccbf65ffc9a3777cde5498db2fdd94664`
(`DE169B2AC7F6B1E54A684E0CDDDA30223651937B75941B21EA53A98F5A2502EE`);
its 386-file maps manifest was also hashed. Generated payloads are deleted
after their size and SHA-256 are validated, while the small summaries and
provenance remain.

<!-- event-log-common-benchmark:start -->
| Scenario | Host | Operation | PSEventViewer | DotNet | EventViewerX | GetWinEvent | Result |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| Large-Common-Sample-Full | Core-7.6.4 | Scan | 1.00x (12.22s) | 4.00x (48.93s) | 1.12x (13.70s) | 4.70x (57.45s) | PSEventViewer fastest |
| Large-Common-Sample-Message | Core-7.6.4 | Scan | 1.00x (10.45s) | 4.67x (48.78s) | 0.81x (8.51s) | 4.89x (51.12s) | PSEventViewer slower than EventViewerX |
| Large-Common-Sample-StructuredData | Core-7.6.4 | Scan | 1.00x (3.25s) | 1.21x (3.94s) | 0.94x (3.04s) | 8.20x (26.63s) | PSEventViewer slower than EventViewerX |
| Large-Common-Scan-Metadata | Core-7.6.4 | Scan | 1.00x (2.54s) | 0.83x (2.10s) | 0.72x (1.82s) | 17.27x (43.90s) | PSEventViewer slower than EventViewerX |
<!-- event-log-common-benchmark:end -->

Common-public-job rows keep the input window and materialization category
equal, but the public APIs can return different object schemas. Exact-output
rows below require identical bytes and SHA-256.

### Scale and cold-start behavior

The scale matrix uses one real Security EVTX and fixed 1,000, 10,000, and
100,000-event windows. Every cell validates count, record identity, and order;
the table compares public jobs with equivalent materialization categories, not
byte-identical output schemas.

<!-- event-log-scale-benchmark:start -->
| Scenario | Host | Operation | PSEventViewer | DotNet | EventViewerX | GetWinEvent | Result |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| Large-Scale-1000-Full | Core-7.6.4 | Scan | 1.00x (669ms) | 0.88x (590ms) | 0.39x (261ms) | 1.70x (1.14s) | PSEventViewer slower than EventViewerX |
| Large-Scale-1000-Metadata | Core-7.6.4 | Scan | 1.00x (498ms) | 0.23x (116ms) | 0.22x (109ms) | 1.59x (794ms) | PSEventViewer slower than EventViewerX |
| Large-Scale-1000-StructuredDataAndMessage | Core-7.6.4 | Scan | 1.00x (680ms) | 0.90x (613ms) | 0.37x (252ms) | 1.68x (1.14s) | PSEventViewer slower than EventViewerX |
| Large-Scale-10000-Full | Core-7.6.4 | Scan | 1.00x (2.49s) | 1.88x (4.69s) | 0.63x (1.56s) | 2.26x (5.63s) | PSEventViewer slower than EventViewerX |
| Large-Scale-10000-Metadata | Core-7.6.4 | Scan | 1.00x (616ms) | 0.35x (218ms) | 0.30x (183ms) | 4.50x (2.77s) | PSEventViewer slower than EventViewerX |
| Large-Scale-10000-StructuredDataAndMessage | Core-7.6.4 | Scan | 1.00x (2.46s) | 1.95x (4.79s) | 0.60x (1.49s) | 2.33x (5.72s) | PSEventViewer slower than EventViewerX |
| Large-Scale-100000-Full | Core-7.6.4 | Scan | 1.00x (14.63s) | 2.97x (43.40s) | 0.75x (10.92s) | 3.32x (48.59s) | PSEventViewer slower than EventViewerX |
| Large-Scale-100000-Metadata | Core-7.6.4 | Scan | 1.00x (1.80s) | 0.61x (1.09s) | 0.48x (859ms) | 10.85x (19.49s) | PSEventViewer slower than EventViewerX |
| Large-Scale-100000-StructuredDataAndMessage | Core-7.6.4 | Scan | 1.00x (14.44s) | 3.00x (43.27s) | 0.73x (10.56s) | 3.37x (48.65s) | PSEventViewer slower than EventViewerX |
<!-- event-log-scale-benchmark:end -->

Remote evidence includes connection/session and network cost. The wrapper pins
one latest record boundary before the rotated run, and every engine must return
the same ordered record identities. These AD0 results describe this lab and
time; the offline scale table remains the reproducible throughput evidence.

<!-- event-log-remote-benchmark:start -->
| Scenario | Variables | Host | Operation | PSEventViewer | EventViewerX | GetWinEvent | Result |
| --- | --- | --- | --- | ---: | ---: | ---: | --- |
| Remote-AD0-Security-Latest-100-Metadata | EventCount=100, LogName=Security, MachineName=AD0 | Core-7.6.4 | Query | 1.00x (69ms) | 0.70x (48ms) | 7.89x (546ms) | PSEventViewer slower than EventViewerX |
| Remote-AD0-Security-Latest-1000-Metadata | EventCount=1000, LogName=Security, MachineName=AD0 | Core-7.6.4 | Query | 1.00x (377ms) | 0.93x (350ms) | 19.42x (7.32s) | PSEventViewer slower than EventViewerX |
<!-- event-log-remote-benchmark:end -->

Cold-start measurements launch a fresh process for every sample. They answer
the Task Scheduler and event-triggered automation question separately from
steady-state scan throughput.

<!-- event-log-cold-start-benchmark:start -->
| Scenario | Host | Operation | EventViewerXCli | EventViewerXCliPortable | GetWinEvent | PSEventViewer | Result |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| Smoke-Command-Cold-StructuredDataAndMessage | Core-7.6.4 | Scan | 1.00x (115ms) | 1.01x (116ms) | 5.15x (590ms) | 5.14x (590ms) | EventViewerXCli tied with EventViewerXCliPortable |
<!-- event-log-cold-start-benchmark:end -->

Reporting measurements include the typed query and the requested renderer.
`All` creates the interactive HTML report, Excel workbook, and compact email
body in one operation; individual formats avoid work the caller does not need.

<!-- event-log-reporting-benchmark:start -->
| Scenario | Host | Operation | EventViewerXReport | Result |
| --- | --- | --- | ---: | --- |
| Typed-Report-All | Core-7.6.4 | Scan | 1.00x (6.58s) | EventViewerXReport only successful |
| Typed-Report-Email | Core-7.6.4 | Scan | 1.00x (510ms) | EventViewerXReport only successful |
| Typed-Report-Excel | Core-7.6.4 | Scan | 1.00x (6.08s) | EventViewerXReport only successful |
| Typed-Report-Html | Core-7.6.4 | Scan | 1.00x (689ms) | EventViewerXReport only successful |
<!-- event-log-reporting-benchmark:end -->

<!-- event-log-exact-output-benchmark:start -->
| Scenario | Host | Operation | Metric | PSEventViewer | DotNet | EventViewerXExport | GetWinEvent | Result |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- |
| Large-Exact-Export-MetadataCsv | Core-7.6.4 | Scan | MedianMs | 1.00x (3.83s) | 0.69x (2.64s) | Skipped | 12.66x (48.46s) | PSEventViewer slower than DotNet |
| Large-Exact-Export-MetadataCsv | Core-7.6.4 | Scan | OutputBytes | 1.00x (19055567) | 1.00x (19055567) | Skipped | 1.00x (19055567) | PSEventViewer baseline |
| Large-Exact-Export-RawXml | Core-7.6.4 | Scan | MedianMs | 1.00x (3.68s) | 1.30x (4.80s) | 0.85x (3.12s) | 13.58x (50.01s) | PSEventViewer slower than EventViewerXExport |
| Large-Exact-Export-RawXml | Core-7.6.4 | Scan | OutputBytes | 1.00x (293062655) | 1.00x (293062655) | 1.00x (293062655) | 1.00x (293062655) | PSEventViewer baseline |
<!-- event-log-exact-output-benchmark:end -->

EventViewerX and EvtxECmd native formats are not interchangeable. Read these
times together with output bytes and fields; do not turn them into an
unqualified speed claim.

<!-- event-log-native-output-benchmark:start -->
| Scenario | Host | Operation | Metric | EventViewerXExport | EvtxECmd | Result |
| --- | --- | --- | --- | ---: | ---: | --- |
| Large-Native-Output-Csv | Core-7.6.4 | Scan | MedianMs | 1.00x (26.46s) | 1.09x (28.73s) | EventViewerXExport fastest |
| Large-Native-Output-Csv | Core-7.6.4 | Scan | OutputBytes | 1.00x (698462495) | 0.46x (318630958) | EventViewerXExport baseline |
| Large-Native-Output-FullJson | Core-7.6.4 | Scan | MedianMs | 1.00x (32.02s) | 1.27x (40.58s) | EventViewerXExport fastest |
| Large-Native-Output-FullJson | Core-7.6.4 | Scan | OutputBytes | 1.00x (915259866) | 0.32x (292846026) | EventViewerXExport baseline |
| Large-Native-Output-Xml | Core-7.6.4 | Scan | MedianMs | 1.00x (2.85s) | 11.15x (31.78s) | EventViewerXExport fastest |
| Large-Native-Output-Xml | Core-7.6.4 | Scan | OutputBytes | 1.00x (293062655) | 1.12x (329124038) | EventViewerXExport baseline |
<!-- event-log-native-output-benchmark:end -->

EventViewerX full JSON includes provider-formatted messages, typed properties,
named data, render status, raw XML, and attachments. The generated
`OutputBytes` rows make that extra work visible instead of hiding it inside an
unqualified timing claim. EvtxECmd is only a pinned external benchmark target
and is not a source, package, or runtime dependency.

<!-- event-log-evtx-native-benchmark:start -->
| Scenario | Host | Operation | Metric | EvtxECmd | Result |
| --- | --- | --- | --- | ---: | --- |
| Large-Evtx-ForensicCsv | Core-7.6.4 | Scan | MedianMs | 1.00x (26.09s) | EvtxECmd only successful |
| Large-Evtx-ForensicCsv | Core-7.6.4 | Scan | OutputBytes | 1.00x (318630958) | EvtxECmd baseline |
| Large-Evtx-FullJson | Core-7.6.4 | Scan | MedianMs | 1.00x (36.26s) | EvtxECmd only successful |
| Large-Evtx-FullJson | Core-7.6.4 | Scan | OutputBytes | 1.00x (292846026) | EvtxECmd baseline |
| Large-Evtx-NativeParse | Core-7.6.4 | Scan | MedianMs | 1.00x (17.53s) | EvtxECmd only successful |
| Large-Evtx-NativeParse | Core-7.6.4 | Scan | OutputBytes | n/a (0) | EvtxECmd baseline |
| Large-Evtx-Xml | Core-7.6.4 | Scan | MedianMs | 1.00x (31.96s) | EvtxECmd only successful |
| Large-Evtx-Xml | Core-7.6.4 | Scan | OutputBytes | 1.00x (329124038) | EvtxECmd baseline |
<!-- event-log-evtx-native-benchmark:end -->

The committed smoke fixture is small and non-sensitive. Large EVTX fixtures
and generated multi-gigabyte outputs remain external and temporary.

## Runtime dependencies

EventViewerX uses the Windows `wevtapi` contract, DnsClientX for optional
bounded DNS enrichment, and Microsoft/BCL packages such as
`System.Diagnostics.EventLog`, `System.DirectoryServices` for optional Group
Policy enrichment, and compatibility packages required by the .NET Framework
target. PSEventViewer ships the compiled engine and cmdlets; it has no
third-party EVTX parser or PowerShell helper-module dependency.

## Development and release

The root build wrapper delegates versioning, library packaging, module
packaging, signing, artifacts, NuGet, PowerShell Gallery, and GitHub release
coordination to PSPublishModule/PowerForge. EventViewerX and PSEventViewer are
built and released from one version source and validated as packed artifacts.

```powershell
.\Build\Build-Module.ps1 -ConfigurationGateMode Build
```

Browse [the event query benchmark contract](Benchmarks/EventLogParsing/README.md),
[the local history benchmark contract](Benchmarks/EventStore/README.md),
[the event-type architecture](Sources/EventViewerX/Rules/README-Rules-System.md),
the PowerShell [examples](Examples/), and the C#
[examples](Sources/EventViewerX.Examples/) for deeper integrations.
