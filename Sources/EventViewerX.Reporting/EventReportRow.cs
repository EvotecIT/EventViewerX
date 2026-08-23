namespace EventViewerX.Reporting;

/// <summary>A normalized event row shared by HTML, Excel, email, and transport adapters.</summary>
public sealed class EventReportRow {
    private static readonly HashSet<string> CommonFieldNames = new(
        typeof(EventReportRow).GetProperties()
            .Select(static property => property.Name)
            .Concat(new[] {
                "TypeName",
                "Id",
                "EventRecordId",
                "ProviderName",
                "SourceLogName",
                "LogName",
                "ContainerLogName",
                "MachineName",
                "Computer",
                "When",
                "LevelDisplayName"
            }),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Event timestamp.</summary>
    public DateTime TimeCreated { get; set; }
    /// <summary>Built-in type name or Generic.</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Event identifier.</summary>
    public int EventId { get; set; }
    /// <summary>Event record identifier.</summary>
    public long? RecordId { get; set; }
    /// <summary>Provider name.</summary>
    public string Provider { get; set; } = string.Empty;
    /// <summary>Original source channel.</summary>
    public string SourceLog { get; set; } = string.Empty;
    /// <summary>Container channel or file.</summary>
    public string ContainerLog { get; set; } = string.Empty;
    /// <summary>Whether the original query read a live channel or an offline event-log file.</summary>
    public EventLogQuerySourceKind SourceKind { get; set; }
    /// <summary>Computer that emitted the event.</summary>
    public string SourceComputer { get; set; } = string.Empty;
    /// <summary>Direct target or collector from which the event was read.</summary>
    public string CollectorComputer { get; set; } = string.Empty;
    /// <summary>Level display name.</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Numeric Windows event level used by exact predicates.</summary>
    public byte? LevelValue { get; set; }
    /// <summary>Rendered provider message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Type-specific projected values.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Deterministic canonical views of type-specific values. Raw evidence remains in <see cref="Values"/>.
    /// </summary>
    public IReadOnlyDictionary<string, EventNormalizedValue> NormalizedValues { get; internal set; } =
        new Dictionary<string, EventNormalizedValue>();

    /// <summary>Flattens common and type-specific fields for serialization and transport adapters.</summary>
    public IReadOnlyDictionary<string, object?> ToDictionary() {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            [nameof(TimeCreated)] = TimeCreated,
            [nameof(Type)] = Type,
            [nameof(EventId)] = EventId,
            [nameof(RecordId)] = RecordId,
            [nameof(Provider)] = Provider,
            [nameof(SourceLog)] = SourceLog,
            [nameof(ContainerLog)] = ContainerLog,
            [nameof(SourceKind)] = SourceKind,
            [nameof(SourceComputer)] = SourceComputer,
            [nameof(CollectorComputer)] = CollectorComputer,
            [nameof(Level)] = Level,
            [nameof(LevelValue)] = LevelValue,
            [nameof(Message)] = Message
        };
        foreach (KeyValuePair<string, object?> value in Values) {
            if (!result.ContainsKey(value.Key)) {
                result[value.Key] = value.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Flattens common fields and canonical type-specific values for grouping and aggregation.
    /// Use <see cref="ToDictionary()"/> when raw projected values are required.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ToNormalizedDictionary() {
        var result = ToDictionary().ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        AddCommonAliases(result);
        foreach (KeyValuePair<string, EventNormalizedValue> value in NormalizedValues) {
            if (string.Equals(Type, "Generic", StringComparison.OrdinalIgnoreCase) &&
                IsCommonFieldName(value.Key)) {
                continue;
            }
            result[value.Key] = value.Value.Value;
        }
        return result;
    }

    /// <summary>
    /// Flattens one homogeneous report section using canonical values while retaining the section's
    /// declared field contract.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ToNormalizedDictionary(EventReportSection section) {
        if (section == null) {
            throw new ArgumentNullException(nameof(section));
        }
        var result = ToNormalizedDictionary().ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        if (section.Kind == EventReportSectionKind.Generic) {
            return result;
        }
        foreach (EventReportColumn column in section.Columns) {
            result[column.Name] = NormalizedValues.TryGetValue(column.Name, out EventNormalizedValue? normalized)
                ? normalized.Value
                : Values.TryGetValue(column.Name, out object? value)
                    ? value
                    : null;
        }
        return result;
    }

    /// <summary>
    /// Flattens common and type-specific fields using one homogeneous report section as the output contract.
    /// Declared typed or custom fields take precedence over same-named native metadata; generic sections retain
    /// the native metadata contract.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ToDictionary(EventReportSection section) {
        if (section == null) {
            throw new ArgumentNullException(nameof(section));
        }
        var result = ToDictionary().ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        if (section.Kind == EventReportSectionKind.Generic) {
            return result;
        }
        foreach (EventReportColumn column in section.Columns) {
            result[column.Name] = Values.TryGetValue(column.Name, out object? value)
                ? value
                : null;
        }
        return result;
    }

    /// <summary>Flattens the row using the same field names and aliases as live typed predicates.</summary>
    public IReadOnlyDictionary<string, object?> ToPredicateDictionary() {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> value in ToDictionary()) {
            result[value.Key] = value.Value;
        }
        result["TypeName"] = Type;
        result["Id"] = EventId;
        result["EventRecordId"] = RecordId;
        result["ProviderName"] = Provider;
        result["SourceLogName"] = SourceLog;
        result["LogName"] = SourceLog;
        result["ContainerLogName"] = ContainerLog;
        result["MachineName"] = SourceComputer;
        result["Computer"] = SourceComputer;
        result["When"] = TimeCreated;
        result["Level"] = LevelValue.HasValue
            ? (EventViewerX.Level?)LevelValue.Value
            : null;
        result["LevelDisplayName"] = Level;
        foreach (KeyValuePair<string, object?> value in Values) {
            if (string.Equals(Type, "Generic", StringComparison.OrdinalIgnoreCase) &&
                IsCommonFieldName(value.Key)) {
                continue;
            }
            result[value.Key] = value.Value;
        }
        return result;
    }

    internal static bool IsCommonFieldName(string name) => CommonFieldNames.Contains(name);

    private void AddCommonAliases(IDictionary<string, object?> result) {
        result["TypeName"] = Type;
        result["Id"] = EventId;
        result["EventRecordId"] = RecordId;
        result["ProviderName"] = Provider;
        result["SourceLogName"] = SourceLog;
        result["LogName"] = SourceLog;
        result["ContainerLogName"] = ContainerLog;
        result["MachineName"] = SourceComputer;
        result["Computer"] = SourceComputer;
        result["When"] = TimeCreated;
        result["LevelDisplayName"] = Level;
    }
}
