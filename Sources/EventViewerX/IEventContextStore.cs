namespace EventViewerX;

/// <summary>Stores immutable context facts and resolves them by event time.</summary>
public interface IEventContextStore {
    /// <summary>Stores one fact idempotently.</summary>
    ValueTask StoreAsync(EventContextFact fact, CancellationToken cancellationToken = default);

    /// <summary>Resolves facts visible to one object query.</summary>
    ValueTask<EventContextResolution> ResolveAsync(
        EventContextQuery query,
        CancellationToken cancellationToken = default);
}
