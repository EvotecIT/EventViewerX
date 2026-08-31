namespace EventViewerX;

/// <summary>Canonical hunting pivot category.</summary>
public enum EventPivotKind {
    /// <summary>Actor that initiated an action.</summary>
    Actor,
    /// <summary>Object, account, or resource affected.</summary>
    Target,
    /// <summary>Source or collector host.</summary>
    Host,
    /// <summary>Actor or target account.</summary>
    Account,
    /// <summary>Windows security identifier.</summary>
    Sid,
    /// <summary>IPv4 or IPv6 address.</summary>
    IpAddress,
    /// <summary>Process name or identifier.</summary>
    Process,
    /// <summary>ETW activity or related-activity identifier.</summary>
    Activity,
    /// <summary>Authentication logon identifier.</summary>
    Logon,
    /// <summary>Provider transaction or correlation identifier.</summary>
    Transaction
}
