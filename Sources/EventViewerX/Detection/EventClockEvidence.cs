namespace EventViewerX;

/// <summary>Ordering justified by independently supplied clock bounds.</summary>
public enum EventClockOrder {
    /// <summary>Clock evidence is missing or possible time intervals overlap.</summary>
    Ambiguous,
    /// <summary>Every possible time for the first entry precedes the second.</summary>
    Before,
    /// <summary>Every possible time for the first entry follows the second.</summary>
    After
}

/// <summary>An independently established bound on a host clock, valid for a specified raw source-time interval.</summary>
public sealed class EventClockEvidence {
    /// <summary>Creates a clock bound. Correction is added to raw source time; uncertainty expands both ends.</summary>
    public EventClockEvidence(string computer, DateTime validFromUtc, DateTime validThroughUtc,
        TimeSpan correction, TimeSpan uncertainty, string provenance) {
        if (string.IsNullOrWhiteSpace(computer) || string.IsNullOrWhiteSpace(provenance)) {
            throw new ArgumentException("Computer and clock evidence provenance are required.");
        }
        if (validFromUtc.Kind != DateTimeKind.Utc || validThroughUtc.Kind != DateTimeKind.Utc || validThroughUtc < validFromUtc) {
            throw new ArgumentException("Clock validity must be an ordered UTC interval.");
        }
        if (uncertainty < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(uncertainty)); }
        // Reject bounds that cannot be represented rather than silently clamping them.
        _ = validFromUtc.Add(correction).Subtract(uncertainty);
        _ = validThroughUtc.Add(correction).Add(uncertainty);
        Computer = computer.Trim(); ValidFromUtc = validFromUtc; ValidThroughUtc = validThroughUtc;
        Correction = correction; Uncertainty = uncertainty; Provenance = provenance.Trim();
    }
    /// <summary>Source host, compared case-insensitively.</summary>
    public string Computer { get; }
    /// <summary>First raw source time covered by this bound.</summary>
    public DateTime ValidFromUtc { get; }
    /// <summary>Last raw source time covered by this bound.</summary>
    public DateTime ValidThroughUtc { get; }
    /// <summary>Measured correction added to the raw timestamp.</summary>
    public TimeSpan Correction { get; }
    /// <summary>Maximum error around corrected time.</summary>
    public TimeSpan Uncertainty { get; }
    /// <summary>Origin of the independent clock measurement or operator assumption.</summary>
    public string Provenance { get; }
}

/// <summary>Possible UTC times supported by clock evidence. Raw event timestamps are never modified.</summary>
public sealed class EventClockInterval {
    internal EventClockInterval(DateTime earliest, DateTime latest, IEnumerable<string> provenance) {
        EarliestUtc = earliest; LatestUtc = latest;
        Provenance = Array.AsReadOnly(provenance.Distinct(StringComparer.Ordinal).ToArray());
    }
    /// <summary>Earliest supported time.</summary>
    public DateTime EarliestUtc { get; }
    /// <summary>Latest supported time.</summary>
    public DateTime LatestUtc { get; }
    /// <summary>Clock evidence used to construct the interval.</summary>
    public IReadOnlyList<string> Provenance { get; }

    /// <summary>Compares intervals without interpreting receive delay as clock skew.</summary>
    public EventClockOrder CompareTo(EventClockInterval? other) => other == null ? EventClockOrder.Ambiguous :
        LatestUtc < other.EarliestUtc ? EventClockOrder.Before :
        EarliestUtc > other.LatestUtc ? EventClockOrder.After : EventClockOrder.Ambiguous;

    internal static EventClockInterval? Resolve(EventObservation observation, IReadOnlyList<EventClockEvidence> evidence) {
        EventClockEvidence[] matches = evidence.Where(item =>
            string.Equals(item.Computer, observation.SourceComputer, StringComparison.OrdinalIgnoreCase) &&
            item.ValidFromUtc <= observation.EventTimeUtc && item.ValidThroughUtc >= observation.EventTimeUtc).ToArray();
        if (matches.Length == 0) { return null; }
        // Multiple measurements are combined conservatively, including disagreement.
        return new EventClockInterval(matches.Min(item => observation.EventTimeUtc.Add(item.Correction).Subtract(item.Uncertainty)),
            matches.Max(item => observation.EventTimeUtc.Add(item.Correction).Add(item.Uncertainty)), matches.Select(item => item.Provenance));
    }
}
