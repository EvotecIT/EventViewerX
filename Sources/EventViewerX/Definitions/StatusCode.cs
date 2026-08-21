namespace EventViewerX;

/// <summary>
/// Combined set of NTSTATUS values (logon failures) and Kerberos KDC error/status codes used by security events.
/// </summary>
public enum StatusCode : uint
{
    /// <summary>Request succeeded / no error.</summary>
    None = 0x00000000,

    // Kerberos KDC status codes (RFC 4120 / MS-KILE)
    /// <summary>Client entry has expired in the KDC database.</summary>
    KdcErrNameExp = 0x00000001,
    /// <summary>Service entry has expired in the KDC database.</summary>
    KdcErrServiceExp = 0x00000002,
    /// <summary>Kerberos protocol version in the request is unsupported.</summary>
    KdcErrBadPvno = 0x00000003,
    /// <summary>Client key was encrypted with an outdated master key.</summary>
    KdcErrCOldMastKvno = 0x00000004,
    /// <summary>Service key was encrypted with an outdated master key.</summary>
    KdcErrSOldMastKvno = 0x00000005,
    /// <summary>Client principal not found in the realm.</summary>
    KdcErrCPrincipalUnknown = 0x00000006,
    /// <summary>Service principal not found in the realm.</summary>
    KdcErrSPrincipalUnknown = 0x00000007,
    /// <summary>Principal name maps to multiple directory entries.</summary>
    KdcErrPrincipalNotUnique = 0x00000008,
    /// <summary>Account has no usable key material configured.</summary>
    KdcErrNullKey = 0x00000009,
    /// <summary>Ticket cannot be issued with a postdated start time.</summary>
    KdcErrCannotPostdate = 0x0000000A,
    /// <summary>Requested ticket would never become valid.</summary>
    KdcErrNeverValid = 0x0000000B,
    /// <summary>KDC policy prevented issuing the ticket.</summary>
    KdcErrPolicy = 0x0000000C,
    /// <summary>Request contained an unsupported or invalid KDC option.</summary>
    KdcErrBadOption = 0x0000000D,
    /// <summary>Requested encryption type is not supported.</summary>
    KdcErrEtypeNotSupp = 0x0000000E,
    /// <summary>Requested checksum type is not supported.</summary>
    KdcErrSumtypeNotSupp = 0x0000000F,
    /// <summary>Requested pre-authentication data type is not supported.</summary>
    KdcErrPadataTypeNotSupp = 0x00000010,
    /// <summary>Transited encoding type is not supported.</summary>
    KdcErrTrtypeNotSupp = 0x00000011,
    /// <summary>Client credentials have been revoked.</summary>
    KdcErrClientRevoked = 0x00000012,
    /// <summary>Service credentials have been revoked.</summary>
    KdcErrServiceRevoked = 0x00000013,
    /// <summary>Ticket-granting ticket (TGT) has been revoked.</summary>
    KdcErrTgtRevoked = 0x00000014,
    /// <summary>Client account is not yet valid.</summary>
    KdcErrClientNotYet = 0x00000015,
    /// <summary>Service account is not yet valid.</summary>
    KdcErrServiceNotYet = 0x00000016,
    /// <summary>Account key has expired.</summary>
    KdcErrKeyExpired = 0x00000017,
    /// <summary>Pre-authentication failed (bad password or data).</summary>
    KdcErrPreauthFailed = 0x00000018,
    /// <summary>Pre-authentication is required before a ticket can be issued.</summary>
    KdcErrPreauthRequired = 0x00000019,
    /// <summary>Requested server principal could not be matched for referral.</summary>
    KdcErrServerNOMatch = 0x0000001A,
    /// <summary>Server key has expired.</summary>
    KdcErrSKeyExpired = 0x0000001B,
    /// <summary>Authorization data or ticket lifetime has expired.</summary>
    KdcErrAuthExp = 0x0000001C,
    /// <summary>Pre-authentication data failed integrity verification.</summary>
    KdcErrPreauthBadIntegrity = 0x0000001D,
    /// <summary>Additional tickets were requested but cannot be issued.</summary>
    KdcErrAdditionalTickets = 0x0000001F,

    // NTSTATUS codes for logon failures
    /// <summary>Unknown username or bad password.</summary>
    StatusLogonFailure = 0xC000006D,
    /// <summary>User account restrictions.</summary>
    StatusAccountRestriction = 0xC000006E,
    /// <summary>Invalid logon hours.</summary>
    StatusInvalidLogonHours = 0xC000006F,
    /// <summary>Invalid workstation.</summary>
    StatusInvalidWorkstation = 0xC0000070,
    /// <summary>Password expired.</summary>
    StatusPasswordExpired = 0xC0000071,
    /// <summary>Account disabled.</summary>
    StatusAccountDisabled = 0xC0000072,
    /// <summary>Account locked out.</summary>
    StatusAccountLockedOut = 0xC0000234,
    /// <summary>Account expired.</summary>
    StatusAccountExpired = 0xC0000193,
    /// <summary>Logon type not granted.</summary>
    StatusLogonTypeNotGranted = 0xC000015B,
    /// <summary>No trust SAM account.</summary>
    StatusNoTrust = 0xC000005E
}
