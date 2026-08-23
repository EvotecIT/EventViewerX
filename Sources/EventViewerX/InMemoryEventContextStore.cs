namespace EventViewerX;

/// <summary>Process-local context store useful when durable storage is not requested.</summary>
public sealed class InMemoryEventContextStore : IEventContextStore {
    private readonly ConcurrentDictionary<string, EventContextFact> _facts = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask StoreAsync(EventContextFact fact, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EventContextFact snapshot = EventContextResolver.ValidateAndSnapshot(fact);
        _facts.TryAdd(EventContextIdentity.CreateFactKey(snapshot), snapshot);
        return default;
    }

    /// <inheritdoc />
    public ValueTask<EventContextResolution> ResolveAsync(
        EventContextQuery query,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<EventContextResolution>(EventContextResolver.Resolve(_facts.Values, query));
    }
}
