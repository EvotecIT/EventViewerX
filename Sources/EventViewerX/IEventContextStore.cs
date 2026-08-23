namespace EventViewerX;

/// <summary>Stores immutable context facts and resolves them by event time.</summary>
public interface IEventContextStore {
    /// <summary>Stores one fact idempotently.</summary>
    ValueTask StoreAsync(EventContextFact fact, CancellationToken cancellationToken = default);

    /// <summary>Stores a batch of facts idempotently using one store operation.</summary>
    ValueTask StoreManyAsync(
        IReadOnlyList<EventContextFact> facts,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves facts visible to one object query.</summary>
    ValueTask<EventContextResolution> ResolveAsync(
        EventContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a batch of event-time queries from one materialized store snapshot.</summary>
    ValueTask<IReadOnlyList<EventContextResolution>> ResolveManyAsync(
        IReadOnlyList<EventContextQuery> queries,
        CancellationToken cancellationToken = default);
}
