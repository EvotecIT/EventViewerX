namespace EventViewerX;

/// <summary>
/// Additional sub-status codes for failed authentication.
/// </summary>
public enum SubStatusCode : uint {
    /// <summary>No sub-status reported.</summary>
    None = 0x0,
    // Add any specific substatus codes as they are discovered
    /// <summary>Invalid logon hours.</summary>
    StatusInvalidLogonHours = 0xC000006F,
    /// <summary>Invalid workstation.</summary>
    StatusInvalidWorkstation = 0xC0000070,
    /// <summary>Password expired.</summary>
    StatusPasswordExpired = 0xC0000071,
    /// <summary>Account disabled.</summary>
    StatusAccountDisabled = 0xC0000072
}
