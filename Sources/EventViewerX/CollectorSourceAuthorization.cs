namespace EventViewerX;

/// <summary>Whether source-initiated WEC authorization was read or applies to the subscription.</summary>
public enum CollectorSourceAuthorizationStatus {
    /// <summary>The subscription is collector-initiated, so source authorization is not applicable.</summary>
    NotApplicable,
    /// <summary>The source-initiated authorization fields were read from subscription XML.</summary>
    Available,
    /// <summary>The subscription configuration could not be read or interpreted reliably.</summary>
    Unavailable
}

/// <summary>
/// Read-only authorization values from a source-initiated WEC subscription. Domain-computer
/// DACL entries and non-domain certificate subject rules are separate Windows controls.
/// Neither proves effective access for a particular source computer.
/// </summary>
public sealed class CollectorSourceAuthorization {
    internal CollectorSourceAuthorization(
        CollectorSourceAuthorizationStatus status,
        CollectorDomainSourceAccess? domainComputers,
        string? rawNonDomainSourceXml,
        string? allowedIssuerCAs,
        string? allowedSubjects,
        string? deniedSubjects,
        string? diagnostic = null) {
        Status = status;
        DomainComputers = domainComputers;
        RawNonDomainSourceXml = rawNonDomainSourceXml;
        AllowedIssuerCAs = allowedIssuerCAs;
        AllowedSubjects = allowedSubjects;
        DeniedSubjects = deniedSubjects;
        Diagnostic = diagnostic;
    }

    /// <summary>Whether source authorization applies and whether its configuration was read.</summary>
    public CollectorSourceAuthorizationStatus Status { get; }
    /// <summary>Parsed AllowedSourceDomainComputers DACL or an unavailable result; null when not applicable.</summary>
    public CollectorDomainSourceAccess? DomainComputers { get; }
    /// <summary>Original AllowedSourceNonDomainComputers XML fragment, when present.</summary>
    public string? RawNonDomainSourceXml { get; }
    /// <summary>Raw allowed issuer CA configuration for non-domain certificate sources, if supplied.</summary>
    public string? AllowedIssuerCAs { get; }
    /// <summary>Raw allowed subject configuration for non-domain certificate sources, if supplied.</summary>
    public string? AllowedSubjects { get; }
    /// <summary>Raw denied subject configuration for non-domain certificate sources, if supplied.</summary>
    public string? DeniedSubjects { get; }
    /// <summary>Why the authoritative subscription configuration could not be read, when applicable.</summary>
    public string? Diagnostic { get; }

    internal static CollectorSourceAuthorization Unavailable(string diagnostic) =>
        new CollectorSourceAuthorization(
            CollectorSourceAuthorizationStatus.Unavailable,
            CollectorDomainSourceAccess.Unavailable(null, diagnostic),
            null,
            null,
            null,
            null,
            diagnostic);

    internal static CollectorSourceAuthorization NotApplicable() =>
        new CollectorSourceAuthorization(CollectorSourceAuthorizationStatus.NotApplicable, null, null, null, null, null);
}
