namespace EventViewerX;

/// <summary>Built-in decision-oriented report profiles over observations and findings.</summary>
public enum EventDecisionReportKind {
    /// <summary>Collection coverage, lag, volume, failures, and retained evidence.</summary>
    CollectionCoverage,
    /// <summary>Audit, channel, provider, and delivery integrity.</summary>
    EventingIntegrity,
    /// <summary>Authentication protocols, failures, and modernization posture.</summary>
    AuthenticationPosture,
    /// <summary>Account lifecycle and related activity.</summary>
    IdentityLifecycle,
    /// <summary>Privileged membership, rights, and subsequent access.</summary>
    PrivilegedAccess,
    /// <summary>Group Policy creation, edits, links, enforcement, and deletion.</summary>
    GroupPolicyGovernance,
    /// <summary>Certificate Services governance and readiness evidence.</summary>
    CertificateServicesGovernance,
    /// <summary>Execution, persistence, firewall, and endpoint protection activity.</summary>
    ExecutionAndPersistence,
    /// <summary>Pack validity, required data, matches, suppressions, and incomplete runs.</summary>
    DetectionHealth,
    /// <summary>Unmapped events, missing fields, and projection or schema drift.</summary>
    UnknownEventAndSchemaDrift,
    /// <summary>Ordered findings and raw evidence with reusable pivots.</summary>
    IncidentTimeline
}
