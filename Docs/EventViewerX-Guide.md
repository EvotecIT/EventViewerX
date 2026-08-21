# EventViewerX .NET guide

EventViewerX is the reusable C# engine beneath PSEventViewer.
It targets .NET Framework 4.7.2, .NET 8 for Windows, and .NET 10 for Windows.
The typed APIs own native query construction, filter partitioning, bounded
multi-source reads, projection, message rendering, export, subscriptions,
catalog/administration, and event writing.

```xml
<PackageReference Include="EventViewerX" Version="4.0.0" />
```

The examples below use:

```csharp
using EventViewerX;
using EventViewerX.Providers;
using System.Globalization;
```

## Stream a local channel

```csharp
var filter = new EventFilter {
    EventIds = new[] { 4624, 4625 },
    StartTime = DateTime.UtcNow.AddHours(-1),
    NamedData = new Dictionary<string, IReadOnlyList<string>> {
        ["TargetUserName"] = new[] { "alice" }
    }
};

var options = new EventLogQueryOptions {
    ReadMode = EventReadMode.StructuredData,
    MaxEvents = 1_000
};

foreach (EventObject item in EventLogEngine.ReadChannel(
             "Security",
             filter,
             options: options)) {
    Console.WriteLine(
        $"{item.RecordId}: {item.Id} {item.ProviderName}");
}
```

Enumeration is lazy. Do not call `ToList()` unless the application actually
needs the complete result in memory.

## Read offline files

```csharp
var options = new EventLogQueryOptions {
    Oldest = true,
    ReadMode = EventReadMode.Metadata
};

foreach (EventObject item in EventLogEngine.ReadFiles(
             new[] {
                 @"C:\Logs\DC01-Security.evtx",
                 @"C:\Logs\DC02-Security.evtx"
             },
             options: options)) {
    Console.WriteLine($"{item.TimeCreated:o} {item.Id}");
}
```

`ReadFile` and `ReadFiles` use the same typed filtering and projection
contracts as live channels.

## Resume from a bookmark

Bookmark creation is opt-in. `EventObject.BookmarkXml` exposes the same
portable string on .NET Framework 4.7.2 and modern .NET:

```csharp
EventObject last = EventLogEngine.ReadChannel(
    new EventLogChannelQuery("System") {
        ReadMode = EventReadMode.Metadata,
        IncludeBookmark = true,
        MaxEvents = 1
    }).Single();

var resumedQuery = new EventLogChannelQuery("System") {
    ReadMode = EventReadMode.Metadata,
    BookmarkXml = last.BookmarkXml
};

foreach (EventObject item in EventLogEngine.ReadChannel(resumedQuery)) {
    // Starts after the bookmarked event by default.
}
```

Persist `BookmarkXml`, not the framework bookmark object. Set
`BookmarkOffset = 0` when the bookmarked event should be included again.

## Read several hosts or channels asynchronously

```csharp
using var cancellation = new CancellationTokenSource(
    TimeSpan.FromMinutes(2));

var failures = new List<EventLogQueryFailure>();
var options = new EventLogQueryOptions {
    ReadMode = EventReadMode.Message,
    MessageCulture = CultureInfo.GetCultureInfo("en-US"),
    MaxConcurrency = 4,
    BufferCapacity = 64,
    ContinueOnError = true,
    FailureHandler = failures.Add
};

await foreach (EventObject item in EventLogEngine.ReadChannelsAsync(
                   new[] { "System", "Application" },
                   new string?[] { "DC01", "DC02" },
                   new EventFilter {
                       Levels = new byte[] { 1, 2 }
                   },
                   options,
                   cancellation.Token)) {
    Console.WriteLine(
        $"{item.QueriedMachine} {item.TimeCreated:o} {item.Message}");
}
```

The merge has bounded per-source buffers. `ContinueOnError` allows healthy
sources to continue and reports isolated failures through `FailureHandler`.
Cancellation closes native handles and remote sessions.

## Choose projection cost

`EventReadMode` is a workload contract:

| Mode | Use |
| --- | --- |
| `Metadata` | System fields and high-volume scans |
| `Message` | Metadata plus localized provider rendering |
| `StructuredData` | Typed/named payload and XML without message rendering |
| `RawXml` | Native XML interchange |
| `Full` | Message plus complete structured projection |

For deterministic English output:

```csharp
var options = new EventLogQueryOptions {
    ReadMode = EventReadMode.Message,
    MessageCulture = CultureInfo.GetCultureInfo("en-US"),
    FallbackMessageCulture = CultureInfo.GetCultureInfo("de-DE")
};
```

Consumers should inspect the message render status when an absent provider
resource must be distinguished from an empty message.

## Build advanced queries

`EventFilter` covers IDs, record boundaries, providers, levels, keywords,
time, users, unnamed data, named data, and exclusions. The query factory
partitions filters that exceed the native Windows expression limit and
consolidates equivalent sources:

```csharp
EventLogBatchQuery batch = EventLogQueryFactory.ForChannels(
    new[] { "Security", "System" },
    new string?[] { null, "DC01" },
    new EventFilter {
        EventIds = Enumerable.Range(1, 100).ToArray(),
        ExcludedEventIds = new[] { 42 }
    },
    new EventLogQueryOptions {
        MaxConcurrency = 4,
        ContinueOnError = true
    });

foreach (EventObject item in EventLogBatchEngine.Read(batch)) {
    // One deterministic merged stream.
}
```

Named-data exclusions are translated into native QueryList suppressions:

```csharp
EventLogBatchQuery batch = EventLogQueryFactory.ForChannels(
    new[] { "Security" },
    filter: new EventFilter {
        EventIds = new[] { 4624, 4625 },
        ExcludedNamedData =
            new Dictionary<string, IReadOnlyList<string>> {
                ["TargetUserName"] = new[] { "svc-noisy" }
            }
    });
```

Use `EventLogQueryFactory`, `BuildChannelQueryXml`, or `BuildFileQueryXml`
for this filter. `EventFilterCompiler.BuildXPath` rejects
`ExcludedNamedData` because the native raw-XPath subset cannot keep records
where that field is absent while excluding a matching value.

Use `EventLogStructuredQuery` for an existing native `QueryList` containing
multiple Select/Suppress paths. A native bookmark identifies one independent
query handle, so bookmarked structured reads must target one channel session
or one offline file; split multi-file or mixed-source reads reject one shared
bookmark instead of seeking unrelated sources.

## Export without object-per-record application code

```csharp
var query = new EventLogFileQuery(
    @"C:\Logs\Security.evtx") {
    Oldest = true,
    ReadMode = EventReadMode.Full,
    MessageCulture = CultureInfo.GetCultureInfo("en-US")
};

EventExportResult result = EventLogExporter.ExportFile(
    query,
    @"C:\Exports\Security.jsonl",
    EventExportFormat.JsonLines,
    overwrite: true);

Console.WriteLine(
    $"{result.EventCount} events, {result.Bytes} bytes, {result.Sha256}");
```

`ExportFile`, `ExportChannel`, `ExportStructured`, and `ExportBatch` write to a
temporary destination and atomically publish the completed file. CSV, JSON
Lines, XML, and native EVTX are supported where the underlying Windows API
supports that source/format combination.

## Subscribe to new events

```csharp
var query = new EventLogSubscriptionQuery("Security") {
    XPath = "*[System[EventID=4625]]",
    Start = EventLogSubscriptionStart.Future,
    ReadMode = EventReadMode.StructuredData,
    BufferCapacity = 256
};

using var subscription = new EventLogSubscription(
    query,
    item => Console.WriteLine(
        $"{item.TimeCreated:o} {item.MachineName}"),
    failure => Console.Error.WriteLine(failure.Exception.Message));

Console.ReadLine();
```

Subscriptions support local/remote sessions, future/oldest/bookmark starts,
strict or tolerant bookmarks, bounded handle queues, and cancellation. A
managed watcher remains active while any partitioned native subscription is
healthy and retires itself after the last subscription reports a terminal
failure.
`EventLogSubscriptionQuery.XPath` also accepts QueryList XML when a
subscription needs native `Suppress` clauses; build it with
`EventFilterCompiler.BuildChannelQueryXml`.
The higher-level `WatcherManager` adds named lifecycle, stop-after, timeout,
and event-type projection.

## Use typed event definitions

```csharp
var query = new EventTypeQuery(new[] {
    EventType.ADUserLogonFailed,
    EventType.ADUserLockouts
}) {
    MachineNames = new string?[] { "DC01", "DC02" }
};

await foreach (EventTypeRecord item in EventTypeEngine.ReadAsync(query)) {
    Console.WriteLine(
        $"{item.TypeName} {item.EventId} {item.MachineName}");
}
```

Typed events are projections over the same engine. A type owns its source
logs, providers, event IDs, filters, and projection, so callers do not supply a
log name. A query may instead point the type at offline files or a collector's
`ForwardedEvents` channel without weakening those semantics.

For a user-defined schema, load an `EventDefinition` and stream it through
`EventDefinitionEngine`. `EventDefinitionQuery` provides the same remote,
offline, collector, time, record ID, result/candidate limit, culture,
checkpoint-observer, and failure contracts. See
[custom event definitions](Event-Definitions.md).

Typed fields are discoverable before a query. Each field describes its value
kind, aliases, supported operators, and filtering stage:

```csharp
EventPredicateBuilder fields = EventPredicateBuilder.ForType(
    EventType.ADUserLogonFailed);

foreach (EventPredicateField field in fields.Fields) {
    Console.WriteLine(
        $"{field.Name}: {field.Definition.ValueType.Name}, " +
        field.Definition.FilterStage);
}

EventPredicate predicate = fields.AllOf(
    fields.Field("Who").MatchesWildcard("CONTOSO\\*"),
    fields.Field("IpAddress").NotIn("-", "::1"));

var query = new EventTypeQuery(new[] { EventType.ADUserLogonFailed }) {
    TimePeriod = TimePeriod.Last24Hours,
    Predicate = predicate
};

EventPredicatePlan plan = EventPredicatePlanner.Plan(predicate);
await foreach (EventTypeRecord item in EventTypeEngine.ReadAsync(query)) {
    Console.WriteLine($"{item.TimeCreated:u} {item.TypeName} {item.SourceComputer}");
}
```

The planner pushes safe common event dimensions to Windows and evaluates
domain fields after typed projection. The complete predicate is always
verified, so optimization cannot weaken the requested semantics. Predicates
are serializable and can also be supplied through the PowerShell and CLI JSON
surfaces. `predicate.ToJson()` emits enum-named, versionable JSON; use
`EventPredicate.ParseJson` when a C# host receives the same contract from a
configuration file.

## Create HTML, Excel, and email output

Install or reference `EventViewerX.Reporting` when presentation is needed.
The reporting assembly keeps HtmlForgeX, HtmlForgeX.Email, and OfficeIMO out of
the low-level query package while reusing EventViewerX for all data access.

```csharp
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
EventReportHtmlRenderer.Save(report, "Authentication.html");
EventReportExcelRenderer.Save(report, "Authentication.xlsx");
EventReportCsvRenderer.Save(report, "Authentication.zip");
EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
```

`EventReport.Sections` is the presentation schema. A leaf definition creates
one homogeneous section containing its domain fields. Composite definitions
create one section per populated leaf type, which becomes a separate HTML
table and Excel worksheet. Generic log queries instead expose the familiar
Windows event metadata. Excel adds `Event Provenance` for typed reports so the
source log, provider, event ID, record ID, and raw message remain available
without crowding the domain worksheets.

The email package is transport-neutral. A host may give its HTML/plain-text
body and resources to Mailozaurr, Microsoft Graph, or another sender without
coupling EventViewerX.Reporting to credentials or delivery policy.

## Store normalized history locally

Reference `EventViewerX.Storage` when a host needs durable local history:

```xml
<PackageReference Include="EventViewerX.Storage" Version="4.0.0" />
```

The package uses DbaClientX for SQLite access and preserves the same normalized
rows and homogeneous reporting schemas:

```csharp
using EventViewerX.Storage;

EventReport current = await EventReportEngine.QueryAsync(
    EventReportRequest.ForTypes(EventType.ActiveDirectoryAuthentication));

var store = new EventStore(@"C:\ProgramData\EventViewerX\events.db");
EventStoreWriteResult written = await store.WriteAsync(current);

var history = new EventStoreQuery {
    Types = new[] { EventType.ADUserLogonFailed },
    StartTime = DateTime.UtcNow.AddDays(-7),
    Predicate = EventPredicate.Compare(
        "Who",
        EventPredicateOperator.MatchesWildcard,
        "CONTOSO\\*")
};

EventStoreQueryPlan plan = EventStore.Plan(history);
EventReport failed = await store.ReadReportAsync(history);
EventStoreSummaryResult daily = await store.SummarizeAsync(
    new EventStoreQuery {
        Types = new[] { EventType.ActiveDirectoryAuthentication },
        StartTime = DateTime.UtcNow.AddMonths(-1)
    },
    EventStoreSummaryPeriod.Day);
```

Writes are transactional and provenance-deduplicated. An optional
`EventStoreCheckpoint` is committed with the rows. Typed/custom structural
schema changes fail closed while rows remain; generic event-data fields are
dynamic. `MaxCandidates` bounds managed predicate scans and reports whether
the bound was reached. Calendar summaries reject `MaxEvents` because a partial
result would not be an honest summary. Retention is explicit through
`PruneBeforeAsync`. `EventStoreQuery.Types` expands composites to their stored
leaf definitions, so a selector such as `ActiveDirectoryAuthentication` has
the same meaning against live channels, ForwardedEvents, and local history.

## Provision and diagnose Windows Event Collector

The reusable core builds both collector- and source-initiated subscription
XML, initializes the local collector, and reports readiness and per-source
runtime evidence. Source policy remains an explicit deployment concern rather
than a hidden remote mutation.

```csharp
string domainControllersSid =
    "S-1-5-21-111111111-222222222-333333333-516";
var definition = new CollectorSubscriptionDefinition {
    SubscriptionId = "Domain controller authentication",
    SubscriptionType = CollectorSubscriptionType.SourceInitiated,
    CollectorHostName = "WEC01.ad.contoso.com",
    AllowedSourceDomainComputersSddl =
        CollectorSourcePolicy.BuildAllowedSourceSddl(new[] {
            domainControllersSid
        }),
    DeliveryMode = CollectorSubscriptionDeliveryMode.Push,
    QueryXml = EventDefinitionCompiler.BuildQueryXml(
        new[] { EventType.ActiveDirectoryAuthentication })
};

Console.WriteLine(definition.SourceSubscriptionManagerValue);
CollectorSubscriptionManager.InitializeCollector();
CollectorSubscriptionManager.ApplyCollectorSubscription(definition);

CollectorSubscriptionRuntimeStatus runtime =
    CollectorSubscriptionManager.GetCollectorSubscriptionRuntimeStatus(
        definition.SubscriptionId);
foreach (CollectorSubscriptionSourceRuntimeStatus source in runtime.Sources) {
    Console.WriteLine(
        $"{source.Address}: {source.Status}, {source.EventsProcessed} events, " +
        $"error 0x{source.LastErrorCode:X8}");
}
```

Deploy `SourceSubscriptionManagerValue` through the source computers' Windows
Event Forwarding SubscriptionManager policy. Security-channel forwarding also
requires Network Service read access on each source. Preserve the channel's
existing access descriptor when adding that ACE. Domain controllers require
their Domain Controllers group SID (RID 516) or explicit computer SIDs; the
generic Domain Computers ACE is insufficient.

Affected Windows Server 2025 builds can terminate the Event Log service when
`ForwardedEvents` evaluates any filtered native XPath, including simple event
ID and `TimeCreated` predicates. EventViewerX therefore opens that channel once
with the native `*` selector and applies the complete typed filter in its
bounded streaming reader. Inclusive time windows still stop after the reader
crosses the ordered boundary; event IDs, providers, original channels, data
fields, record checkpoints, `MaxEvents`, and `MaxCandidates` remain enforced.
Direct live logs and EVTX files retain their selective native-query fast path.
Raw filtered XPath and structured `QueryList` input against `ForwardedEvents`
are rejected before Windows executes them; use `EventFilter`, typed event
definitions, or the high-level query planner instead. Set
`EventLogQueryOptions.MaxEventsScanned` when a generic collector query needs an
explicit raw-scan ceiling; typed/custom queries use their `MaxCandidates`
ceiling for the same purpose.

## Catalog, health, and administration

```csharp
foreach (EventLogDetails log in EventLogCatalog.DisplayEventLogs()) {
    Console.WriteLine(
        $"{log.LogName} Enabled={log.IsEnabled}");
}

EventLogProbeResult health = EventLogProbe.ProbeLatestEvent(
    "System");

ChannelPolicy? policy = EventLogChannelPolicyService.Get(
    "System");

EventLogChannelPolicyService.Apply(new ChannelPolicy {
    LogName = "System",
    MaximumSizeInBytes = 64L * 1024 * 1024,
    Mode = System.Diagnostics.Eventing.Reader.EventLogMode.Circular
});
```

Use `ClassicEventLogManager` for classic logs/sources and
`CollectorSubscriptionManager` for Windows Event Collector inventory and local
updates. Administrative changes normally require elevation.
Managed catalog and administration sessions require a `NetworkCredential` when
`Kerberos`, `Ntlm`, or `Negotiate` is selected explicitly. The current-identity
`EventLogSession` overload cannot enforce a specific authentication package;
use `Default` when current-identity negotiation is acceptable.

## Write events

For classic logs:

```csharp
ClassicEventLogManager.Write(new ClassicEventWriteRequest {
    SourceName = "Contoso-App",
    LogName = "Application",
    Message = "Service started",
    EventId = 1000,
    EntryType = System.Diagnostics.EventLogEntryType.Information
});
```

For a registered manifest provider:

```csharp
using var writer = ResolvedManifestEventWriter.Open(
    "Contoso.Scanner",
    "ScanCompleted");

writer.Write(new Dictionary<string, object?> {
    ["ComputerName"] = Environment.MachineName,
    ["FindingCount"] = 7U
});
```

Reuse one `ResolvedManifestEventWriter` for high-volume writes so the native
provider registration is cached. For compile-time payload contracts, see
[Custom providers](Custom-Providers.md#typed-c-schema-and-writes).

## API selection

| Need | API |
| --- | --- |
| One typed local/remote channel | `EventLogEngine.ReadChannel[Async]` |
| One offline file | `EventLogEngine.ReadFile[Async]` |
| Several channels/hosts/files | `ReadChannels[Async]`, `ReadFiles[Async]` |
| Explicit mixed or structured batch | `EventLogQueryFactory`, `EventLogBatchQuery`, `EventLogBatchEngine` |
| Direct export | `EventLogExporter` |
| Native real-time subscription | `EventLogSubscription` |
| Managed watcher lifecycle | `WatcherManager` |
| Typed event definitions | `EventTypeEngine` |
| HTML, Excel, and email projection | `EventViewerX.Reporting` |
| Durable local history and summaries | `EventViewerX.Storage`, `EventStore` |
| Catalog and health | `EventLogCatalog`, `EventProviderCatalog`, `EventLogProbe` |
| Administration | `EventLogChannelPolicyService`, `ClassicEventLogManager`, `CollectorSubscriptionManager` |
| Provider packages | `EventProviderPackageBuilder`, `EventProviderPackageManager` |
| Manifest writes | `ManifestEventWriter`, `ResolvedManifestEventWriter` |

Runnable source examples are in
[`Sources/EventViewerX.Examples`](../Sources/EventViewerX.Examples), including
the typed
[`custom-provider lifecycle`](../Sources/EventViewerX.Examples/Examples.CustomProviders.cs).
