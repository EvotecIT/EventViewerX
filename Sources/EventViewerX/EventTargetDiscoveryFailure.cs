namespace EventViewerX;

/// <summary>One independently reported target-discovery failure.</summary>
public sealed class EventTargetDiscoveryFailure {
    /// <summary>Creates a classified failure.</summary>
    public EventTargetDiscoveryFailure(
        string scope,
        string stage,
        EventTargetDiscoveryFailureKind kind,
        string message) {

        Scope = string.IsNullOrWhiteSpace(scope) ? "Unknown" : scope.Trim();
        Stage = string.IsNullOrWhiteSpace(stage) ? "Discovery" : stage.Trim();
        Kind = kind;
        Message = string.IsNullOrWhiteSpace(message) ? kind.ToString() : message.Trim();
    }

    /// <summary>Domain, forest, trust, or local scope that failed.</summary>
    public string Scope { get; }
    /// <summary>Discovery stage that failed.</summary>
    public string Stage { get; }
    /// <summary>Stable failure classification.</summary>
    public EventTargetDiscoveryFailureKind Kind { get; }
    /// <summary>Human-readable diagnostic message.</summary>
    public string Message { get; }
}
