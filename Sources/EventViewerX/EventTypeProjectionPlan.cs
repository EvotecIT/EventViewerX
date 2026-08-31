namespace EventViewerX;

/// <summary>
/// Immutable event-type projection plan compiled once for a query or watcher.
/// </summary>
public sealed class EventTypeProjectionPlan {
    private static readonly EventProjectorDefinition[] EmptyCandidates = Array.Empty<EventProjectorDefinition>();
    private readonly Dictionary<SourceKey, EventProjectorDefinition[]> _candidatesBySource;

    internal EventTypeProjectionPlan(
        EventType[] requestedTypes,
        EventType[] expandedTypes,
        Dictionary<SourceKey, EventProjectorDefinition[]> candidatesBySource) {

        RequestedTypes = Array.AsReadOnly(requestedTypes);
        ExpandedTypes = Array.AsReadOnly(expandedTypes);
        _candidatesBySource = candidatesBySource;
    }

    /// <summary>Gets the leaf or composite types supplied by the caller.</summary>
    public IReadOnlyList<EventType> RequestedTypes { get; }

    /// <summary>Gets the distinct leaf types represented by this plan.</summary>
    public IReadOnlyList<EventType> ExpandedTypes { get; }

    internal IReadOnlyList<EventProjectorDefinition> GetCandidates(int eventId, string logName) {
        return _candidatesBySource.TryGetValue(new SourceKey(eventId, logName), out EventProjectorDefinition[]? candidates)
            ? candidates
            : EmptyCandidates;
    }

    internal readonly struct SourceKey : IEquatable<SourceKey> {
        internal SourceKey(int eventId, string logName) {
            EventId = eventId;
            LogName = logName ?? string.Empty;
        }

        internal int EventId { get; }
        internal string LogName { get; }

        public bool Equals(SourceKey other) {
            return EventId == other.EventId &&
                   string.Equals(LogName, other.LogName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj) {
            return obj is SourceKey other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return (EventId * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(LogName);
            }
        }
    }
}
