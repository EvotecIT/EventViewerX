namespace EventViewerX;

/// <summary>Reason reported by a KDCsvc RC4 audit or enforcement event.</summary>
public enum KerberosKdcRc4Issue
{
    /// <summary>The event ID is not one of the documented KDCsvc 201-209 events.</summary>
    Unknown,
    /// <summary>The service has no explicit encryption mask and the client advertises only insecure ciphers.</summary>
    ClientOnlySupportsInsecureEncryption,
    /// <summary>The service has no explicit encryption mask and has only insecure keys.</summary>
    ServiceOnlyHasInsecureKeys,
    /// <summary>The domain default explicitly enables a cipher other than AES-SHA1.</summary>
    ExplicitInsecureDomainDefault,
    /// <summary>An AES-only service or domain default cannot be used because the client does not advertise AES-SHA1.</summary>
    ClientDoesNotSupportAes,
    /// <summary>An AES-only service or domain default cannot be used because the service has no AES-SHA1 keys.</summary>
    ServiceMissingAesKeys
}

/// <summary>Effect represented by a KDCsvc RC4 event.</summary>
public enum KerberosKdcRc4Disposition
{
    /// <summary>The event ID is not one of the documented KDCsvc 201-209 events.</summary>
    Unknown,
    /// <summary>The request currently succeeds but the dependency is reported for remediation.</summary>
    AuditWarning,
    /// <summary>The KDC denied the request under enforcement behavior.</summary>
    EnforcementBlock,
    /// <summary>The KDC detected an explicit insecure domain-default configuration.</summary>
    ExplicitConfigurationWarning
}
