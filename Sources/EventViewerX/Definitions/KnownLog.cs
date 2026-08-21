namespace EventViewerX;

/// <summary>
/// Enumerates common Windows event logs.
/// </summary>
public enum KnownLog {
    /// <summary>Application log.</summary>
    Application,
    /// <summary>System log.</summary>
    System,
    /// <summary>Security log.</summary>
    Security,
    /// <summary>Setup log.</summary>
    Setup,
    /// <summary>ForwardedEvents subscription log.</summary>
    ForwardedEvents,
    /// <summary>Active Directory Directory Service log.</summary>
    DirectoryService,
    /// <summary>DNS Server log.</summary>
    DNSServer,
    /// <summary>Windows PowerShell operational log.</summary>
    WindowsPowerShell,
    // Add other well-known logs as needed
}
