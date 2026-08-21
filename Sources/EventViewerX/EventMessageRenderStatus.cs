namespace EventViewerX;

/// <summary>Outcome of provider message rendering for an event snapshot.</summary>
public enum EventMessageRenderStatus {
    /// <summary>The selected read mode did not request provider message rendering.</summary>
    NotRequested,

    /// <summary>The Windows eventing API rendered the provider message successfully.</summary>
    Rendered,

    /// <summary>The provider metadata could not be opened for the requested event source and culture.</summary>
    ProviderMetadataUnavailable,

    /// <summary>The provider exists, but the requested message or locale resource is unavailable.</summary>
    MessageResourceUnavailable,

    /// <summary>Message rendering failed for another Windows eventing or runtime reason.</summary>
    Failed
}
