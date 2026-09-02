namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Represents KDCsvc events 201-209 that report RC4 dependencies, explicit
/// insecure domain defaults, and requests blocked by Kerberos enforcement.
/// </summary>
public sealed class KerberosKdcRc4Audit : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 201, 202, 203, 204, 205, 206, 207, 208, 209 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.KerberosKdcRc4Audit;

    /// <summary>Accepts the documented KDCsvc RC4 audit and enforcement events.</summary>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Kdcsvc");
    }

    /// <summary>Domain controller that emitted the event.</summary>
    public string Computer = string.Empty;
    /// <summary>Provider-rendered event summary.</summary>
    public string Action = string.Empty;
    /// <summary>Event-local reason for the RC4 warning or enforcement block.</summary>
    public KerberosKdcRc4Issue Issue;
    /// <summary>Whether the event is an audit warning, enforcement block, or explicit-configuration warning.</summary>
    public KerberosKdcRc4Disposition Disposition;
    /// <summary>Cipher name reported by the KDC when structured data supplies it.</summary>
    public string CipherName = string.Empty;
    /// <summary>Explicit insecure ciphers reported by event 205.</summary>
    public string EnabledInsecureCiphers = string.Empty;
    /// <summary>Requesting account name.</summary>
    public string AccountName = string.Empty;
    /// <summary>Realm supplied with the request.</summary>
    public string SuppliedRealmName = string.Empty;
    /// <summary>Raw account supported-encryption field.</summary>
    public string AccountSupportedEncryptionTypesRaw = string.Empty;
    /// <summary>Parsed account supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? AccountSupportedEncryptionTypes;
    /// <summary>Keys available for the requesting account.</summary>
    public string AccountAvailableKeys = string.Empty;
    /// <summary>Target service name.</summary>
    public string ServiceName = string.Empty;
    /// <summary>Target service SID.</summary>
    public string ServiceSid = string.Empty;
    /// <summary>Raw service supported-encryption field.</summary>
    public string ServiceSupportedEncryptionTypesRaw = string.Empty;
    /// <summary>Parsed service supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? ServiceSupportedEncryptionTypes;
    /// <summary>Keys available for the service account.</summary>
    public string ServiceAvailableKeys = string.Empty;
    /// <summary>Raw domain-controller supported-encryption field.</summary>
    public string DomainControllerSupportedEncryptionTypesRaw = string.Empty;
    /// <summary>Parsed domain-controller supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? DomainControllerSupportedEncryptionTypes;
    /// <summary>Raw DefaultDomainSupportedEncTypes value observed by this domain controller.</summary>
    public string DefaultDomainSupportedEncTypesRaw = string.Empty;
    /// <summary>Parsed DefaultDomainSupportedEncTypes flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? DefaultDomainSupportedEncTypes;
    /// <summary>Keys available for the emitting domain controller.</summary>
    public string DomainControllerAvailableKeys = string.Empty;
    /// <summary>Normalized client address.</summary>
    public string ClientAddress = string.Empty;
    /// <summary>Client source port.</summary>
    public string ClientPort = string.Empty;
    /// <summary>Encryption types advertised by the client, preserved verbatim.</summary>
    public string ClientAdvertizedEncryptionTypes = string.Empty;
    /// <summary>Time the event was created.</summary>
    public DateTime When;

    /// <summary>Initializes a KDCsvc RC4 audit or enforcement projection.</summary>
    public KerberosKdcRc4Audit(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = nameof(KerberosKdcRc4Audit);
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        (Issue, Disposition) = Classify(SourceEvent.Id);

        KerberosKdcPayloadFields payload = KerberosKdcPayloadFields.Parse(SourceEvent);
        KerberosKdcMessageFields message = KerberosKdcMessageFields.Parse(SourceEvent.Message);
        CipherName = First(ReadData("CipherName", "Cipher", "Cipher Name"), payload.CipherName);
        EnabledInsecureCiphers = First(
            ReadData("EnabledInsecureCiphers", "Enabled Insecure Ciphers", "Ciphers"),
            payload.EnabledInsecureCiphers,
            message.TopLevel("Cipher(s)", "Enabled Insecure Ciphers"));

        AccountName = First(
            ReadData("AccountName", "TargetUserName"),
            payload.AccountName,
            message.Account("Account Name"));
        SuppliedRealmName = First(
            ReadData("SuppliedRealmName", "TargetDomainName"),
            payload.SuppliedRealmName,
            message.Account("Supplied Realm Name"));
        AccountSupportedEncryptionTypesRaw = First(
            ReadData("AccountSupportedEncryptionTypes", "SupportedEncryptionTypes"),
            payload.AccountSupportedEncryptionTypes,
            message.Account("msds-SupportedEncryptionTypes", "MSDS-SupportedEncryptionTypes"));
        AccountSupportedEncryptionTypes = EventsHelper.GetKerberosSupportedEncryptionTypes(AccountSupportedEncryptionTypesRaw);
        AccountAvailableKeys = First(
            ReadData("AccountAvailableKeys"),
            payload.AccountAvailableKeys,
            message.Account("Available Keys"));

        ServiceName = First(ReadData("ServiceName"), payload.ServiceName, message.Service("Service Name"));
        ServiceSid = First(
            ReadData("ServiceSid", "ServiceID"),
            payload.ServiceSid,
            message.Service("Service ID", "Service SID"));
        ServiceSupportedEncryptionTypesRaw = First(
            ReadData("ServiceSupportedEncryptionTypes"),
            payload.ServiceSupportedEncryptionTypes,
            message.Service("msds-SupportedEncryptionTypes", "MSDS-SupportedEncryptionTypes"));
        ServiceSupportedEncryptionTypes = EventsHelper.GetKerberosSupportedEncryptionTypes(ServiceSupportedEncryptionTypesRaw);
        ServiceAvailableKeys = First(
            ReadData("ServiceAvailableKeys"),
            payload.ServiceAvailableKeys,
            message.Service("Available Keys"));

        DomainControllerSupportedEncryptionTypesRaw = First(
            ReadData("DCSupportedEncryptionTypes", "DomainControllerSupportedEncryptionTypes"),
            payload.DomainControllerSupportedEncryptionTypes,
            message.DomainController("msds-SupportedEncryptionTypes", "MSDS-SupportedEncryptionTypes"));
        DomainControllerSupportedEncryptionTypes = EventsHelper.GetKerberosSupportedEncryptionTypes(
            DomainControllerSupportedEncryptionTypesRaw);
        DefaultDomainSupportedEncTypesRaw = First(
            ReadData("DefaultDomainSupportedEncTypes"),
            payload.DefaultDomainSupportedEncTypes,
            message.DomainController("DefaultDomainSupportedEncTypes"),
            message.TopLevel("DefaultDomainSupportedEncTypes"));
        DefaultDomainSupportedEncTypes = EventsHelper.GetKerberosSupportedEncryptionTypes(
            DefaultDomainSupportedEncTypesRaw);
        DomainControllerAvailableKeys = First(
            ReadData("DCAvailableKeys", "DomainControllerAvailableKeys"),
            payload.DomainControllerAvailableKeys,
            message.DomainController("Available Keys"));

        ClientAddress = RuleHelpers.NormalizeIp(First(
            ReadData("ClientAddress", "IpAddress"),
            payload.ClientAddress,
            message.Network("Client Address")));
        ClientPort = First(
            ReadData("ClientPort", "IpPort"),
            payload.ClientPort,
            message.Network("Client Port"));
        ClientAdvertizedEncryptionTypes = First(
            ReadData("ClientAdvertizedEncryptionTypes", "AdvertizedEtypes"),
            payload.ClientAdvertizedEncryptionTypes,
            message.Network("Advertized Etypes", "Advertised Etypes"));
        When = SourceEvent.TimeCreated;
    }

    private string ReadData(params string[] keys)
    {
        foreach (string key in keys) {
            string value = SourceEvent.GetDataValueOrEmpty(key);
            if (!string.IsNullOrWhiteSpace(value)) {
                return value;
            }
        }
        return string.Empty;
    }

    private static string First(params string[] values)
    {
        foreach (string value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value.Trim();
            }
        }
        return string.Empty;
    }

    private static (KerberosKdcRc4Issue Issue, KerberosKdcRc4Disposition Disposition) Classify(int eventId)
    {
        return eventId switch {
            201 => (KerberosKdcRc4Issue.ClientOnlySupportsInsecureEncryption, KerberosKdcRc4Disposition.AuditWarning),
            202 => (KerberosKdcRc4Issue.ServiceOnlyHasInsecureKeys, KerberosKdcRc4Disposition.AuditWarning),
            203 => (KerberosKdcRc4Issue.ClientOnlySupportsInsecureEncryption, KerberosKdcRc4Disposition.EnforcementBlock),
            204 => (KerberosKdcRc4Issue.ServiceOnlyHasInsecureKeys, KerberosKdcRc4Disposition.EnforcementBlock),
            205 => (KerberosKdcRc4Issue.ExplicitInsecureDomainDefault, KerberosKdcRc4Disposition.ExplicitConfigurationWarning),
            206 => (KerberosKdcRc4Issue.ClientDoesNotSupportAes, KerberosKdcRc4Disposition.AuditWarning),
            207 => (KerberosKdcRc4Issue.ServiceMissingAesKeys, KerberosKdcRc4Disposition.AuditWarning),
            208 => (KerberosKdcRc4Issue.ClientDoesNotSupportAes, KerberosKdcRc4Disposition.EnforcementBlock),
            209 => (KerberosKdcRc4Issue.ServiceMissingAesKeys, KerberosKdcRc4Disposition.EnforcementBlock),
            _ => (KerberosKdcRc4Issue.Unknown, KerberosKdcRc4Disposition.Unknown)
        };
    }
}
