namespace EventViewerX;

/// <summary>A normalized event produced by a declarative custom definition.</summary>
public sealed class CustomEventRecord {
    internal CustomEventRecord(EventDefinition definition, EventObject sourceEvent, IReadOnlyDictionary<string, object?> values) {
        Definition = definition;
        SourceEvent = sourceEvent;
        Values = values;
    }
    /// <summary>Definition used for projection.</summary>
    public EventDefinition Definition { get; }
    /// <summary>Stable definition name.</summary>
    public string TypeName => Definition.Name;
    /// <summary>Original event snapshot.</summary>
    public EventObject SourceEvent { get; }
    /// <summary>Projected custom fields.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }
}
