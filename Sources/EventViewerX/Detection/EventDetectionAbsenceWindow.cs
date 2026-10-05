namespace EventViewerX;

/// <summary>Explicit completeness boundary for absence evaluation. Receiving a later event alone never proves source completeness.</summary>
public sealed class EventDetectionAbsenceWindow {
    private readonly EventCoverageWindow[] _receipts;
    /// <summary>Creates a final evaluation boundary and snapshots exact source/query collection receipts.</summary>
    public EventDetectionAbsenceWindow(DateTime asOfUtc, IEnumerable<EventCoverageWindow> receipts) {
        if (asOfUtc.Kind != DateTimeKind.Utc) { throw new ArgumentException("As-of time must be UTC.", nameof(asOfUtc)); }
        if (receipts == null) { throw new ArgumentNullException(nameof(receipts)); }
        _receipts = receipts.Select(item => {
            if (item == null) { throw new ArgumentException("Receipts cannot contain null.", nameof(receipts)); }
            item.Validate();
            return new EventCoverageWindow { ScopeIdentity = item.ScopeIdentity, StartUtc = item.StartUtc,
                EndUtc = item.EndUtc, IsComplete = item.IsComplete, Diagnostic = item.Diagnostic };
        }).ToArray();
        AsOfUtc = asOfUtc;
    }
    /// <summary>UTC time through which deadlines may be evaluated.</summary>
    public DateTime AsOfUtc { get; }
    internal bool Covers(string scope, DateTime start, DateTime end) => end <= AsOfUtc && EventCoverageWindow.Covers(_receipts, scope, start, end);
}
