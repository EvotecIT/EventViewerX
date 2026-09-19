namespace EventViewerX;

/// <summary>
/// One DACL entry in the source-initiated collector's AllowedSourceDomainComputers descriptor.
/// This is not an effective authorization decision for a particular forwarding computer.
/// </summary>
public sealed class CollectorDomainSourceAccessRule {
    internal CollectorDomainSourceAccessRule(ParsedSecurityDescriptorAce ace) {
        TrusteeSid = ace.TrusteeSid;
        AceType = ace.AceType;
        IsAllow = ace.IsAllow;
        IsDeny = ace.IsDeny;
        IsInherited = ace.IsInherited;
        AccessMask = ace.AccessMask;
    }

    /// <summary>Trustee SID, independent of account-name localization.</summary>
    public string TrusteeSid { get; }
    /// <summary>Native ACE type.</summary>
    public string AceType { get; }
    /// <summary>Whether this ACE explicitly allows rights.</summary>
    public bool IsAllow { get; }
    /// <summary>Whether this ACE explicitly denies rights.</summary>
    public bool IsDeny { get; }
    /// <summary>Whether this ACE is inherited.</summary>
    public bool IsInherited { get; }
    /// <summary>Unmodified native access mask; it uses WEC source authorization semantics, not event-log Read/Write/Clear bits.</summary>
    public int AccessMask { get; }
    /// <summary>Whether the ACE contains the generic-all bit (0x10000000), commonly used in WEC source authorization SDDL.</summary>
    public bool IncludesGenericAll => (AccessMask & 0x10000000) != 0;
}

/// <summary>
/// Read-only view of source-initiated collector authorization for domain computers. Non-domain
/// certificate subject allow/deny lists are separate WEC properties and are not represented here.
/// </summary>
public sealed class CollectorDomainSourceAccess {
    private CollectorDomainSourceAccess(
        string? securityDescriptor,
        SecurityDescriptorDaclState daclState,
        IReadOnlyList<CollectorDomainSourceAccessRule>? accessRules,
        string? diagnostic) {
        SecurityDescriptor = securityDescriptor;
        DaclState = daclState;
        AccessRules = accessRules;
        Diagnostic = diagnostic;
    }

    /// <summary>Raw AllowedSourceDomainComputers SDDL, if it could be read.</summary>
    public string? SecurityDescriptor { get; }
    /// <summary>Presence and shape of the DACL; unavailable does not mean empty.</summary>
    public SecurityDescriptorDaclState DaclState { get; }
    /// <summary>Ordered DACL entries, or null when the descriptor could not be read or parsed.</summary>
    public IReadOnlyList<CollectorDomainSourceAccessRule>? AccessRules { get; }
    /// <summary>Reason the domain-source authorization could not be determined, when known.</summary>
    public string? Diagnostic { get; }

    /// <summary>Interprets domain-source authorization SDDL without contacting or changing a collector.</summary>
    public static CollectorDomainSourceAccess Inspect(string? securityDescriptor) {
        if (string.IsNullOrWhiteSpace(securityDescriptor)) {
            return Unavailable(securityDescriptor, "The subscription did not provide AllowedSourceDomainComputers SDDL. Windows can apply a default when all source authorization fields are empty; no ACL is inferred here.");
        }

        try {
            ParsedSecurityDescriptorDacl parsed = SecurityDescriptorDaclParser.Parse(securityDescriptor!);
            return new CollectorDomainSourceAccess(
                securityDescriptor,
                parsed.DaclState,
                parsed.AccessRules
                    .Select(static ace => new CollectorDomainSourceAccessRule(ace))
                    .ToList()
                    .AsReadOnly(),
                null);
        } catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException or FormatException) {
            return Unavailable(securityDescriptor, exception.Message);
        }
    }

    internal static CollectorDomainSourceAccess Unavailable(string? securityDescriptor, string diagnostic) =>
        new CollectorDomainSourceAccess(securityDescriptor, SecurityDescriptorDaclState.Unavailable, null, diagnostic);
}
