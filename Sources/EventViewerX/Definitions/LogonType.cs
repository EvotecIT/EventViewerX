namespace EventViewerX;

/// <summary>
/// Enumerates possible Windows logon types.
/// </summary>
public enum LogonType {
    /// <summary>Local console logon.</summary>
    Interactive = 2,
    /// <summary>Network logon (e.g., SMB).</summary>
    Network = 3,
    /// <summary>Batch logon (scheduled tasks).</summary>
    Batch = 4,
    /// <summary>Service logon.</summary>
    Service = 5,
    /// <summary>Unlock workstation.</summary>
    Unlock = 7,
    /// <summary>Network logon with cleartext credentials.</summary>
    NetworkCleartext = 8,
    /// <summary>New credentials (runas /netonly).</summary>
    NewCredentials = 9,
    /// <summary>Remote interactive (RDP / TS).</summary>
    RemoteInteractive = 10,
    /// <summary>Cached interactive logon.</summary>
    CachedInteractive = 11
}
