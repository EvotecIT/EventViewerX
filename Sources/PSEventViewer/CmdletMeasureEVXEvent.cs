using System.Globalization;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Computes bounded event counts, distinct values, first/last observations, rates, and time trends.</para>
/// <para type="description">Uses the shared deterministic aggregation contract for pipeline events and safe SQLite pushdown for stored history.</para>
/// <para type="description">A single EventReport preserves its source-coverage evidence. Individual pipeline rows have unknown completeness because a pipeline cannot prove that it contains the complete source query.</para>
/// </summary>
/// <example>
///   <summary>Count failed logons by account</summary>
///   <code>Get-EVXEvent -Type ADUserLogonFailed -TimePeriod Last24Hours | Measure-EVXEvent -GroupBy Who</code>
///   <para>Returns one completeness-aware aggregation result containing deterministic rows.</para>
/// </example>
/// <example>
///   <summary>Create an hourly stored trend</summary>
///   <code>Measure-EVXEvent -FromStore C:\EventViewerX\events.db -Type AuthenticationHealth -Bucket Hour -GroupBy Type</code>
///   <para>Uses SQLite pushdown when the selected fields and UTC bucket can preserve the shared semantics.</para>
/// </example>
/// <example>
///   <summary>Count distinct source computers</summary>
///   <code>Get-EVXEvent -Preset AuthenticationHealth -TimePeriod Last7Days | Measure-EVXEvent -GroupBy Type -Measure 'Count', 'DistinctCount:SourceComputer:Sources'</code>
///   <para>String measure specifications use Operation:Field:OutputName:RateUnit and can be mixed with typed measure objects or hashtables.</para>
/// </example>
[OutputType(typeof(EventAggregationResult))]
[OutputType(typeof(EventStoreAggregationPlan))]
[Cmdlet(VerbsDiagnostic.Measure, "EVXEvent", DefaultParameterSetName = "Input")]
public sealed class CmdletMeasureEVXEvent : AsyncPSCmdlet {
    private readonly List<object> _input = new();

    /// <summary>Event rows, typed EventViewerX records, or one EventReport to aggregate. Supply an EventReport alone to preserve its completeness envelope.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Input")]
    public object? InputObject { get; set; }

    /// <summary>SQLite EventStore path.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Store")]
    public string? FromStore { get; set; }

    /// <summary>Stored built-in event types to include.</summary>
    [Parameter(ParameterSetName = "Store")]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <summary>Stored definition names to include.</summary>
    [Parameter(ParameterSetName = "Store")]
    public string[] DefinitionName { get; set; } = Array.Empty<string>();

    /// <summary>Stored event lower time boundary.</summary>
    [Parameter(ParameterSetName = "Store")]
    public DateTime? StartTime { get; set; }

    /// <summary>Stored event upper time boundary.</summary>
    [Parameter(ParameterSetName = "Store")]
    public DateTime? EndTime { get; set; }

    /// <summary>Stored source computers to include.</summary>
    [Parameter(ParameterSetName = "Store")]
    public string[] SourceComputer { get; set; } = Array.Empty<string>();

    /// <summary>Canonical dimensions forming each group key.</summary>
    [Parameter]
    public string[] GroupBy { get; set; } = Array.Empty<string>();

    /// <summary>Calendar trend bucket.</summary>
    [Parameter]
    public EventAggregationBucket Bucket { get; set; }

    /// <summary>Timezone used for calendar buckets. UTC enables stored pushdown.</summary>
    [Parameter]
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Typed measures, hashtables, or Operation:Field:OutputName:RateUnit strings. Count is used by default.</summary>
    [Parameter]
    public object[] Measure { get; set; } = Array.Empty<object>();

    /// <summary>Maximum ranked groups returned. Zero returns every group.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Top { get; set; }

    /// <summary>Global or per-bucket top-N scope.</summary>
    [Parameter]
    public EventAggregationTopScope TopScope { get; set; } = EventAggregationTopScope.GlobalGroup;

    /// <summary>Measure output used for top-N ranking.</summary>
    [Parameter]
    public string? RankingMeasure { get; set; }

    /// <summary>Explicit unbucketed rate interval start.</summary>
    [Parameter]
    public DateTime? WindowStart { get; set; }

    /// <summary>Explicit unbucketed rate interval end.</summary>
    [Parameter]
    public DateTime? WindowEnd { get; set; }

    /// <summary>Maximum aggregation groups retained.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumGroups { get; set; } = 25000;

    /// <summary>Maximum distinct values retained per measure.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumDistinctValues { get; set; } = 100000;

    /// <summary>Maximum approximate managed aggregation-state bytes.</summary>
    [Parameter]
    [ValidateRange(1, long.MaxValue)]
    public long MaximumStateBytes { get; set; } = 64L * 1024L * 1024L;

    /// <summary>Returns the selected stored execution owner without aggregating.</summary>
    [Parameter(ParameterSetName = "Store")]
    public SwitchParameter Explain { get; set; }

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
        EventAggregationDefinition definition = CreateDefinition();
        if (ParameterSetName == "Store") {
            if (Type.Length > 0 && DefinitionName.Length > 0) {
                throw new PSArgumentException("Type and DefinitionName are mutually exclusive.", nameof(DefinitionName));
            }
            var query = new EventStoreQuery {
                Types = Type.Length == 0 ? null : Type,
                DefinitionNames = DefinitionName.Length == 0 ? null : DefinitionName,
                StartTime = StartTime,
                EndTime = EndTime,
                SourceComputers = SourceComputer.Length == 0 ? null : SourceComputer,
                MaxEvents = 0
            };
            if (Explain.IsPresent) {
                WriteObject(EventStore.PlanAggregation(query, definition));
                return;
            }
            EventAggregationResult stored = await new EventStore(FromStore!)
                .AggregateAsync(query, definition, CancelToken)
                .ConfigureAwait(false);
            WriteObject(stored);
            return;
        }
        EventAggregationResult result;
        if (_input.Count == 1 && _input[0] is EventReport existingReport) {
            result = EventAggregationEngine.Aggregate(existingReport, definition);
        } else {
            if (_input.Any(static item => item is EventReport)) {
                throw new PSArgumentException(
                    "An EventReport must be supplied as the only pipeline input.",
                    nameof(InputObject));
            }
            EventReportRow[] rows = _input.Select(static item => item is EventReportRow row
                    ? row
                    : EventReportEngine.CreateRow(item))
                .ToArray();
            result = EventAggregationEngine.Aggregate(
                rows,
                definition,
                EventAggregationInputCompleteness.Unknown);
        }
        WriteObject(result);
    }

    private EventAggregationDefinition CreateDefinition() => new() {
        GroupBy = GroupBy,
        Bucket = Bucket,
        TimeZoneId = TimeZoneId,
        Measures = Measure.Length == 0
            ? new[] { new EventAggregationMeasure { Operation = EventAggregationOperation.Count, OutputName = "Count" } }
            : Measure.Select(ParseMeasure).ToArray(),
        Top = Top,
        TopScope = TopScope,
        RankingMeasure = RankingMeasure,
        WindowStart = WindowStart,
        WindowEnd = WindowEnd,
        MaximumGroups = MaximumGroups,
        MaximumDistinctValues = MaximumDistinctValues,
        MaximumStateBytes = MaximumStateBytes
    };

    private static EventAggregationMeasure ParseMeasure(object value) {
        while (value is PSObject wrapper && wrapper.BaseObject != value) {
            value = wrapper.BaseObject;
        }
        if (value is EventAggregationMeasure typed) {
            return typed;
        }
        if (value is string text) {
            string[] parts = text.Split(new[] { ':' }, 4);
            if (!Enum.TryParse(parts[0], ignoreCase: true, out EventAggregationOperation operation) ||
                !Enum.IsDefined(typeof(EventAggregationOperation), operation)) {
                throw new PSArgumentException($"Unknown aggregation operation '{parts[0]}'.", nameof(Measure));
            }
            return new EventAggregationMeasure {
                Operation = operation,
                Field = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null,
                OutputName = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null,
                RateUnit = parts.Length > 3 && parts[3].Length > 0
                    ? TimeSpan.Parse(parts[3], CultureInfo.InvariantCulture)
                    : null
            };
        }
        if (value is IDictionary dictionary) {
            string operationText = Convert.ToString(dictionary["Operation"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (!Enum.TryParse(operationText, ignoreCase: true, out EventAggregationOperation operation) ||
                !Enum.IsDefined(typeof(EventAggregationOperation), operation)) {
                throw new PSArgumentException("A measure hashtable requires a valid Operation.", nameof(Measure));
            }
            string? nullsText = Convert.ToString(dictionary["Nulls"], CultureInfo.InvariantCulture);
            EventAggregationNullPolicy nulls = string.IsNullOrWhiteSpace(nullsText)
                ? EventAggregationNullPolicy.Exclude
                : Enum.TryParse(nullsText, ignoreCase: true, out EventAggregationNullPolicy parsedNulls) &&
                  Enum.IsDefined(typeof(EventAggregationNullPolicy), parsedNulls)
                    ? parsedNulls
                    : throw new PSArgumentException("A measure hashtable contains an invalid Nulls value.", nameof(Measure));
            string? rateText = Convert.ToString(dictionary["RateUnit"], CultureInfo.InvariantCulture);
            return new EventAggregationMeasure {
                Operation = operation,
                Field = Convert.ToString(dictionary["Field"], CultureInfo.InvariantCulture),
                OutputName = Convert.ToString(dictionary["OutputName"], CultureInfo.InvariantCulture),
                Nulls = nulls,
                RateUnit = string.IsNullOrWhiteSpace(rateText)
                    ? null
                    : TimeSpan.Parse(rateText, CultureInfo.InvariantCulture)
            };
        }
        throw new PSArgumentException(
            "Measure values must be EventAggregationMeasure objects, hashtables, or Operation:Field:OutputName:RateUnit strings.",
            nameof(Measure));
    }
}
