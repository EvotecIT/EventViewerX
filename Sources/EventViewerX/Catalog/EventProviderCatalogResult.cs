namespace EventViewerX;

/// <summary>Success or failure for one provider catalog entry.</summary>
public sealed class EventProviderCatalogResult {
    internal EventProviderCatalogResult(
        string providerName,
        EventProviderMetadataSnapshot? provider,
        Exception? exception) {

        ProviderName = providerName;
        Provider = provider;
        Exception = exception;
    }

    /// <summary>Requested provider name.</summary>
    public string ProviderName { get; }
    /// <summary>Detached provider metadata when successful.</summary>
    public EventProviderMetadataSnapshot? Provider { get; }
    /// <summary>Failure when the provider could not be opened.</summary>
    public Exception? Exception { get; }
    /// <summary>Whether provider metadata was returned.</summary>
    public bool Success => Provider != null && Exception == null;
}
