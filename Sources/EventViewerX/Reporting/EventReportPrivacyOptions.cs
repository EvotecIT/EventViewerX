namespace EventViewerX.Reporting;

/// <summary>Explicit payload policy for a detached report export.</summary>
/// <remarks>Event times, IDs, provider, channel and level remain visible. Rows use one generic schema. This minimizes data; it does not guarantee anonymity.</remarks>
public sealed class EventReportPrivacyOptions {
    /// <summary>Payload field names whose scalar values may remain in the export. All other payload fields are omitted.</summary>
    public IReadOnlyCollection<string> RetainedValueFields { get; set; } = Array.Empty<string>();
    /// <summary>Payload field names replaced by field-scoped HMAC-SHA256 tokens. Requires a key of at least 32 bytes.</summary>
    public IReadOnlyCollection<string> PseudonymizedValueFields { get; set; } = Array.Empty<string>();
    /// <summary>Replaces computer, container and observation identities with tokens instead of omitting them. Requires a key.</summary>
    public bool PseudonymizeSourceIdentities { get; set; }
}
