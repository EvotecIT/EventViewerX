namespace EventViewerX;

/// <summary>
/// Authentication package used when EventViewerX opens a remote Windows Event Log RPC session.
/// </summary>
public enum EventLogAuthentication {
    /// <summary>Lets Windows choose the authentication package.</summary>
    Default = 0,

    /// <summary>Uses Negotiate so Windows can select Kerberos or NTLM.</summary>
    Negotiate = 1,

    /// <summary>Requires Kerberos authentication.</summary>
    Kerberos = 2,

    /// <summary>Requires NTLM authentication.</summary>
    Ntlm = 3
}
