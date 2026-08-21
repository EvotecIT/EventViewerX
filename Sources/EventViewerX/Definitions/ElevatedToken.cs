namespace EventViewerX;

/// <summary>
/// Specifies if an elevated token was used for the logon.
/// </summary>
public enum ElevatedToken {
    /// <summary>Logon used an elevated token.</summary>
    Yes = 1842,
    /// <summary>Logon did not use an elevated token.</summary>
    No = 1843
}
