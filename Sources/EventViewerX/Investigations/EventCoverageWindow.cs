namespace EventViewerX;

/// <summary>A collection receipt for one exact source/query scope and a half-open UTC interval.</summary>
public sealed class EventCoverageWindow {
    /// <summary>Stable source and query scope identity. Different provider or event filters must use different identities.</summary>
    public string ScopeIdentity { get; set; } = string.Empty;
    /// <summary>Inclusive beginning of the collected interval.</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Exclusive end of the collected interval.</summary>
    public DateTime EndUtc { get; set; }
    /// <summary>True only after exhaustive collection of this exact scope, without limits or failed partitions.</summary>
    public bool IsComplete { get; set; }
    /// <summary>Collection failure, truncation, or uncertainty explanation.</summary>
    public string Diagnostic { get; set; } = string.Empty;

    internal void Validate() {
        if (string.IsNullOrWhiteSpace(ScopeIdentity) || StartUtc.Kind != DateTimeKind.Utc || EndUtc.Kind != DateTimeKind.Utc || EndUtc <= StartUtc) {
            throw new InvalidDataException("Coverage requires a scope identity and a nonempty UTC interval.");
        }
        if (IsComplete && !string.IsNullOrWhiteSpace(Diagnostic)) {
            throw new InvalidDataException("A coverage receipt with a diagnostic cannot claim complete collection.");
        }
    }

    /// <summary>Checks contiguous complete coverage; a failed overlapping receipt conservatively invalidates the affected interval.</summary>
    public static bool Covers(IEnumerable<EventCoverageWindow> receipts, string scopeIdentity, DateTime startUtc, DateTime endUtc) {
        if (receipts == null) { throw new ArgumentNullException(nameof(receipts)); }
        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc || endUtc <= startUtc || string.IsNullOrWhiteSpace(scopeIdentity)) {
            throw new ArgumentException("A nonempty UTC interval and scope identity are required.");
        }
        EventCoverageWindow[] windows = receipts.ToArray();
        foreach (EventCoverageWindow receipt in windows) {
            if (receipt == null) { throw new ArgumentException("Coverage receipts cannot contain null.", nameof(receipts)); }
            receipt.Validate();
        }
        windows = windows.Where(item => string.Equals(item.ScopeIdentity, scopeIdentity, StringComparison.Ordinal) &&
            item.StartUtc < endUtc && item.EndUtc > startUtc).OrderBy(item => item.StartUtc).ToArray();
        if (windows.Any(item => !item.IsComplete)) { return false; }
        DateTime cursor = startUtc;
        foreach (EventCoverageWindow receipt in windows) {
            if (receipt.StartUtc > cursor) { return false; }
            if (receipt.EndUtc > cursor) { cursor = receipt.EndUtc; }
            if (cursor >= endUtc) { return true; }
        }
        return false;
    }
}
