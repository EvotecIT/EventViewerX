namespace EventViewerX;

using System;
using System.Collections.Generic;

/// <summary>
/// Normalized details extracted from a collector subscription XML payload.
/// </summary>
public sealed class CollectorSubscriptionXmlDetails {
    /// <summary>The normalized XML payload safe for comparison and write-back.</summary>
    public string NormalizedXml { get; set; } = string.Empty;

    /// <summary>Subscription identity from the XML payload, when supplied.</summary>
    public string? SubscriptionId { get; set; }

    /// <summary>Description extracted from the XML payload.</summary>
    public string? Description { get; set; }

    /// <summary>Destination event-log channel extracted from the XML payload.</summary>
    public string? DestinationLog { get; set; }

    /// <summary>Subscription type from the XML payload, when supplied.</summary>
    public string? SubscriptionType { get; set; }

    /// <summary>Raw domain-computer source authorization SDDL, when supplied.</summary>
    public string? AllowedSourceDomainComputersSddl { get; set; }

    /// <summary>Raw AllowedSourceNonDomainComputers XML fragment, when supplied.</summary>
    public string? RawNonDomainSourceXml { get; set; }

    /// <summary>Raw non-domain certificate issuer CA allow-list, when supplied.</summary>
    public string? AllowedIssuerCAs { get; set; }

    /// <summary>Raw non-domain certificate subject allow-list, when supplied.</summary>
    public string? AllowedSubjects { get; set; }

    /// <summary>Raw non-domain certificate subject deny-list, when supplied.</summary>
    public string? DeniedSubjects { get; set; }

    /// <summary>Normalized event queries extracted from the XML payload.</summary>
    public IReadOnlyList<string> Queries { get; set; } = Array.Empty<string>();
}
