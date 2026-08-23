namespace EventViewerX;

/// <summary>Identifies one object and the event time at which its context should be resolved.</summary>
public sealed class EventContextQuery {
    /// <summary>Object family being resolved.</summary>
    public EventContextObjectKind ObjectKind { get; set; }

    /// <summary>Canonical identity when already known.</summary>
    public string? CanonicalId { get; set; }

    /// <summary>Alternate identity, such as a distinguished name.</summary>
    public string? Alias { get; set; }

    /// <summary>UTC event time for the resolution.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Authorization partition allowed to see matching non-shareable evidence.</summary>
    public string? AuthorizationContext { get; set; }
}
