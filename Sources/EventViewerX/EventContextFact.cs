namespace EventViewerX;

/// <summary>An immutable-in-storage observation about one object at event time.</summary>
public sealed class EventContextFact {
    /// <summary>Object family described by the fact.</summary>
    public EventContextObjectKind ObjectKind { get; set; }

    /// <summary>Canonical stable identity, normally a normalized object GUID.</summary>
    public string CanonicalId { get; set; } = string.Empty;

    /// <summary>Additional identities such as old and new distinguished names.</summary>
    public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();

    /// <summary>Display name carried by this observation, when present.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Directory domain carried or derived from the observation.</summary>
    public string? Domain { get; set; }

    /// <summary>Distinguished name effective for this observation.</summary>
    public string? DistinguishedName { get; set; }

    /// <summary>UTC event time at which the fact became effective.</summary>
    public DateTime EffectiveAtUtc { get; set; }

    /// <summary>UTC time at which EventViewerX observed the fact.</summary>
    public DateTime ObservedAtUtc { get; set; }

    /// <summary>Whether this observation says that the object was deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Origin of the fact.</summary>
    public EventContextProvenance Provenance { get; set; } = EventContextProvenance.Event;

    /// <summary>Stable identity of the event, lookup, or import that supplied the fact.</summary>
    public string SourceIdentity { get; set; } = string.Empty;

    /// <summary>Compiled provider that produced the fact.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Provider schema version used to interpret the fact.</summary>
    public int ProviderSchemaVersion { get; set; } = 1;

    /// <summary>Human-readable reason for the confidence assigned to the fact.</summary>
    public string? ConfidenceReason { get; set; }

    /// <summary>Optional authorization partition for non-shareable lookup or import evidence.</summary>
    public string? AuthorizationContext { get; set; }

    /// <summary>Whether the fact can be reused outside its authorization partition.</summary>
    public bool IsShareable { get; set; } = true;
}
