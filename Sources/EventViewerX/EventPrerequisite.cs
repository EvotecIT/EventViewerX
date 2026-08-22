namespace EventViewerX;

/// <summary>One stable prerequisite used by readiness, documentation, and operator inspection.</summary>
public sealed class EventPrerequisite {
    internal EventPrerequisite(
        string key,
        EventRequirementKind kind,
        string name,
        string description,
        string appliesTo,
        EventAuditOutcome auditOutcomes = EventAuditOutcome.None,
        EventRequirementVolume volume = EventRequirementVolume.Unknown,
        string? documentationUri = null,
        Guid? auditSubcategoryGuid = null) {

        Key = key;
        Kind = kind;
        Name = name;
        Description = description;
        AppliesTo = appliesTo;
        AuditOutcomes = auditOutcomes;
        Volume = volume;
        DocumentationUri = documentationUri;
        AuditSubcategoryGuid = auditSubcategoryGuid;
    }

    /// <summary>Stable case-insensitive requirement identity.</summary>
    public string Key { get; }
    /// <summary>Requirement kind.</summary>
    public EventRequirementKind Kind { get; }
    /// <summary>Human-friendly requirement name.</summary>
    public string Name { get; }
    /// <summary>Actionable requirement explanation.</summary>
    public string Description { get; }
    /// <summary>Computer role or source to which the requirement applies.</summary>
    public string AppliesTo { get; }
    /// <summary>Required success and failure audit outcomes.</summary>
    public EventAuditOutcome AuditOutcomes { get; }
    /// <summary>Expected volume guidance.</summary>
    public EventRequirementVolume Volume { get; }
    /// <summary>Authoritative documentation URL when available.</summary>
    public string? DocumentationUri { get; }
    /// <summary>Culture-independent Windows audit subcategory identifier.</summary>
    public Guid? AuditSubcategoryGuid { get; }
}
