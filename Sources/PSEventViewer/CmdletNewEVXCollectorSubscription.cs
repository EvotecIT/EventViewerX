using System.Globalization;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Creates a typed collector- or source-initiated WEC subscription definition.</para>
/// <para type="description">Builds safe Windows Event Collector XML from typed reports, custom definitions, a QueryList, or common event filters. The command does not change the collector; pipe the definition to Set-EVXCollectorSubscription to apply it.</para>
/// </summary>
/// <example>
///   <summary>Create and apply a failed-logon collector subscription</summary>
///   <code>New-EVXCollectorSubscription -Name FailedLogons -SourceComputer DC1,DC2 -LogName Security -EventId 4625 | Set-EVXCollectorSubscription</code>
///   <para>Builds a typed definition and creates or updates the local collector subscription.</para>
/// </example>
/// <example>
///   <summary>Create a source-initiated domain-controller subscription</summary>
///   <code>New-EVXCollectorSubscription -Name GpoAudit -SubscriptionType SourceInitiated -CollectorHostName WEC01.contoso.com -Type GroupPolicyActivity -AllowedSourceSid $domainControllersSid | Set-EVXCollectorSubscription -InitializeCollector</code>
///   <para>Uses source policy for discovery. Domain controllers need the Domain Controllers SID or explicit computer SIDs in the source authorization SDDL.</para>
/// </example>
/// <example>
///   <summary>Write a reviewable WEC XML template</summary>
///   <code>New-EVXCollectorSubscription -Name SystemErrors -SourceComputer SRV01 -LogName System -Level Error -Enabled $false -OutputPath .\SystemErrors.xml</code>
///   <para>Writes inbox-compatible XML without changing the collector.</para>
/// </example>
[Cmdlet(VerbsCommon.New, "EVXCollectorSubscription", DefaultParameterSetName = "Filter")]
[OutputType(typeof(CollectorSubscriptionDefinition), typeof(FileInfo))]
public sealed class CmdletNewEVXCollectorSubscription : PSCmdlet {
    /// <summary>Unique WEC subscription name.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    /// <summary>Source computers collected by this subscription.</summary>
    [Parameter(Position = 1)]
    [Alias("ComputerName", "MachineName", "ServerName")]
    public string[] SourceComputer { get; set; } = Array.Empty<string>();

    /// <summary>CollectorInitiated for explicit sources, or SourceInitiated for policy-discovered sources.</summary>
    [Parameter]
    public CollectorSubscriptionType SubscriptionType { get; set; } =
        CollectorSubscriptionType.CollectorInitiated;

    /// <summary>Collector DNS name required for Push delivery and the source SubscriptionManager policy value.</summary>
    [Parameter]
    public string? CollectorHostName { get; set; }

    /// <summary>Source authorization SDDL used by a source-initiated subscription.</summary>
    [Parameter]
    public string AllowedSourceDomainComputersSddl { get; set; } =
        "O:NSG:NSD:(A;;GA;;;DC)(A;;GA;;;NS)";

    /// <summary>Explicit computer or group SIDs authorized for source-initiated forwarding. This is a simpler alternative to AllowedSourceDomainComputersSddl.</summary>
    [Parameter]
    public string[] AllowedSourceSid { get; set; } = Array.Empty<string>();

    /// <summary>Source policy refresh interval in seconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int SourceRefreshIntervalSeconds { get; set; } = 60;

    /// <summary>Event channel used by the generated query.</summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = "Filter")]
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = "TypedFilter")]
    public string LogName { get; set; } = string.Empty;

    /// <summary>Built-in leaf or composite event types. Their definitions own source channels and event IDs.</summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = "Type")]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <summary>Custom typed definition or JSON definition path.</summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = "Definition")]
    public object? Definition { get; set; }

    /// <summary>Reusable typed event filter.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "TypedFilter")]
    public EventFilter? Filter { get; set; }

    /// <summary>Event identifiers included in the generated query.</summary>
    [Parameter(ParameterSetName = "Filter")]
    [Alias("Id")]
    public int[]? EventId { get; set; }

    /// <summary>Provider names included in the generated query.</summary>
    [Parameter(ParameterSetName = "Filter")]
    public string[]? ProviderName { get; set; }

    /// <summary>Numeric Windows event levels included in the generated query.</summary>
    [Parameter(ParameterSetName = "Filter")]
    public EventViewerX.Level[]? Level { get; set; }

    /// <summary>Earliest event time included in the generated query.</summary>
    [Alias("DateFrom")]
    [Parameter(ParameterSetName = "Filter")]
    public DateTime? StartTime { get; set; }

    /// <summary>Latest event time included in the generated query.</summary>
    [Alias("DateTo")]
    [Parameter(ParameterSetName = "Filter")]
    public DateTime? EndTime { get; set; }

    /// <summary>Relative time range included in the generated query.</summary>
    [Parameter(ParameterSetName = "Filter")]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Complete Windows Event Log QueryList XML.</summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = "QueryXml")]
    public string? QueryXml { get; set; }

    /// <summary>Operator-facing description.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Whether the subscription starts enabled.</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>Whether already-recorded source events are collected.</summary>
    [Parameter]
    public SwitchParameter ReadExistingEvents { get; set; }

    /// <summary>Pull or push delivery.</summary>
    [Parameter]
    public CollectorSubscriptionDeliveryMode DeliveryMode { get; set; } =
        CollectorSubscriptionDeliveryMode.Pull;

    /// <summary>Maximum items delivered in one batch.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = 1;

    /// <summary>Maximum delivery latency in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxLatencyMilliseconds { get; set; } = 1000;

    /// <summary>Heartbeat or polling interval in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int HeartbeatIntervalMilliseconds { get; set; } = 40000;

    /// <summary>HTTP or HTTPS transport.</summary>
    [Parameter]
    [ValidateSet("HTTP", "HTTPS", IgnoreCase = true)]
    public string TransportName { get; set; } = "HTTP";

    /// <summary>Explicit transport port. Zero uses the Windows default.</summary>
    [Parameter]
    [ValidateRange(0, ushort.MaxValue)]
    public int TransportPort { get; set; }

    /// <summary>Raw Events or RenderedText delivery.</summary>
    [Parameter]
    public CollectorSubscriptionContentFormat ContentFormat { get; set; } =
        CollectorSubscriptionContentFormat.Events;

    /// <summary>Culture used for rendered text.</summary>
    [Parameter]
    public CultureInfo Locale { get; set; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Collector destination channel.</summary>
    [Parameter]
    public string DestinationLog { get; set; } = "ForwardedEvents";

    /// <summary>Publisher that owns or imports the destination channel.</summary>
    [Parameter]
    public string PublisherName { get; set; } = "Microsoft-Windows-EventCollector";

    /// <summary>Optional path that receives the generated XML.</summary>
    [Parameter]
    public string? OutputPath { get; set; }

    /// <summary>Overwrites OutputPath when it already exists.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Also emits the typed definition when OutputPath is used.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string[] sources = SourceComputer
            .Select(static source => source?.Trim() ?? string.Empty)
            .Where(static source => source.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (SubscriptionType == CollectorSubscriptionType.CollectorInitiated && sources.Length == 0) {
            throw new PSArgumentException("SourceComputer requires at least one non-empty computer name for a collector-initiated subscription.");
        }
        if (SubscriptionType == CollectorSubscriptionType.SourceInitiated && sources.Length > 0) {
            throw new PSArgumentException("SourceComputer cannot be used with a source-initiated subscription; authorize sources with AllowedSourceDomainComputersSddl and configure their SubscriptionManager policy.");
        }

        string queryXml;
        if (ParameterSetName == "QueryXml") {
            queryXml = QueryXml!;
        } else if (ParameterSetName == "Type") {
            queryXml = EventDefinitionCompiler.BuildQueryXml(Type);
        } else if (ParameterSetName == "Definition") {
            EventDefinition customDefinition = Definition switch {
                EventDefinition typed => typed,
                string path => EventDefinition.Load(path),
                _ => throw new PSArgumentException("Definition must be an EventDefinition instance or a JSON file path.", nameof(Definition))
            };
            queryXml = EventDefinitionCompiler.BuildQueryXml(customDefinition);
        } else {
            EventFilter filter;
            if (ParameterSetName == "TypedFilter") {
                filter = Filter!;
            } else {
                (DateTime? startTime, DateTime? endTime) = EventTimeRange.Resolve(
                    StartTime,
                    EndTime,
                    TimePeriod);
                filter = new EventFilter {
                    EventIds = EventId,
                    ProviderNames = ProviderName,
                    Levels = Level?.Select(static value => (byte)value).ToArray(),
                    StartTime = startTime,
                    EndTime = endTime
                };
            }
            queryXml = EventFilterCompiler.BuildChannelUnionQueryXml(
                new[] { LogName },
                EventFilterPartitioner.Partition(filter));
        }

        CollectorSubscriptionDeliveryMode deliveryMode = SubscriptionType == CollectorSubscriptionType.SourceInitiated &&
                                                         !MyInvocation.BoundParameters.ContainsKey(nameof(DeliveryMode))
            ? CollectorSubscriptionDeliveryMode.Push
            : DeliveryMode;
        if (AllowedSourceSid.Length > 0 &&
            MyInvocation.BoundParameters.ContainsKey(nameof(AllowedSourceDomainComputersSddl))) {
            throw new PSArgumentException("AllowedSourceSid and AllowedSourceDomainComputersSddl cannot be used together.");
        }
        string allowedSourceSddl = AllowedSourceSid.Length > 0
            ? CollectorSourcePolicy.BuildAllowedSourceSddl(AllowedSourceSid)
            : AllowedSourceDomainComputersSddl;
        var definition = new CollectorSubscriptionDefinition {
            SubscriptionId = Name,
            Description = Description,
            Enabled = Enabled,
            SubscriptionType = SubscriptionType,
            QueryXml = queryXml,
            Sources = sources.Select(static source => new CollectorSubscriptionSource(source)).ToArray(),
            CollectorHostName = CollectorHostName,
            AllowedSourceDomainComputersSddl = allowedSourceSddl,
            SourceRefreshIntervalSeconds = SourceRefreshIntervalSeconds,
            ReadExistingEvents = ReadExistingEvents.IsPresent,
            DeliveryMode = deliveryMode,
            MaxItems = MaxItems,
            MaxLatencyMilliseconds = MaxLatencyMilliseconds,
            HeartbeatIntervalMilliseconds = HeartbeatIntervalMilliseconds,
            TransportName = TransportName,
            TransportPort = TransportPort,
            ContentFormat = ContentFormat,
            Locale = Locale,
            DestinationLog = DestinationLog,
            PublisherName = PublisherName
        };
        definition.Validate();

        if (string.IsNullOrWhiteSpace(OutputPath)) {
            WriteObject(definition);
            return;
        }
        FileInfo file = CollectorSubscriptionManager.WriteCollectorSubscriptionDefinition(
            definition,
            SessionState.Path.GetUnresolvedProviderPathFromPSPath(OutputPath),
            Force.IsPresent);
        WriteObject(file);
        if (PassThru.IsPresent) {
            WriteObject(definition);
        }
    }
}
