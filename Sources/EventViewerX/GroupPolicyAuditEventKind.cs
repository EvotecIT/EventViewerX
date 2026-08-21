namespace EventViewerX;

/// <summary>Directory Service Changes operation represented by a Group Policy audit event.</summary>
public enum GroupPolicyAuditEventKind {
    /// <summary>An existing directory object or attribute was modified.</summary>
    Modified = 5136,
    /// <summary>A directory object was created.</summary>
    Created = 5137,
    /// <summary>A directory object was moved.</summary>
    Moved = 5139,
    /// <summary>A directory object was deleted.</summary>
    Deleted = 5141
}
