namespace EventViewerX;

/// <summary>Deterministically canonicalizes a known event field without external lookups.</summary>
public interface IEventValueNormalizer {
    /// <summary>Stable normalizer name.</summary>
    string Name { get; }

    /// <summary>Normalizer contract version.</summary>
    int Version { get; }

    /// <summary>Returns true only when this normalizer owns the supplied event-field context.</summary>
    bool CanNormalize(EventValueContext context);

    /// <summary>Normalizes the supplied value while retaining its raw representation.</summary>
    EventNormalizedValue Normalize(EventValueContext context);
}
