using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Enhanced event querying cmdlet that replaces and extends Get-WinEvent functionality.</para>
/// <para type="description">Supports local and remote logs, built-in event types, custom JSON definitions, record ID resumes, parallel queries, and rich filtering.</para>
/// </summary>
/// <example>
///   <summary>Query successful logons from Security</summary>
///   <code>Get-EVXEvent -LogName Security -EventId 4624 -StartTime (Get-Date).AddHours(-1)</code>
///   <para>Shows only successful logons from the last hour.</para>
/// </example>
/// <example>
///   <summary>Read events from an EVTX file</summary>
///   <code>Get-EVXEvent -Path C:\Logs\App.evtx -EventId 1000,1001</code>
///   <para>Filters specific application error IDs from an offline log.</para>
/// </example>
/// <example>
///   <summary>Resume from last record ID</summary>
///   <code>Get-EVXEvent -LogName Security -RecordIdFile C:\temp\resume.json -RecordIdKey Sec</code>
///   <para>Continues from the last processed record and updates the checkpoint file.</para>
/// </example>
/// <example>
///   <summary>Use a built-in event type</summary>
///   <code>Get-EVXEvent -Type ADUserLogonFailed -StartTime (Get-Date).AddDays(-1)</code>
///   <para>The event type owns its source channel, event IDs, filters, and typed projection.</para>
/// </example>
/// <example>
///   <summary>Reuse a discoverable typed filter</summary>
///   <code>$filter = New-EVXFilter -Type ADUserLogonFailed; $filter.AllOf($filter.Fields.Who.MatchesWildcard('EVOTEC\*'), $filter.Fields.IPAddress.MatchesSubnet('10.0.0.0/8')); Get-EVXEvent -Filter $filter -TimePeriod Last7Days</code>
///   <para>The filter retains its type and exact predicate, so the query does not repeat either one.</para>
/// </example>
/// <example>
///   <summary>Describe one typed event contract</summary>
///   <code>Get-EVXEvent -Type ADUserLogonFailed -Describe</code>
///   <para>Returns the source, field, alias, type, and filter-stage metadata without reading events.</para>
/// </example>
/// <example>
///   <summary>Use a custom JSON definition against an offline file</summary>
///   <code>Get-EVXEvent -Definition .\ServiceChanges.json -Path .\System.evtx</code>
///   <para>Applies the definition-owned sources and fields while the path supplies the event container.</para>
/// </example>
/// <example>
///   <summary>Parallel query across servers</summary>
///   <code>Get-EVXEvent -LogName Security -MachineName DC1,DC2 -EventId 4740 -MaxConcurrency 8</code>
///   <para>Retrieves account lockouts from multiple domain controllers with bounded concurrent source setup.</para>
/// </example>
/// <example>
///   <summary>Stream core metadata from a large EVTX file</summary>
///   <code>Get-EVXEvent -Path C:\Logs\Security.evtx -Oldest -ReadMode Metadata | Select-Object TimeCreated, RecordId, Id, ProviderName, MachineName | Export-Csv C:\Logs\Security-metadata.csv -NoTypeInformation</code>
///   <para>Skips provider message formatting, XML parsing, attachments, and bookmarks while streaming every record.</para>
/// </example>
[OutputType(typeof(EventObject), ParameterSetName = new string[] { "Channel" })]
[OutputType(typeof(EventObject), ParameterSetName = new string[] { "Path" })]
[OutputType(typeof(EventObject), ParameterSetName = new string[] { "Hashtable" })]
[OutputType(typeof(EventObject), ParameterSetName = new string[] { "Xml" })]
[OutputType(typeof(EventObject), ParameterSetName = new string[] { "Provider" })]
[OutputType(typeof(EventTypeRecord), ParameterSetName = new string[] { "Type" })]
[OutputType(typeof(GroupPolicyAuditRecord), ParameterSetName = new string[] { "Type" })]
[OutputType(typeof(EventTypeRecord), ParameterSetName = new string[] { "Preset" })]
[OutputType(typeof(CustomEventRecord), ParameterSetName = new string[] { "Definition" })]
[OutputType(typeof(EventTypeRecord), ParameterSetName = new string[] { "TypedFilter" })]
[OutputType(typeof(CustomEventRecord), ParameterSetName = new string[] { "TypedFilter" })]
[Cmdlet(VerbsCommon.Get, "EVXEvent", DefaultParameterSetName = "TypedFilter")]
[Alias("Find-WinEvent")]
public sealed partial class CmdletGetEVXEvent : AsyncPSCmdlet {
    private string _recordIdKey = string.Empty;
    private Dictionary<string, long> _recordMap = new();
    private readonly Dictionary<string, Guid> _checkpointGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _checkpointBoundaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _checkpointBoundaryMigrations = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CheckpointSource>? _checkpointSources;
    private readonly Dictionary<string, long> _highestRecordIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventObject> _highestCheckpointEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _resetCheckpointKeys = new(StringComparer.OrdinalIgnoreCase);
    private WildcardPattern[] _managedProviderPatterns =
        Array.Empty<WildcardPattern>();
    private long _eventsOutput;
    private EventDefinition? _resolvedDefinition;
    private PowerShellEventPredicateBuilder? _typedFilter;
    /// <summary>
    /// Name of the log to query.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "Channel")]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Path to an event log file for offline analysis.
    /// </summary>
    [Alias("PSPath")]
    [Parameter(
        Mandatory = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Event identifiers used to filter results.
    /// </summary>
    [Alias("Id")]
    [Parameter(Mandatory = false, Position = 1, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, Position = 1, ParameterSetName = "Provider")]
    public int[]? EventId { get; set; }

    /// <summary>
    /// Specific event record identifiers to retrieve.
    /// </summary>
    [Alias("RecordId")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public long[]? EventRecordId { get; set; }

    /// <summary>
    /// Path to a file storing last processed record ID.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public string? RecordIdFile { get; set; }

    /// <summary>
    /// Identifier used when persisting record IDs to allow multiple jobs to share a file.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public string? RecordIdKey { get; set; }

    /// <summary>
    /// Computer names against which to run the query.
    /// </summary>
    [Alias("ComputerName", "ServerName")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public List<string?>? MachineName { get; set; }

    /// <summary>
    /// Windows Event Collector computers from which typed events are read through ForwardedEvents.
    /// The selected Type still owns each event's original source channel and identifiers.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    public List<string?>? Collector { get; set; }

    /// <summary>
    /// Event provider name to filter results.
    /// </summary>
    [Alias("Source", "Provider")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = true, ParameterSetName = "Provider")]
    public string[]? ProviderName { get; set; }

    /// <summary>
    /// Keywords used to filter events.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public long[]? Keywords { get; set; }

    /// <summary>
    /// Event level (e.g. Error, Warning) used for filtering.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public EventViewerX.Level[]? Level { get; set; }

    /// <summary>
    /// Start time for the event query.
    /// </summary>
    [Alias("DateFrom")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time for the event query.
    /// </summary>
    [Alias("DateTo")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Relative time period for filtering events.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>
    /// User identifier used to filter events.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public string[]? UserId { get; set; }

    /// <summary>
    /// Filters events by matching their formatted message against the provided regular expression.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public Regex? MessageRegex { get; set; }

    /// <summary>
    /// Maximum number of independent event sources opened concurrently.
    /// </summary>
    [Alias("NumberOfThreads")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [ValidateRange(1, EventLogLimits.MaximumConcurrency)]
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>
    /// Maximum number of events to return.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [ValidateRange(0, long.MaxValue)]
    public long MaxEvents { get; set; }

    /// <summary>
    /// Maximum number of merged candidate events delivered for message and checkpoint filtering.
    /// Zero continues until the output limit is satisfied or the query is exhausted. Native selection may perform
    /// one initial lookahead per machine/XPath chunk plus bounded page prefetch; those rows are not evaluated by the cmdlet.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [ValidateRange(0, long.MaxValue)]
    public long MaxEventsScanned { get; set; }

    /// <summary>
    /// Resolves reverse-DNS names for supported typed events after projection. DNS failures remain visible on the
    /// event and never remove the event from the pipeline.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    public SwitchParameter ResolveDns { get; set; }

    /// <summary>
    /// Whole-request timeout in milliseconds for each optional reverse-DNS request, including dependency retries.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [ValidateRange(1, 60000)]
    public int DnsTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Maximum number of reverse-DNS requests that may overlap. Results and checkpoints remain in event order.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [ValidateRange(1, 64)]
    public int DnsMaxConcurrency { get; set; } = 8;

    /// <summary>
    /// Controls per-event materialization. Metadata skips provider messages, XML, attachments, and bookmarks;
    /// Message formats the provider message; StructuredData parses XML without formatting the message;
    /// StructuredDataAndMessage includes both without decoding attachments; Full includes all data.
    /// Typed queries default to StructuredDataAndMessage; other query sets default to Message.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public EventReadMode ReadMode { get; set; } =
        EventReadMode.Message;

    /// <summary>
    /// Culture used to format provider messages and display names for offline EVTX queries.
    /// For example, use <c>en-US</c> for deterministic English output.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public CultureInfo? MessageCulture { get; set; } =
        CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Overrides both remote connection and no-progress read timeouts in milliseconds.
    /// Zero uses Settings.SessionTimeoutMs for connection establishment and
    /// Settings.QuerySessionTimeoutMs for reading.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [ValidateRange(0, int.MaxValue)]
    public int SessionTimeoutMs { get; set; }

    private int EffectiveRemoteConnectionTimeoutMilliseconds =>
        SessionTimeoutMs > 0
            ? SessionTimeoutMs
            : Settings.SessionTimeoutMs;

    private int EffectiveRemoteReadTimeoutMilliseconds =>
        SessionTimeoutMs > 0
            ? SessionTimeoutMs
            : Settings.QuerySessionTimeoutMs;

    /// <summary>
    /// Maximum number of projected events buffered between parallel readers and the PowerShell pipeline. Zero selects a bounded default.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [ValidateRange(0, int.MaxValue)]
    public int BufferCapacity { get; set; }

    /// <summary>
    /// Expands event data into individual properties.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    [Alias("Expand")]
    public SwitchParameter ExpandData { get; set; }

    /// <summary>
    /// Reads events from oldest to newest when querying files.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter Oldest { get; set; }

    /// <summary>
    /// Hashtable filter for named EventData fields when querying files.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public Hashtable? NamedDataFilter { get; set; }

    /// <summary>
    /// Hashtable filter to exclude named EventData fields when querying files.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public Hashtable? NamedDataExcludeFilter { get; set; }

    /// <summary>
    /// Disables parallel processing of queries.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Channel")]
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    [Parameter(Mandatory = false, ParameterSetName = "Preset")]
    [Parameter(Mandatory = false, ParameterSetName = "Definition")]
    [Parameter(Mandatory = false, ParameterSetName = "TypedFilter")]
    [Parameter(Mandatory = false, ParameterSetName = "Path")]
    [Parameter(Mandatory = false, ParameterSetName = "Hashtable")]
    [Parameter(Mandatory = false, ParameterSetName = "Xml")]
    [Parameter(Mandatory = false, ParameterSetName = "Provider")]
    public SwitchParameter DisableParallel { get; set; }

    /// <summary>
    /// One or more built-in typed event definitions to query. Each type owns its source channels and event identifiers.
    /// </summary>
    [Alias("NamedEvent", "NamedEvents")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Type")]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <summary>Built-in monitoring preset combining canonical event types with exact semantic filtering.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Preset")]
    public EventMonitoringPreset? Preset { get; set; }

    /// <summary>
    /// SQLite file that preserves Group Policy names and identity history across runs.
    /// This requires exactly <c>-Type GroupPolicyDirectoryAudit</c> and does not scan Active Directory or SYSVOL.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    public string? ContextStorePath { get; set; }

    /// <summary>Caller-authorized partition used to resolve non-shareable imported Group Policy context. Requires <c>ContextStorePath</c>.</summary>
    [Parameter(Mandatory = false, ParameterSetName = "Type")]
    public string? ContextAuthorization { get; set; }

    /// <summary>Custom EventViewerX definition instance or JSON file path.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Definition")]
    public object? Definition { get; set; }

    /// <summary>Reusable typed EventPredicate, predicate JSON, or predicate JSON file.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Preset")]
    [Parameter(ParameterSetName = "Definition")]
    public object? Where { get; set; }

    /// <summary>Returns the native/managed predicate plan without querying event sources.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Preset")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "TypedFilter")]
    [Parameter(ParameterSetName = "Path")]
    public SwitchParameter Explain { get; set; }

    /// <summary>Returns definition and field metadata without querying event sources.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Preset")]
    [Parameter(ParameterSetName = "Definition")]
    [Parameter(ParameterSetName = "TypedFilter")]
    [Parameter(ParameterSetName = "Path")]
    public SwitchParameter Describe { get; set; }

    /// <summary>
    /// Initializes logging and helper classes before processing.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        if (OutputLimitReached) {
            return;
        }
        _checkpointSources = null;
        _managedProviderPatterns =
            Array.Empty<WildcardPattern>();
        _offlineProvidersByPath.Clear();
        ValidateContextAuthorizationSelection();
        InitializeTypedFilter();
        EventPredicate? presetPredicate = ResolvePreset();
        if (Describe.IsPresent) {
            if (UsesCustomDefinitionQuery) {
                WriteObject(ResolveEventDefinition());
            } else {
                foreach (EventType type in Type) {
                    WriteObject(EventTypeCatalog.GetDefinition(type));
                }
            }
            return;
        }
        if (!string.IsNullOrWhiteSpace(ContextStorePath)) {
            await ProcessGroupPolicyContextAsync();
            return;
        }
        if ((UsesBuiltInTypeQuery || UsesCustomDefinitionQuery) &&
            !MyInvocation.BoundParameters.ContainsKey(
                nameof(ReadMode))) {
            ReadMode = EventReadMode.StructuredDataAndMessage;
        }
        EventPredicateBuilder? predicateBuilder = UsesBuiltInTypeQuery
            ? EventPredicateBuilder.ForTypes(Type)
            : UsesCustomDefinitionQuery
                ? EventPredicateBuilder.ForDefinition(ResolveEventDefinition())
                : null;
        EventPredicate? predicate = PowerShellEventPredicateAdapter.Resolve(
            Where,
            nameof(Where),
            predicateBuilder);
        if (presetPredicate != null) {
            predicate = predicate == null
                ? presetPredicate
                : EventPredicate.AllOf(presetPredicate, predicate);
        }
        if (predicate != null && predicateBuilder != null) {
            predicate = predicateBuilder.Normalize(predicate);
        }
        if (Explain.IsPresent) {
            if (predicate == null) {
                throw new PSArgumentException("Explain requires Where so there is a typed predicate to plan.");
            }
            EventPredicatePlan plan = UsesCustomDefinitionQuery
                ? EventDefinitionEngine.PlanPredicate(
                    ResolveEventDefinition(),
                    predicate,
                    Collector == null ? null : "ForwardedEvents")
                : Collector == null
                    ? EventPredicatePlanner.Plan(predicate)
                    : EventPredicatePlanner.PlanManagedOnly(
                        predicate,
                        "ForwardedEvents uses the Windows Server 2025 safe '*' reader, so typed filtering is bounded and managed.");
            WriteObject(plan);
            return;
        }
        ValidateRecordOptions();
        InitializeCheckpointKey(predicate);

        CancellationToken token;
#if NET8_0_OR_GREATER
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(StoppingToken, CancelToken);
        token = linkedCts.Token;
#else
        token = CancelToken;
#endif
        List<object>? results = null;

        PrepareRecordProcessing(token);
        if (UsesCustomDefinitionQuery) {
            await ProcessDefinitionAsync(token, predicate);
        } else if (UsesBuiltInTypeQuery) {
                // let's find the events prepared for search
                List<EventType> typeList = Type.ToList();
                int typeThreads = DisableParallel.IsPresent
                    ? 1
                    : MaxConcurrency;
                var typeQueryInfo = new EventTypeQueryExecutionInfo();
                Func<EventTypeRecord, bool>? typeResultPredicate = MessageRegex == null
                    ? null
                    : eventObject => MessageMatches(eventObject.SourceEvent);
                EventEnrichmentOptions? enrichmentOptions = ResolveDns
                    ? new EventEnrichmentOptions {
                        ResolveDns = true,
                        DnsTimeoutMilliseconds = DnsTimeoutMs,
                        DnsMaxConcurrency = DnsMaxConcurrency,
                        RetryDnsOnTransient = false
                    }
                    : null;
                var typeQuery =
                    new EventTypeQuery(typeList) {
                        Paths = Path.Length == 0
                            ? null
                            : Path,
                        MachineNames = Collector ?? MachineName,
                        CollectorLogName = Collector == null
                            ? null
                            : "ForwardedEvents",
                        StartTime = StartTime,
                        EndTime = EndTime,
                        TimePeriod = TimePeriod,
                        SourceLogName = null,
                        SourceEventIds = null,
                        SourceRecordIds = EventRecordId,
                        MaxConcurrency =
                            typeThreads,
                        MaxEvents = MaxEvents,
                        MaxCandidates =
                            MaxEventsScanned,
                        MinimumRecordIdExclusiveResolver =
                            GetCheckpointLowerBound,
                        CandidateObserver =
                            candidate =>
                                TrackCheckpointProgress(
                                    candidate),
                        Oldest = EffectiveOldest,
                        ReadMode =
                            ReadMode,
                        ResultPredicate =
                            typeResultPredicate,
                        Predicate = predicate,
                        Enrichment =
                            enrichmentOptions,
                        MessageCulture =
                            MessageCulture,
                        FallbackMessageCulture =
                            FallbackMessageCulture,
                        Credential =
                            Credential?.GetNetworkCredential(),
                        Authentication =
                            Authentication,
                        RemoteConnectionTimeoutMilliseconds =
                            EffectiveRemoteConnectionTimeoutMilliseconds,
                        RemoteReadTimeoutMilliseconds =
                            EffectiveRemoteReadTimeoutMilliseconds,
                        BufferCapacity =
                            BufferCapacity > 0
                                ? BufferCapacity
                                : 64,
                        ContinueOnRemoteFailure =
                            ContinueOnError.IsPresent ||
                            (MachineName?.Count ?? 0) > 1,
                        IncludeBookmark =
                            IncludeBookmark.IsPresent
                    };
                await foreach (EventTypeRecord eventObject in
                               EventTypeEngine.ReadAsync(
                                   typeQuery,
                                   typeQueryInfo,
                                   token)) {
                    token.ThrowIfCancellationRequested();
                    if (!TrackCheckpointProgress(eventObject.SourceEvent)) {
                        continue;
                    }
                    object output = ExpandData
                        ? GetExpandedObject(eventObject, eventObject.SourceEvent)
                        : eventObject;
                    WriteObject(output);
                    _eventsOutput++;
                    if (OutputLimitReached) {
                        break;
                    }
                }
                WriteNamedTargetFailures(
                    typeQueryInfo.TargetFailures);
        } else {
            ProcessNativeEvents(token, results);
        }

    }

    private bool UsesCustomDefinitionQuery =>
        ParameterSetName == "Definition" ||
        _typedFilter?.Definition != null;

    private void InitializeTypedFilter() {
        if (Filter == null ||
            ParameterSetName != "TypedFilter" && ParameterSetName != "Path") {
            return;
        }
        object? value = Filter;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        _typedFilter = value as PowerShellEventPredicateBuilder;
        if (_typedFilter == null) {
            if (ParameterSetName == "TypedFilter") {
                throw new PSArgumentException(
                    "Typed Filter queries require the object returned by New-EVXFilter -Type or -Definition.",
                    nameof(Filter));
            }
            return;
        }
        if (ParameterSetName == "Path") {
            ValidateTypedFilterPathOptions();
        }
        if (_typedFilter.Type.HasValue) {
            Type = new[] { _typedFilter.Type.Value };
        } else if (_typedFilter.Definition != null) {
            _resolvedDefinition = _typedFilter.Definition;
        } else {
            throw new PSArgumentException(
                "The typed filter does not retain a Type or Definition query source.",
                nameof(Filter));
        }
        Where = _typedFilter.Predicate;
    }

    private void ValidateTypedFilterPathOptions() {
        string[] unsupported = new[] {
            nameof(EventId),
            nameof(ProviderName),
            nameof(Keywords),
            nameof(Level),
            nameof(UserId),
            nameof(NamedDataFilter),
            nameof(NamedDataExcludeFilter),
            nameof(FilterXPath),
            nameof(BookmarkXml),
            nameof(BookmarkOffset),
            nameof(IgnoreStaleBookmark)
        }.Where(MyInvocation.BoundParameters.ContainsKey).ToArray();
        if (unsupported.Length > 0) {
            throw new PSArgumentException(
                "A typed Filter with Path cannot be combined with native-only options: " +
                string.Join(", ", unsupported.Select(static name => "-" + name)) + ". " +
                "Express event fields through the typed filter instead.",
                nameof(Filter));
        }
    }

    private async Task ProcessDefinitionAsync(CancellationToken token, EventPredicate? predicate) {
        EventDefinition definition = ResolveEventDefinition();
        if (Collector != null && MachineName != null) {
            throw new PSArgumentException(
                "-Collector and -MachineName cannot be used together. Use -Collector for ForwardedEvents or -MachineName for direct source queries.");
        }
        var query = new EventDefinitionQuery(definition) {
            Paths = Path.Length == 0 ? null : Path,
            MachineNames = Collector ?? MachineName,
            CollectorLogName = Collector == null ? null : "ForwardedEvents",
            StartTime = StartTime,
            EndTime = EndTime,
            TimePeriod = TimePeriod,
            RecordIds = EventRecordId,
            MaxEvents = MaxEvents,
            MaxCandidates = MaxEventsScanned,
            MaxConcurrency = DisableParallel.IsPresent ? 1 : MaxConcurrency,
            Oldest = EffectiveOldest,
            ReadMode = ReadMode,
            IncludeBookmark = IncludeBookmark.IsPresent,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            RemoteConnectionTimeoutMilliseconds = EffectiveRemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = EffectiveRemoteReadTimeoutMilliseconds,
            BufferCapacity = BufferCapacity > 0 ? BufferCapacity : 64,
            MessageCulture = MessageCulture,
            FallbackMessageCulture = FallbackMessageCulture,
            Predicate = predicate,
            ResultPredicate = MessageRegex == null ? null : record => MessageMatches(record.SourceEvent),
            MinimumRecordIdExclusiveResolver = GetCheckpointLowerBound,
            CandidateObserver = candidate => TrackCheckpointProgress(candidate),
            ContinueOnRemoteFailure = ContinueOnError.IsPresent || (MachineName?.Count ?? 0) > 1
        };
        var info = new EventDefinitionQueryExecutionInfo();
        await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info, token)) {
            token.ThrowIfCancellationRequested();
            if (!TrackCheckpointProgress(record.SourceEvent)) {
                continue;
            }
            PSObject output = new(record);
            foreach (KeyValuePair<string, object?> value in record.Values.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                if (output.Properties[value.Key] == null) {
                    output.Properties.Add(new PSNoteProperty(value.Key, value.Value));
                }
            }
            if (ExpandData.IsPresent) {
                foreach (KeyValuePair<string, string> value in record.SourceEvent.Data.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                    if (output.Properties[value.Key] == null) {
                        output.Properties.Add(new PSNoteProperty(value.Key, value.Value));
                    }
                }
            }
            WriteObject(output);
            _eventsOutput++;
            if (OutputLimitReached) {
                break;
            }
        }
        WriteNamedTargetFailures(info.TargetFailures);
    }

    private EventDefinition ResolveEventDefinition() {
        if (_resolvedDefinition != null) {
            return _resolvedDefinition;
        }
        object? value = Definition;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        _resolvedDefinition = value switch {
            EventDefinition typed => typed,
            string path => EventDefinition.Load(path),
            _ => throw new PSArgumentException(
                "Definition must be an EventDefinition instance or a JSON file path.",
                nameof(Definition))
        };
        _resolvedDefinition.Validate();
        return _resolvedDefinition;
    }

    private void WriteNamedTargetFailures(
        IReadOnlyList<EventLogQueryTargetFailure> failures) {

        foreach (EventLogQueryTargetFailure failure in failures) {
            ErrorCategory category = failure.Kind switch {
                EventLogRemoteQueryFailureKind.AccessDenied =>
                    ErrorCategory.PermissionDenied,
                EventLogRemoteQueryFailureKind.Timeout =>
                    ErrorCategory.OperationTimeout,
                EventLogRemoteQueryFailureKind.HostUnavailable =>
                    ErrorCategory.ResourceUnavailable,
                EventLogRemoteQueryFailureKind.LogNotFound =>
                    ErrorCategory.ObjectNotFound,
                _ => ErrorCategory.ReadError
            };
            string message =
                string.IsNullOrWhiteSpace(
                    failure.Message)
                    ? $"Failed to read '{failure.LogName}' on '{failure.MachineName}'."
                    : failure.Message;
            WriteError(
                new ErrorRecord(
                    new InvalidOperationException(
                        message),
                    "EVXEventTypeTargetFailed",
                    category,
                    $"{failure.LogName} on {failure.MachineName}"));
        }
    }

    private bool HasManagedPostReadFilter =>
        MessageRegex != null ||
        _managedProviderPatterns.Length > 0 ||
        UsesCheckpoint;

    private bool UsesManagedOutputSelection =>
        (MessageRegex != null ||
         _managedProviderPatterns.Length > 0) &&
        !UsesCheckpoint &&
        MaxEvents > 0 &&
        MaxEventsScanned <= 0;

    private void ProcessEventResult(EventObject eventObject, List<object>? results) {
        if (!TrackCheckpointProgress(eventObject) ||
            !ProviderMatches(eventObject) ||
            !MessageMatches(eventObject)) {
            return;
        }

        object output = ExpandData ? GetExpandedObject(eventObject) : eventObject;
        WriteObject(output);
        _eventsOutput++;
    }

    /// <summary>
    /// Creates an expanded PSObject from EventObject with properties expanded from the Data property.
    /// </summary>
    /// <param name="eventObject">The event object.</param>
    /// <returns>PSObject with expanded properties.</returns>
    private PSObject GetExpandedObject(EventObject eventObject) {
        return GetExpandedObject(eventObject, eventObject);
    }

    /// <summary>
    /// Creates an expanded PSObject around the selected output projection using structured data from its source event.
    /// </summary>
    /// <param name="output">Object that remains the PowerShell base object.</param>
    /// <param name="eventObject">Source event whose structured data is expanded.</param>
    /// <returns>PSObject with non-conflicting structured data properties.</returns>
    private static PSObject GetExpandedObject(object output, EventObject eventObject) {
        PSObject outputObj = new(output);
        foreach (var property in eventObject.Data.OrderBy(static d => d.Key, StringComparer.OrdinalIgnoreCase)) {
            if (outputObj.Properties[property.Key] == null) {
                outputObj.Properties.Add(new PSNoteProperty(property.Key, property.Value));
            }
        }
        return outputObj;
    }

    /// <summary>
    /// Checks whether the event object's formatted message matches the provided regex filter.
    /// </summary>
    /// <param name="eventObject">The event to test.</param>
    /// <returns>True when no regex is defined or when the message matches the expression.</returns>
    private bool MessageMatches(EventObject eventObject) {
        if (MessageRegex == null) {
            return true;
        }

        var message = eventObject?.Message ?? string.Empty;
        return MessageRegex.IsMatch(message);
    }

    private bool ProviderMatches(EventObject eventObject) {
        return _managedProviderPatterns.Length == 0 ||
               _managedProviderPatterns.Any(pattern =>
                   pattern.IsMatch(
                       eventObject.ProviderName ??
                       string.Empty));
    }

}
