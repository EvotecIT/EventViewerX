using System.Globalization;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Streams Windows events directly to CSV, JSON Lines, XML, or native EVTX.</para>
/// <para type="description">Uses the EventViewerX native engine and writes directly to the destination without materializing PowerShell objects. Completed output is promoted atomically, so cancellation or failure does not replace an existing file.</para>
/// </summary>
/// <example>
///   <summary>Export a complete EVTX file as JSON Lines with English messages</summary>
///   <code>Export-EVXEvent -Path C:\Logs\Security.evtx -OutputPath C:\Exports\Security.jsonl -Format JsonLines -MessageCulture en-US</code>
///   <para>Streams complete projected events directly to one JSON object per line.</para>
/// </example>
/// <example>
///   <summary>Export only core metadata at maximum throughput</summary>
///   <code>Export-EVXEvent -Path C:\Logs\System.evtx -OutputPath C:\Exports\System.csv -Format Csv -ReadMode Metadata</code>
///   <para>Skips provider messages, XML, and payload parsing while writing a stable CSV schema.</para>
/// </example>
/// <example>
///   <summary>Export filtered raw event XML</summary>
///   <code>Export-EVXEvent -Path C:\Logs\Application.evtx -OutputPath C:\Exports\Errors.xml -Format Xml -XPath "*[System[Level=2]]"</code>
///   <para>Writes matching native event XML fragments inside one well-formed Events document.</para>
/// </example>
/// <example>
///   <summary>Export English messages directly from a remote Security log</summary>
///   <code>Export-EVXEvent -LogName Security -MachineName DC1 -OutputPath C:\Exports\DC1-Security.jsonl -Format JsonLines -ReadMode Full -MessageCulture en-US</code>
///   <para>Uses the bounded native remote reader and avoids a PowerShell object-to-file pipeline.</para>
/// </example>
[OutputType(typeof(EventExportResult))]
[Cmdlet(
    VerbsData.Export,
    "EVXEvent",
    DefaultParameterSetName = "Path",
    SupportsShouldProcess = true)]
public sealed class CmdletExportEVXEvent : PSCmdlet {
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentQueue<ErrorRecord>
        _structuredFailures = new();

    /// <summary>
    /// Path to an offline log accepted by the Windows Event Log API. EVTX is the validated format.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path")]
    [Alias("LiteralPath")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Local or remote Windows event channel name.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Channel")]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>Registered provider names or wildcard patterns.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Provider")]
    public string[] ProviderName { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Complete QueryList XML for a direct multi-channel or multi-file export.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = "Xml")]
    public string FilterXml { get; set; } = null!;

    /// <summary>
    /// Remote computer name. Omit to export the local channel.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    [Alias("ComputerName", "ServerName")]
    public string? MachineName { get; set; }

    /// <summary>Credentials for a remote channel export.</summary>
    [Credential]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for a remote channel export.</summary>
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>
    /// Destination path. The parent directory must already exist.
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string OutputPath { get; set; } = null!;

    /// <summary>
    /// Direct streaming format written by the native engine.
    /// </summary>
    [Parameter]
    public EventExportFormat Format { get; set; } = EventExportFormat.JsonLines;

    /// <summary>
    /// Amount of event data projected into CSV or JSON Lines records.
    /// XML always streams the raw native event XML and ignores this value.
    /// </summary>
    [Parameter]
    public EventReadMode ReadMode { get; set; } = EventReadMode.Full;

    /// <summary>
    /// Native Windows event XPath expression. The default selects every record.
    /// </summary>
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Alias("XPath")]
    public string? FilterXPath { get; set; }

    /// <summary>Reusable typed filter produced by New-EVXFilter or EventViewerX.</summary>
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public EventFilter? Filter { get; set; }

    /// <summary>Event identifiers selected natively.</summary>
    [Alias("Id")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public int[]? EventId { get; set; }

    /// <summary>Event levels selected natively.</summary>
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public EventViewerX.Level[]? Level { get; set; }

    /// <summary>Absolute beginning of the event time range.</summary>
    [Alias("DateFrom")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public DateTime? StartTime { get; set; }

    /// <summary>Absolute end of the event time range.</summary>
    [Alias("DateTo")]
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public DateTime? EndTime { get; set; }

    /// <summary>Named relative time range, such as LastHour or CurrentDay.</summary>
    [Parameter(ParameterSetName = "Path")]
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Provider")]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>
    /// Returns records from oldest to newest.
    /// </summary>
    [Parameter]
    public SwitchParameter Oldest { get; set; }

    /// <summary>
    /// Maximum number of records written. Zero writes every match.
    /// </summary>
    [Parameter]
    [ValidateRange(0, long.MaxValue)]
    public long MaxEvents { get; set; }

    /// <summary>
    /// Maximum time for remote RPC probing, worker admission, and session establishment.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    [ValidateRange(1, int.MaxValue)]
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Maximum time without remote read progress. Zero keeps the read unbounded.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    [ValidateRange(0, int.MaxValue)]
    public int RemoteReadTimeoutMilliseconds { get; set; }

    /// <summary>
    /// Maximum number of detached remote events buffered between the native reader and exporter.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [Parameter(ParameterSetName = "Xml")]
    [Parameter(ParameterSetName = "Provider")]
    [ValidateRange(1, 4096)]
    public int BufferCapacity { get; set; } = 64;

    /// <summary>
    /// Culture used for provider messages and display names, for example en-US.
    /// </summary>
    [Parameter]
    public CultureInfo? MessageCulture { get; set; } =
        CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Culture used when provider resources do not contain MessageCulture.
    /// </summary>
    [Parameter]
    public CultureInfo? FallbackMessageCulture { get; set; } =
        CultureInfo.CurrentUICulture;

    /// <summary>
    /// Replaces an existing destination only after the new export completes successfully.
    /// </summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Skips the final SHA-256 pass. Use this for maximum throughput when another system
    /// already provides integrity validation.
    /// </summary>
    [Parameter]
    public SwitchParameter SkipHash { get; set; }

    /// <summary>
    /// Embeds provider resources into a native EVTX export so messages can be rendered
    /// on computers where the original providers are not installed.
    /// </summary>
    [Parameter]
    public SwitchParameter ArchiveResources { get; set; }

    /// <summary>
    /// Allows a structured QueryList export to continue when one path cannot be evaluated.
    /// </summary>
    [Parameter(ParameterSetName = "Xml")]
    public SwitchParameter TolerateQueryErrors { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (Format != EventExportFormat.Evtx &&
            ArchiveResources) {
            throw new PSArgumentException(
                "ArchiveResources is only valid with Format Evtx.");
        }
        string source = ParameterSetName switch {
            "Channel" => string.IsNullOrWhiteSpace(MachineName)
                ? string.Join(", ", LogName)
                : $"{MachineName}\\{string.Join(", ", LogName)}",
            "Provider" => string.Join(", ", ProviderName),
            "Xml" => "QueryList XML",
            _ => string.Join(", ", Path)
        };
        if (!ShouldProcess(OutputPath, $"Export events from '{source}'")) {
            return;
        }

        EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(
            CreateQueryDefinition(),
            _cancellation.Token);
        EventExportResult result = Format == EventExportFormat.Evtx
            ? ExportNativeBatch(batch)
            : EventLogExporter.ExportBatch(
                batch,
                OutputPath,
                Format,
                Force.IsPresent,
                _cancellation.Token,
                computeSha256: !SkipHash.IsPresent);
        WriteStructuredFailures();
        WriteObject(result);
    }

    private EventQueryDefinition CreateQueryDefinition() {
        if (Filter != null &&
            new[] { nameof(EventId), nameof(Level), nameof(StartTime), nameof(EndTime), nameof(TimePeriod) }
                .Any(MyInvocation.BoundParameters.ContainsKey)) {
            throw new PSArgumentException(
                "Filter cannot be combined with EventId, Level, StartTime, EndTime, or TimePeriod.");
        }
        if (ParameterSetName == "Provider" &&
            (Filter?.ProviderNames?.Count ?? 0) > 0) {
            throw new PSArgumentException(
                "ProviderName already defines the provider source in the Provider parameter set; Filter.ProviderNames must be empty.");
        }
        (DateTime? start, DateTime? end) =
            EventTimeRange.Resolve(StartTime, EndTime, TimePeriod);
        var definition = new EventQueryDefinition {
            LogNames = ParameterSetName == "Channel" ? LogName : null,
            ProviderNames = ParameterSetName == "Provider" ? ProviderName : null,
            Paths = ParameterSetName == "Path" ? ResolvePaths() : null,
            QueryXml = ParameterSetName == "Xml" ? FilterXml : null,
            MachineNames = ParameterSetName == "Path"
                ? null
                : new[] { MachineName },
            Filter = ParameterSetName == "Xml"
                ? null
                : Filter ?? new EventFilter {
                    EventIds = EventId,
                    Levels = Level?.Select(static value => (byte)value).ToArray(),
                    StartTime = start,
                    EndTime = end
                },
            FilterXPath = FilterXPath,
            TolerateQueryErrors = TolerateQueryErrors.IsPresent,
            Options = new EventLogQueryOptions {
                Oldest = Oldest.IsPresent,
                ReadMode = ReadMode,
                MaxEvents = MaxEvents,
                Credential = Credential?.GetNetworkCredential(),
                Authentication = Authentication,
                MessageCulture = MessageCulture,
                FallbackMessageCulture = FallbackMessageCulture,
                RemoteConnectionTimeoutMilliseconds =
                    RemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    RemoteReadTimeoutMilliseconds,
                BufferCapacity = BufferCapacity,
                ContinueOnError = TolerateQueryErrors.IsPresent,
                FailureHandler = QueueQueryFailure
            }
        };
        return definition;
    }

    private EventExportResult ExportNativeBatch(
        EventLogBatchQuery batch) {

        if (batch.ChannelQueries.Count != 0 ||
            batch.FileQueries.Count != 0 ||
            batch.StructuredQueries.Count != 1) {
            throw new PSNotSupportedException(
                "Windows can write one native EVTX only from one native query session. " +
                "Narrow the source to one machine/query, export each source separately, " +
                "or choose Csv, JsonLines, or Xml.");
        }
        EventLogStructuredQuery query = batch.StructuredQueries[0];
        query.FailureHandler = QueueQueryFailure;
        return EventLogExporter.ExportStructured(
            query,
            OutputPath,
            Format,
            Force.IsPresent,
            _cancellation.Token,
            computeSha256: !SkipHash.IsPresent,
            archiveResources: ArchiveResources.IsPresent);
    }

    private void QueueQueryFailure(EventLogQueryFailure failure) {
        _structuredFailures.Enqueue(new ErrorRecord(
            failure.Exception,
            "EVXExportQuerySourceFailed",
            ErrorCategory.ReadError,
            string.IsNullOrWhiteSpace(failure.MachineName)
                ? failure.Source
                : $"{failure.Source} on {failure.MachineName}"));
    }

    private void WriteStructuredFailures() {
        while (_structuredFailures.TryDequeue(
                   out ErrorRecord? failure)) {
            WriteError(failure);
        }
    }

    private string[] ResolvePaths() {
        var paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string pattern in Path) {
            try {
                foreach (string path in SessionState.Path
                             .GetResolvedProviderPathFromPSPath(
                                 pattern,
                                 out ProviderInfo provider)) {
                    if (!string.Equals(
                            provider.Name,
                            "FileSystem",
                            StringComparison.OrdinalIgnoreCase)) {
                        throw new PSArgumentException(
                            $"Path '{pattern}' must use the FileSystem provider.");
                    }
                    paths.Add(System.IO.Path.GetFullPath(path));
                }
            } catch (ItemNotFoundException) {
                throw new PSArgumentException(
                    $"No event-log files match path '{pattern}'.");
            }
        }
        if (paths.Count == 0) {
            throw new PSArgumentException(
                "At least one event-log file is required.");
        }
        return paths
            .OrderBy(static path =>
                path,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    protected override void StopProcessing() {
        _cancellation.Cancel();
        base.StopProcessing();
    }

    /// <inheritdoc />
    protected override void EndProcessing() {
        _cancellation.Dispose();
        base.EndProcessing();
    }
}
