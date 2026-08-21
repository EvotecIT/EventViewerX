namespace EventViewerX;

/// <summary>
/// Indicates that one or more matching event-type rules failed while projecting a raw event.
/// </summary>
public sealed class EventRuleProjectionException : Exception {
    internal EventRuleProjectionException(
        EventObject eventObject,
        IReadOnlyList<string> ruleNames,
        IReadOnlyList<Exception> errors)
        : base(
            BuildMessage(eventObject, ruleNames),
            new AggregateException(errors)) {
        EventId = eventObject.Id;
        RecordId = eventObject.RecordId;
        LogName = ResolveLogName(eventObject);
        RuleNames = ruleNames.ToArray();
    }

    /// <summary>Event identifier that could not be projected.</summary>
    public int EventId { get; }

    /// <summary>Event-record identifier when the source supplied one.</summary>
    public long? RecordId { get; }

    /// <summary>Source log containing the event.</summary>
    public string LogName { get; }

    /// <summary>Rule types or event-type registrations that failed.</summary>
    public IReadOnlyList<string> RuleNames { get; }

    private static string BuildMessage(EventObject eventObject, IReadOnlyList<string> ruleNames) {
        string record = eventObject.RecordId.HasValue
            ? $" record {eventObject.RecordId.Value}"
            : string.Empty;
        return $"Named-event projection failed for event {eventObject.Id}{record} in '{ResolveLogName(eventObject)}' " +
               $"using rule(s): {string.Join(", ", ruleNames)}.";
    }

    private static string ResolveLogName(EventObject eventObject) {
        return eventObject.OriginalLogName;
    }
}
