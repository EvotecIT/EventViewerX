namespace EventViewerX;

/// <summary>
/// Flags used to describe Kerberos ticket (KDC) options, per RFC 4120 / MS-KILE.
/// </summary>
[Flags]
public enum TicketOptions : uint
{
    /// <summary>No flags set.</summary>
    None = 0x00000000,
    /// <summary>Ticket may be forwarded to another host (FORWARDABLE).</summary>
    Forwardable = 0x40000000,
    /// <summary>Ticket has already been forwarded (FORWARDED).</summary>
    Forwarded = 0x20000000,
    /// <summary>Ticket may be proxied (PROXIABLE).</summary>
    Proxiable = 0x10000000,
    /// <summary>Ticket is a proxy ticket (PROXY).</summary>
    Proxy = 0x08000000,
    /// <summary>Ticket may be postdated (ALLOW_POSTDATE).</summary>
    AllowPostdate = 0x04000000,
    /// <summary>Ticket has been postdated (POSTDATED).</summary>
    Postdated = 0x02000000,
    /// <summary>Ticket is currently invalid (INVALID).</summary>
    Invalid = 0x01000000,
    /// <summary>Ticket is renewable (RENEWABLE).</summary>
    Renewable = 0x00800000,
    /// <summary>Initial ticket in the authentication exchange (INITIAL).</summary>
    Initial = 0x00400000,
    /// <summary>Pre-authentication was performed (PRE_AUTHENT).</summary>
    PreAuthenticated = 0x00200000,
    /// <summary>Client used hardware authentication (HW_AUTHENT).</summary>
    HwAuthenticated = 0x00100000,
    /// <summary>Transit policy was checked (TRANSITED_POLICY_CHECKED).</summary>
    TransitPolicyChecked = 0x00080000,
    /// <summary>Ticket may be used for delegation (OK_AS_DELEGATE).</summary>
    OkAsDelegate = 0x00040000,
    /// <summary>Request is anonymous (ANONYMOUS).</summary>
    Anonymous = 0x00020000,
    /// <summary>Client requests canonicalized principal names (CANONICALIZE).</summary>
    Canonicalize = 0x00010000,
    /// <summary>Embedded ticket in second ticket (CNAME_IN_ADDL_TKT / ENC_TKT_IN_SKEY).</summary>
    EncTicketInSessionKey = 0x00000008,
    /// <summary>Client is requesting ticket renewal (RENEW).</summary>
    Renew = 0x00000002,
    /// <summary>Client is validating an existing ticket (VALIDATE).</summary>
    Validate = 0x00000001,
    /// <summary>Renewable lifetime is acceptable though end time exceeded (RENEWABLE_OK).</summary>
    RenewableOk = 0x00000010
}
