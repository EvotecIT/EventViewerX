namespace EventViewerX;

/// <summary>
/// Canonical field names commonly present in event payload dictionaries.
/// </summary>
public enum KnownEventField {
    /// <summary>Primary message subject key.</summary>
    Message,
    /// <summary>Account name that triggered the event.</summary>
    SubjectUserName,
    /// <summary>Domain of the subject account.</summary>
    SubjectDomainName,
    /// <summary>Subject logon session identifier.</summary>
    SubjectLogonId,
    /// <summary>Target account name affected by the event.</summary>
    TargetUserName,
    /// <summary>Domain of the target account.</summary>
    TargetDomainName,
    /// <summary>Security identifier of the target account.</summary>
    TargetSid,
    /// <summary>Source workstation name.</summary>
    WorkstationName,
    /// <summary>Source IP address.</summary>
    IpAddress,
    /// <summary>Source IP port.</summary>
    IpPort,
    /// <summary>Authentication/logon type value.</summary>
    LogonType,
    /// <summary>Primary status value.</summary>
    Status,
    /// <summary>Sub-status value.</summary>
    SubStatus,
    /// <summary>Authentication failure reason.</summary>
    FailureReason,
    /// <summary>Authentication package name.</summary>
    AuthenticationPackageName,
    /// <summary>Logon process name.</summary>
    LogonProcessName,
    /// <summary>LAN Manager package name.</summary>
    LmPackageName,
    /// <summary>Session key length value.</summary>
    KeyLength,
    /// <summary>Originating process identifier.</summary>
    ProcessId,
    /// <summary>Originating process executable name.</summary>
    ProcessName,
    /// <summary>Transmitted services string.</summary>
    TransmittedServices,
    /// <summary>Kerberos ticket options.</summary>
    TicketOptions,
    /// <summary>Kerberos ticket encryption type.</summary>
    TicketEncryptionType,
    /// <summary>Kerberos pre-authentication type.</summary>
    PreAuthType,
    /// <summary>Client IP address key (<c>ClientIP</c>).</summary>
    ClientIp,
    /// <summary>Hardware/MAC address key (<c>HWAddress</c>).</summary>
    HwAddress,
    /// <summary>MAC address key (<c>MacAddress</c>).</summary>
    MacAddress,
    /// <summary>Privilege list key (<c>PrivilegeList</c>).</summary>
    PrivilegeList,
    /// <summary>Fallback unlabeled payload field 0.</summary>
    NoNameA0,
    /// <summary>Fallback unlabeled payload field 1.</summary>
    NoNameA1,
    /// <summary>Raw XML text payload key (<c>#text</c>).</summary>
    TextPayload
}

internal static class KnownEventFieldExtensions {
    internal static string ToEventFieldKey(this KnownEventField field) {
        return field switch {
            KnownEventField.TextPayload => "#text",
            KnownEventField.ClientIp => "ClientIP",
            KnownEventField.HwAddress => "HWAddress",
            _ => field.ToString()
        };
    }
}
