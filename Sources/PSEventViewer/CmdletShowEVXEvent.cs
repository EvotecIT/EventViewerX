using HtmlForgeX;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Queries or accepts EventViewerX events and creates polished HTML, Excel, or email output.</para>
/// <para type="description">Show-EVXEvent uses one normalized report snapshot for every selected output. A Type owns its source channels and event IDs; LogName is reserved for generic event queries.</para>
/// <para type="description">Typed and custom definitions render only their domain fields. Composite types keep each leaf schema in a separate table and Excel worksheet, while Event Provenance retains the technical Windows event context.</para>
/// </summary>
/// <example>
///   <summary>Open a failed-logon report</summary>
///   <code>Show-EVXEvent -Type ADUserLogonFailed -TimePeriod Last24Hours</code>
///   <para>Queries the definition-owned Security events and opens a self-contained interactive HTML report.</para>
/// </example>
/// <example>
///   <summary>Create HTML and Excel from one query</summary>
///   <code>$filter = New-EVXFilter -Type ActiveDirectoryAuthentication; Show-EVXEvent -Type ActiveDirectoryAuthentication -Where $filter.Fields.Who.Contains('svc-') -Collector WEC01 -HtmlPath .\Auth.html -ExcelPath .\Auth.xlsx -PassThru</code>
///   <para>Reads ForwardedEvents once and renders both formats from the same snapshot.</para>
/// </example>
/// <example>
///   <summary>Render an existing pipeline</summary>
///   <code>Get-EVXEvent -LogName System -EventId 41,6008 | Show-EVXEvent -HtmlPath .\Startup.html</code>
///   <para>Does not query the event log again.</para>
/// </example>
[OutputType(typeof(EventReport))]
[OutputType(typeof(EventAggregationResult))]
[OutputType(typeof(EventOccurrenceResult))]
[OutputType(typeof(EventEmailPackage))]
[Cmdlet(VerbsCommon.Show, "EVXEvent", DefaultParameterSetName = "Input")]
public sealed class CmdletShowEVXEvent : AsyncPSCmdlet {
    private readonly List<object> _input = new();

    /// <summary>Built-in leaf or composite event definitions. Each definition owns its channels and event IDs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Store")]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <summary>Generic event channel. Mutually exclusive with Type.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Store")]
    public string? LogName { get; set; }

    /// <summary>One or more offline EVTX files. Type or Definition may be supplied to apply typed semantics; Path alone creates a generic report.</summary>
    [Alias("PSPath")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path", ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>Custom JSON definition path or an EventDefinition instance.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public object? Definition { get; set; }

    /// <summary>Reusable typed EventPredicate, restricted ScriptBlock, predicate JSON, or predicate JSON file.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public object? Where { get; set; }

    /// <summary>Optional event IDs for a generic LogName query.</summary>
    [Alias("Id")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Store")]
    public int[]? EventId { get; set; }

    /// <summary>Exact event record identifiers, including IDs passed by an event-triggered scheduled task.</summary>
    [Alias("RecordId")]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public long[]? EventRecordId { get; set; }

    /// <summary>Reads normalized rows from a local EventViewerX SQLite store instead of querying event logs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Store")]
    public string? FromStore { get; set; }

    /// <summary>Original source computers used to filter stored rows.</summary>
    [Parameter(ParameterSetName = "Store")]
    public string[]? SourceComputer { get; set; }

    /// <summary>Provider names used to filter stored rows.</summary>
    [Parameter(ParameterSetName = "Store")]
    public string[]? ProviderName { get; set; }

    /// <summary>Existing EventObject or EventTypeRecord values. No source query is performed.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Input")]
    public object? InputObject { get; set; }

    /// <summary>Direct local or remote query targets.</summary>
    [Alias("ComputerName", "ServerName")]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Definition")]
    public string?[]? MachineName { get; set; }

    /// <summary>Windows Event Collector targets. Typed source channels are matched inside ForwardedEvents.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    public string?[]? Collector { get; set; }

    /// <summary>Absolute start time.</summary>
    [Alias("DateFrom")]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public DateTime? StartTime { get; set; }

    /// <summary>Absolute end time.</summary>
    [Alias("DateTo")]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public DateTime? EndTime { get; set; }

    /// <summary>Relative time window.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Maximum report rows. Zero is unlimited.</summary>
    [ValidateRange(0, long.MaxValue)]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public long MaxEvents { get; set; }

    /// <summary>Maximum raw candidates evaluated before exact predicate verification. Stored queries default to 100,000 when omitted; zero is unlimited.</summary>
    [ValidateRange(0, long.MaxValue)]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public long MaxEventsScanned { get; set; }

    /// <summary>Maximum sources opened concurrently.</summary>
    [ValidateRange(1, EventLogLimits.MaximumConcurrency)]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>Reads oldest matches first.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "Store")]
    public SwitchParameter Oldest { get; set; }

    /// <summary>Groups stored events into an hourly, daily, weekly, or monthly report.</summary>
    [Parameter(ParameterSetName = "Store")]
    public EventStoreSummaryPeriod? SummaryPeriod { get; set; }

    /// <summary>Enriches typed IP-address properties through DnsClientX.</summary>
    [Parameter(ParameterSetName = "Type")]
    public SwitchParameter ResolveDns { get; set; }

    /// <summary>
    /// SQLite file that preserves Group Policy names and identity history across report runs.
    /// This is available only with <c>-Type GroupPolicyDirectoryAudit</c> and never performs directory discovery.
    /// </summary>
    [Parameter(ParameterSetName = "Type")]
    public string? ContextStorePath { get; set; }

    /// <summary>Caller-authorized partition used to resolve non-shareable imported Group Policy context.</summary>
    [Parameter(ParameterSetName = "Type")]
    public string? ContextAuthorization { get; set; }

    /// <summary>Remote query credential.</summary>
    [Credential]
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Definition")]
    public PSCredential? Credential { get; set; }

    /// <summary>Remote Windows Event Log authentication package.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Log")]
    [Parameter(ParameterSetName = "Definition")]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Report title.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Self-contained interactive HTML output path.</summary>
    [Parameter]
    public string? HtmlPath { get; set; }

    /// <summary>Preferred location of the selected-record drawer in interactive HTML output.</summary>
    [Parameter]
    public MonitoringRecordDrawerPlacement DrawerPlacement { get; set; } = MonitoringRecordDrawerPlacement.Auto;

    /// <summary>Excel workbook output path.</summary>
    [Parameter]
    public string? ExcelPath { get; set; }

    /// <summary>Homogeneous CSV path, or a .zip bundle path when the report contains multiple typed schemas.</summary>
    [Parameter]
    public string? CsvPath { get; set; }

    /// <summary>Persists the normalized report rows in an optional local EventViewerX SQLite store.</summary>
    [Parameter]
    public string? StorePath { get; set; }

    /// <summary>Returns a responsive transport-neutral email package for Mailozaurr.</summary>
    [Parameter]
    public SwitchParameter EmailPackage { get; set; }

    /// <summary>Opens generated files with the registered desktop applications.</summary>
    [Parameter]
    public SwitchParameter Open { get; set; }

    /// <summary>Returns the normalized report snapshot in addition to generated output.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Optional exact transport or compiled semantic occurrence grouping.</summary>
    [Parameter]
    public EventDuplicateMode DuplicateMode { get; set; }

    /// <summary>Maximum separation for compiled semantic causal-identifier grouping.</summary>
    [Parameter]
    public TimeSpan OccurrenceWindow { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum observations accepted by one occurrence grouping operation.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumOccurrenceObservations { get; set; } = 100000;

    /// <summary>Maximum occurrence groups retained.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumOccurrenceGroups { get; set; } = 25000;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "Input" && InputObject != null) {
            object value = InputObject;
            while (value is PSObject wrapper && wrapper.BaseObject != value) {
                value = wrapper.BaseObject;
            }
            _input.Add(value);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task EndProcessingAsync() {
        if (SummaryPeriod.HasValue && !string.IsNullOrWhiteSpace(StorePath)) {
            throw new PSArgumentException(
                "SummaryPeriod and StorePath cannot be combined because derived summary rows are not durable event history. " +
                "Store source events first, then render summaries from that store.",
                nameof(StorePath));
        }
        EventReport report;
        EventAggregationResult? aggregation = null;
        if (ParameterSetName == "Input") {
            if (_input.Count == 1 && _input[0] is EventAggregationResult aggregationResult) {
                aggregation = aggregationResult;
                report = EventAggregationReportFactory.Create(aggregationResult, Title);
            } else {
                report = EventReportEngine.Create(_input, Title);
            }
        } else if (ParameterSetName == "Store") {
            if (Type.Length > 0 && Definition != null) {
                throw new PSArgumentException(
                    "Type and Definition are mutually exclusive when reading stored reports.",
                    nameof(Definition));
            }
            EventDefinition? storedDefinition = ResolveStoredDefinition(Definition);
            EventPredicateBuilder? storedBuilder = storedDefinition != null
                ? EventPredicateBuilder.ForDefinition(storedDefinition)
                : Type.Length > 0
                    ? EventPredicateBuilder.ForTypes(Type)
                    : null;
            IReadOnlyList<string>? storedDefinitionNames = ResolveStoredDefinitionNames(
                Definition,
                storedDefinition);
            var store = new EventStore(FromStore!);
            object? storedWhere = Where;
            while (storedWhere is PSObject wrapper && wrapper.BaseObject != storedWhere) {
                storedWhere = wrapper.BaseObject;
            }
            if (storedBuilder == null && storedWhere is ScriptBlock) {
                IReadOnlyList<EventReportSectionSchema> schemas = await store.GetSchemasAsync(
                    new EventStoreQuery { DefinitionNames = storedDefinitionNames },
                    CancelToken).ConfigureAwait(false);
                EventReportSectionSchema schema = schemas.Count == 1
                    ? schemas[0]
                    : throw new PSArgumentException(
                        "Stored script-block filters require exactly one discoverable schema. " +
                        $"The current stored selection exposes {schemas.Count} schemas. " +
                        "Supply -Type, -Definition, an EventDefinition object, or a JSON definition file before using -Where.",
                        nameof(Definition));
                storedBuilder = EventPredicateBuilder.ForFields(
                    schema.Name,
                    schema.Columns.Select(static column => new KeyValuePair<string, Type>(
                        column.Name,
                        EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName))),
                    schema.DisplayName,
                    schema.Columns.ToDictionary(
                        static column => column.Name,
                        static column => column.Aliases ?? Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase));
            }
            EventPredicate? storedPredicate = PowerShellEventPredicateAdapter.Resolve(
                Where,
                nameof(Where),
                storedBuilder);
            if (storedPredicate != null && storedBuilder != null) {
                storedPredicate = storedBuilder.Normalize(storedPredicate);
            }
            var query = new EventStoreQuery {
                Types = Type.Length == 0 ? null : Type,
                DefinitionNames = storedDefinitionNames,
                DefinitionSchemas = storedDefinition == null
                    ? null
                    : new[] { EventReportSectionSchema.FromDefinition(storedDefinition) },
                StartTime = StartTime,
                EndTime = EndTime,
                TimePeriod = TimePeriod,
                EventIds = EventId,
                RecordIds = EventRecordId,
                SourceComputers = SourceComputer,
                SourceLogs = string.IsNullOrWhiteSpace(LogName) ? null : new[] { LogName! },
                Providers = ProviderName,
                Predicate = storedPredicate,
                MaxEvents = MaxEvents,
                Oldest = Oldest.IsPresent
            };
            if (MyInvocation.BoundParameters.ContainsKey(nameof(MaxEventsScanned))) {
                query.MaxCandidates = MaxEventsScanned;
            }
            report = SummaryPeriod.HasValue
                ? await store.CreateSummaryReportAsync(query, SummaryPeriod.Value, Title, CancelToken).ConfigureAwait(false)
                : await store.ReadReportAsync(query, Title, CancelToken).ConfigureAwait(false);
        } else if (!string.IsNullOrWhiteSpace(ContextStorePath)) {
            ValidateGroupPolicyContextSelection();
            var query = new GroupPolicyAuditQuery {
                ContextStore = new SqliteEventContextStore(ContextStorePath!),
                AuthorizationContext = ContextAuthorization,
                Paths = Path.Length == 0 ? null : Path,
                MachineNames = Collector ?? MachineName,
                CollectorLogName = Collector == null ? null : "ForwardedEvents",
                StartTime = StartTime,
                EndTime = EndTime,
                TimePeriod = TimePeriod,
                MaxEvents = MaxEvents,
                MaxCandidates = MaxEventsScanned,
                MaxConcurrency = MaxConcurrency,
                Oldest = true,
                Credential = Credential?.GetNetworkCredential(),
                Authentication = Authentication
            };
            report = await GroupPolicyAuditReportEngine.QueryAsync(query, Title, CancelToken)
                .ConfigureAwait(false);
        } else {
            EventReportRequest request;
            if (ParameterSetName == "Type") {
                request = EventReportRequest.ForTypes(Type);
                request.Paths = Path.Length == 0 ? null : Path;
            } else if (ParameterSetName == "Definition") {
                object? definitionValue = Definition;
                while (definitionValue is PSObject wrapper && wrapper.BaseObject != definitionValue) {
                    definitionValue = wrapper.BaseObject;
                }
                EventDefinition definition = definitionValue switch {
                    EventDefinition typed => typed,
                    string path => EventDefinition.Load(path),
                    _ => throw new PSArgumentException("Definition must be an EventDefinition instance or a JSON file path.", nameof(Definition))
                };
                request = EventReportRequest.ForDefinition(definition);
                request.Paths = Path.Length == 0 ? null : Path;
            } else if (ParameterSetName == "Path") {
                request = EventReportRequest.ForFiles(Path);
            } else {
                request = EventReportRequest.ForLog(LogName!);
            }
            request.EventIds = EventId;
            request.RecordIds = EventRecordId;
            request.MachineNames = MachineName;
            request.Collectors = Collector;
            request.StartTime = StartTime;
            request.EndTime = EndTime;
            request.TimePeriod = TimePeriod;
            request.MaxEvents = MaxEvents;
            request.MaxCandidates = MaxEventsScanned;
            request.MaxConcurrency = MaxConcurrency;
            request.Oldest = Oldest.IsPresent;
            request.ResolveDns = ResolveDns.IsPresent;
            request.Credential = Credential?.GetNetworkCredential();
            request.Authentication = Authentication;
            request.Title = Title;
            EventPredicateBuilder? requestBuilder = request.Definition != null
                ? EventPredicateBuilder.ForDefinition(request.Definition)
                : request.Types != null && request.Types.Count > 0
                    ? EventPredicateBuilder.ForTypes(request.Types)
                    : null;
            request.Predicate = PowerShellEventPredicateAdapter.Resolve(
                Where,
                nameof(Where),
                requestBuilder);
            report = await EventReportEngine.QueryAsync(request, CancelToken).ConfigureAwait(false);
        }

        EventOccurrenceResult? occurrences = null;
        if (DuplicateMode != EventDuplicateMode.None) {
            if (aggregation != null || SummaryPeriod.HasValue) {
                throw new PSArgumentException(
                    "DuplicateMode applies to source observations before aggregation or stored summaries, not to already-derived rows.",
                    nameof(DuplicateMode));
            }
            occurrences = EventOccurrenceEngine.Group(report.Rows, new EventOccurrenceOptions {
                Mode = DuplicateMode,
                Window = OccurrenceWindow,
                MaximumObservations = MaximumOccurrenceObservations,
                MaximumGroups = MaximumOccurrenceGroups
            });
            report = EventOccurrenceReportFactory.Create(occurrences, report, Title);
        }

        bool hasDestination = !string.IsNullOrWhiteSpace(HtmlPath) ||
                              !string.IsNullOrWhiteSpace(ExcelPath) ||
                              !string.IsNullOrWhiteSpace(CsvPath) ||
                              !string.IsNullOrWhiteSpace(StorePath) ||
                              EmailPackage.IsPresent;
        string? htmlPath = HtmlPath;
        if (!hasDestination && !PassThru.IsPresent) {
            htmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"EventViewerX-{DateTime.Now:yyyyMMdd-HHmmss}.html");
        }
        if (!string.IsNullOrWhiteSpace(htmlPath)) {
            string saved;
            if (aggregation != null) {
                saved = EventAggregationHtmlRenderer.Save(aggregation, htmlPath!, Title);
                if (Open.IsPresent || !hasDestination) {
                    Process.Start(new ProcessStartInfo(saved) { UseShellExecute = true });
                }
            } else {
                var htmlOptions = new EventReportHtmlOptions { RecordDrawerPlacement = DrawerPlacement };
                saved = EventReportHtmlRenderer.Save(report, htmlPath!, htmlOptions, Open.IsPresent || !hasDestination);
            }
            WriteObject(saved);
        }
        if (!string.IsNullOrWhiteSpace(ExcelPath)) {
            string saved = aggregation == null
                ? EventReportExcelRenderer.Save(report, ExcelPath!)
                : EventAggregationExcelRenderer.Save(aggregation, ExcelPath!, Title);
            if (Open.IsPresent) {
                Process.Start(new ProcessStartInfo(saved) { UseShellExecute = true });
            }
            WriteObject(saved);
        }
        if (!string.IsNullOrWhiteSpace(CsvPath)) {
            string saved = EventReportCsvRenderer.Save(report, CsvPath!);
            if (Open.IsPresent) {
                Process.Start(new ProcessStartInfo(saved) { UseShellExecute = true });
            }
            WriteObject(saved);
        }
        if (!string.IsNullOrWhiteSpace(StorePath)) {
            if (aggregation != null || occurrences != null) {
                throw new PSArgumentException(
                    "Aggregation and occurrence rows are derived output and cannot be written into event history. Store source events instead.",
                    nameof(StorePath));
            }
            var store = new EventStore(StorePath!);
            EventStoreWriteResult stored = await store.WriteAsync(report, cancellationToken: CancelToken).ConfigureAwait(false);
            WriteVerbose($"Stored {stored.Inserted} new rows and skipped {stored.Duplicates} duplicates.");
            WriteObject(store.Path);
        }
        if (EmailPackage.IsPresent) {
            WriteObject(await EventReportEmailRenderer.RenderAsync(report).ConfigureAwait(false));
        }
        if (PassThru.IsPresent) {
            WriteObject(occurrences ?? (object?)aggregation ?? report);
        }
    }

    private void ValidateGroupPolicyContextSelection() {
        if (Type.Length != 1 || Type[0] != EventType.GroupPolicyDirectoryAudit) {
            throw new PSArgumentException(
                "ContextStorePath requires exactly -Type GroupPolicyDirectoryAudit so the persistent context engine owns the complete Group Policy directory-audit timeline.",
                nameof(ContextStorePath));
        }
        if (Where != null || ResolveDns.IsPresent || EventRecordId?.Length > 0) {
            throw new PSArgumentException(
                "ContextStorePath cannot be combined with Where, ResolveDns, or EventRecordId because persistent Group Policy context requires the complete selected timeline.",
                nameof(ContextStorePath));
        }
    }

    private static EventDefinition? ResolveStoredDefinition(object? definition) {
        if (definition == null) {
            return null;
        }
        object value = definition;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        return value switch {
            EventDefinition typed => typed,
            string path when File.Exists(path) => EventDefinition.Load(path),
            string stableName when !string.IsNullOrWhiteSpace(stableName) => null,
            _ => throw new PSArgumentException(
                "Stored Definition must be a stable definition name, EventDefinition instance, or JSON file path.",
                nameof(Definition))
        };
    }

    private static IReadOnlyList<string>? ResolveStoredDefinitionNames(
        object? definition,
        EventDefinition? resolvedDefinition) {

        if (definition == null) {
            return null;
        }
        if (resolvedDefinition != null) {
            return new[] { resolvedDefinition.Name };
        }
        object value = definition;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        return new[] { ((string)value).Trim() };
    }
}
