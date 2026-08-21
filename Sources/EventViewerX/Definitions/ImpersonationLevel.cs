namespace EventViewerX;

/// <summary>
/// Describes the impersonation level used during authentication.
/// </summary>
public enum ImpersonationLevel {
    /// <summary>Server can obtain caller identity but not act on their behalf.</summary>
    Identification = 1832,
    /// <summary>Server can act on behalf of the caller on the local system.</summary>
    Impersonation = 1833,
    /// <summary>Server can delegate caller credentials to remote systems.</summary>
    Delegation = 1840,
    /// <summary>Impersonation denied by process trust label ACE.</summary>
    DeniedByProcessTrustLabelACE = 1841,
    /// <summary>Impersonation allowed (boolean helper value).</summary>
    Yes = 1842,
    /// <summary>Impersonation not allowed (boolean helper value).</summary>
    No = 1843,
    /// <summary>System-level impersonation.</summary>
    System = 1844,
    /// <summary>Impersonation level not available.</summary>
    NotAvailable = 1845,
    /// <summary>Default impersonation setting.</summary>
    Default = 1846,
    /// <summary>Impersonation disallowed due to managed metadata configuration.</summary>
    DisallowMmConfig = 1847,
    /// <summary>Impersonation explicitly off.</summary>
    Off = 1848,
    /// <summary>Impersonation automatically chosen.</summary>
    Auto = 1849
}
