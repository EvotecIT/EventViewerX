namespace EventViewerX;

/// <summary>Event-local effect of the KDC RC4 enforcement phase.</summary>
public enum KerberosRc4ImpactState {
    /// <summary>An audit event identifies a request that may fail under enforcement.</summary>
    MayFailUnderEnforcement,
    /// <summary>The KDC already denied the request.</summary>
    AlreadyBlocked,
    /// <summary>An explicit insecure domain-controller default remains active in either phase.</summary>
    ExplicitInsecureDefault
}

/// <summary>Observed requests sharing one KDC, account, service, issue, and impact state.</summary>
public sealed class KerberosRc4ImpactGroup {
    internal KerberosRc4ImpactGroup(
        string domainController,
        string accountName,
        string serviceName,
        string serviceSid,
        KerberosKdcRc4Issue issue,
        KerberosRc4ImpactState impact,
        long eventCount,
        DateTime latestEventUtc,
        IReadOnlyList<string> evidenceIdentities,
        bool evidenceTruncated) {

        DomainController = domainController;
        AccountName = accountName;
        ServiceName = serviceName;
        ServiceSid = serviceSid;
        Issue = issue;
        Impact = impact;
        EventCount = eventCount;
        LatestEventUtc = latestEventUtc;
        EvidenceIdentities = evidenceIdentities;
        EvidenceTruncated = evidenceTruncated;
    }

    /// <summary>Controller that emitted the KDCsvc evidence.</summary>
    public string DomainController { get; }
    /// <summary>Requesting account, when present.</summary>
    public string AccountName { get; }
    /// <summary>Target service, when present.</summary>
    public string ServiceName { get; }
    /// <summary>Target service SID, when present.</summary>
    public string ServiceSid { get; }
    /// <summary>Reason reported by the KDC.</summary>
    public KerberosKdcRc4Issue Issue { get; }
    /// <summary>Observed or projected enforcement effect.</summary>
    public KerberosRc4ImpactState Impact { get; }
    /// <summary>Number of matching event observations.</summary>
    public long EventCount { get; }
    /// <summary>Latest matching event time in UTC.</summary>
    public DateTime LatestEventUtc { get; }
    /// <summary>Bounded sample of stable source evidence identities.</summary>
    public IReadOnlyList<string> EvidenceIdentities { get; }
    /// <summary>Whether more evidence identities existed than the configured sample cap.</summary>
    public bool EvidenceTruncated { get; }
}

/// <summary>Bounded event evidence for planning KDC RC4 enforcement.</summary>
public sealed class KerberosRc4ImpactReport {
    internal KerberosRc4ImpactReport(
        IReadOnlyList<KerberosRc4ImpactGroup> groups,
        long eventsObserved,
        bool selectedWindowComplete,
        string? completenessDiagnostic) {

        Groups = groups;
        EventsObserved = eventsObserved;
        SelectedWindowComplete = selectedWindowComplete;
        CompletenessDiagnostic = completenessDiagnostic;
    }

    /// <summary>Per-controller, per-request impact groups.</summary>
    public IReadOnlyList<KerberosRc4ImpactGroup> Groups { get; }
    /// <summary>Number of KDCsvc 201-209 observations examined.</summary>
    public long EventsObserved { get; }
    /// <summary>Whether the supplied query window was exhaustive. This does not certify domain-controller coverage.</summary>
    public bool SelectedWindowComplete { get; }
    /// <summary>Query truncation or collection failure, when known.</summary>
    public string? CompletenessDiagnostic { get; }
    /// <summary>Coverage boundary: event evidence alone cannot prove that every controller and request was observed.</summary>
    public string CoverageStatement =>
        "This view covers supplied KDCsvc events only. Verify collection from every domain controller and relevant traffic before changing enforcement.";
}
