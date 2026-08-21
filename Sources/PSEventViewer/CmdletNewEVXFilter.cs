namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Creates a reusable typed Windows Event Log filter or compiles it to native query text.</para>
/// <para type="description">The default output is EventViewerX.EventFilter for native event metadata. Supply Type or Definition to discover typed domain fields and build a reusable EventPredicate. Use AsXPath, LogName, or Path when native query text is required by Get-WinEvent, Event Viewer, or WEC.</para>
/// </summary>
/// <example>
///   <summary>Create a reusable typed failed-logon filter</summary>
///   <code>$filter = New-EVXFilter -EventId 4625 -TimePeriod LastDay</code>
///   <para>Returns an EventFilter rather than opaque query text.</para>
/// </example>
/// <example>
///   <summary>Create XPath for Get-WinEvent</summary>
///   <code>Get-WinEvent -LogName Security -FilterXPath (New-EVXFilter -EventId 4625 -AsXPath)</code>
///   <para>Compiles the same typed filter to native XPath.</para>
/// </example>
/// <example>
///   <summary>Create QueryList XML for a custom view or WEC</summary>
///   <code>New-EVXFilter -LogName Security -EventId 4625 -NamedDataExcludeFilter @{ TargetUserName = 'svc_legacy' }</code>
///   <para>Returns QueryList XML with native Select and Suppress clauses.</para>
/// </example>
/// <example>
///   <summary>Build and reuse a discoverable typed filter</summary>
///   <code>$filter = New-EVXFilter -Type ADUserLogonFailed; $filter.AllOf($filter.Fields.Who.In('EVOTEC\Alice', 'EVOTEC\Bob'), $filter.Fields.IPAddress.MatchesSubnet('10.0.0.0/8')); Get-EVXEvent -Filter $filter -TimePeriod Last7Days</code>
///   <para>The builder retains both the typed definition and selected predicate, so Filter is sufficient to execute the query.</para>
/// </example>
/// <example>
///   <summary>Explain an inline typed predicate</summary>
///   <code>New-EVXFilter -Type ADUserLogonFailed -Where { $_.Who -like 'EVOTEC\*' } -Explain</code>
///   <para>Returns native and managed predicate stages without reading events.</para>
/// </example>
[Cmdlet(VerbsCommon.New, "EVXFilter", DefaultParameterSetName = "Object")]
[Alias("Get-EVXFilter")]
[OutputType(typeof(EventFilter), ParameterSetName = new[] { "Object" })]
[OutputType(typeof(string), ParameterSetName = new[] { "XPath", "ChannelXml", "FileXml" })]
[OutputType(typeof(PowerShellEventPredicateBuilder), ParameterSetName = new[] { "Type", "Definition" })]
public sealed class CmdletNewEVXFilter : PSCmdlet {
    /// <summary>Built-in event type whose typed fields should be exposed for predicate construction.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Type")]
    public EventType? Type { get; set; }

    /// <summary>Custom EventDefinition instance or JSON file whose typed fields should be exposed.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Definition")]
    public object? Definition { get; set; }

    /// <summary>Optional restricted typed predicate expression stored in the returned reusable filter.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    public object? Where { get; set; }

    /// <summary>Returns the native and managed execution plan for Where instead of the reusable filter.</summary>
    [Parameter(ParameterSetName = "Type")]
    [Parameter(ParameterSetName = "Definition")]
    public SwitchParameter Explain { get; set; }

    /// <summary>Event identifiers to include.</summary>
    [Alias("Id")]
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public int[]? EventId { get; set; }

    /// <summary>Event record identifiers to include.</summary>
    [Alias("EventRecordId")]
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public long[]? RecordId { get; set; }

    /// <summary>Provider names to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public string[]? ProviderName { get; set; }

    /// <summary>Numeric Windows event levels to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public EventViewerX.Level[]? Level { get; set; }

    /// <summary>Windows keyword masks to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public long[]? Keywords { get; set; }

    /// <summary>Absolute beginning of the time range.</summary>
    [Alias("DateFrom")]
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public DateTime? StartTime { get; set; }

    /// <summary>Absolute end of the time range.</summary>
    [Alias("DateTo")]
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public DateTime? EndTime { get; set; }

    /// <summary>Named relative time range.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>User security identifiers to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public string[]? UserId { get; set; }

    /// <summary>Unnamed EventData values to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public string[]? Data { get; set; }

    /// <summary>Named EventData values to include.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public Hashtable? NamedDataFilter { get; set; }

    /// <summary>Named EventData values to suppress.</summary>
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public Hashtable? NamedDataExcludeFilter { get; set; }

    /// <summary>Event identifiers to suppress.</summary>
    [Alias("ExcludeId")]
    [Parameter(ParameterSetName = "Object")]
    [Parameter(ParameterSetName = "XPath")]
    [Parameter(ParameterSetName = "ChannelXml")]
    [Parameter(ParameterSetName = "FileXml")]
    public int[]? ExcludeEventId { get; set; }

    /// <summary>Returns one native XPath expression.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "XPath")]
    public SwitchParameter AsXPath { get; set; }

    /// <summary>Channel used to produce QueryList XML.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ChannelXml")]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>Offline event-log files used to produce QueryList XML.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "FileXml")]
    [Alias("PSPath")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (ParameterSetName is "Type" or "Definition") {
            WriteTypedBuilder();
            return;
        }
        EventFilter filter = PowerShellEventFilterAdapter.CreateFilter(
            EventId,
            RecordId,
            ProviderName,
            Level,
            Keywords,
            StartTime,
            EndTime,
            TimePeriod,
            UserId,
            Data,
            NamedDataFilter,
            NamedDataExcludeFilter,
            ExcludeEventId);
        if (ParameterSetName == "Object") {
            WriteObject(filter);
            return;
        }

        EventFilterCompiler.SplitNamedDataExclusions(
            filter,
            out EventFilter? select,
            out EventFilter? suppression);
        IReadOnlyList<EventFilter> partitions =
            EventFilterPartitioner.Partition(select!);
        IReadOnlyList<EventFilter> suppressions =
            EventFilterPartitioner.PartitionNamedDataSuppression(suppression);
        if (ParameterSetName == "XPath") {
            if (suppressions.Count > 0) {
                throw new PSArgumentException(
                    "Named-data exclusions require QueryList XML. Supply LogName or Path instead of AsXPath.");
            }
            if (partitions.Count != 1) {
                throw new PSArgumentException(
                    "The filter exceeds one native XPath expression. Supply LogName or Path to generate partitioned QueryList XML.");
            }
            WriteObject(EventFilterCompiler.BuildXPath(partitions[0]));
            return;
        }

        string queryXml = ParameterSetName == "ChannelXml"
            ? EventFilterCompiler.BuildChannelUnionQueryXml(
                LogName,
                partitions,
                suppressions)
            : EventFilterCompiler.BuildFileUnionQueryXml(
                Path.Select(static path => System.IO.Path.GetFullPath(path)),
                partitions,
                suppressions);
        WriteObject(queryXml);
    }

    private void WriteTypedBuilder() {
        EventDefinition? definition = ParameterSetName == "Definition"
            ? ResolveDefinition()
            : null;
        EventPredicateBuilder builder = definition == null
            ? EventPredicateBuilder.ForType(Type!.Value)
            : EventPredicateBuilder.ForDefinition(definition);
        var filter = new PowerShellEventPredicateBuilder(builder, Type, definition);
        EventPredicate? predicate = PowerShellEventPredicateAdapter.Resolve(
            Where,
            nameof(Where),
            builder);
        if (predicate != null) {
            filter.Use(predicate);
        }
        if (Explain.IsPresent) {
            if (predicate == null) {
                throw new PSArgumentException("Explain requires Where so there is a typed predicate to plan.");
            }
            WriteObject(filter.Explain());
            return;
        }
        WriteObject(filter);
    }

    private EventDefinition ResolveDefinition() {
        object? value = Definition;
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        return value switch {
            EventDefinition typed => typed,
            string path => EventDefinition.Load(path),
            _ => throw new PSArgumentException(
                "Definition must be an EventDefinition instance or a JSON file path.",
                nameof(Definition))
        };
    }
}
