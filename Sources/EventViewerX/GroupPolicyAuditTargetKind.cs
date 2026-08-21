namespace EventViewerX;

/// <summary>Group Policy surface affected by a directory audit event.</summary>
public enum GroupPolicyAuditTargetKind {
    /// <summary>The event targets a Group Policy container.</summary>
    GroupPolicyObject,
    /// <summary>The event targets the <c>gPLink</c> list on a domain, OU, or site.</summary>
    ScopeLinks,
    /// <summary>The event targets the <c>gPOptions</c> inheritance setting on a domain, OU, or site.</summary>
    ScopeInheritance,
    /// <summary>The event targets the <c>gPCWQLFilter</c> assignment on a Group Policy container.</summary>
    WmiFilterAssignment,
    /// <summary>The event targets an <c>msWMI-Som</c> WMI filter definition.</summary>
    WmiFilterDefinition
}
