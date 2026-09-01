namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Represents a Kerberos service ticket request event.
/// </summary>
public class KerberosServiceTicket : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4769, 4770 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.KerberosServiceTicket;

    /// <summary>Accepts Kerberos service ticket (TGS) events from the auditing provider.</summary>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing");
    }
    /// <summary>Domain controller processing the TGS request.</summary>
    public string Computer = string.Empty;
    /// <summary>Action from the message subject.</summary>
    public string Action = string.Empty;
    /// <summary>Account requesting the service ticket.</summary>
    public string AccountName = string.Empty;
    /// <summary>Account realm reported by the KDC.</summary>
    public string AccountDomain = string.Empty;
    /// <summary>Logon GUID used to correlate the request with related logon events.</summary>
    public string LogonGuid = string.Empty;
    /// <summary>Raw account supported-encryption field.</summary>
    public string AccountSupportedEncryptionTypes = string.Empty;
    /// <summary>Parsed account supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? AccountSupportedEncryptionTypesFlags;
    /// <summary>Keys available for the requesting account.</summary>
    public string AccountAvailableKeys = string.Empty;
    /// <summary>Service principal targeted by the ticket.</summary>
    public string ServiceName = string.Empty;
    /// <summary>SID of the service account.</summary>
    public string ServiceSid = string.Empty;
    /// <summary>Raw service supported-encryption field.</summary>
    public string ServiceSupportedEncryptionTypes = string.Empty;
    /// <summary>Parsed service supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? ServiceSupportedEncryptionTypesFlags;
    /// <summary>Keys available for the service account.</summary>
    public string ServiceAvailableKeys = string.Empty;
    /// <summary>Raw domain-controller supported-encryption field.</summary>
    public string DCSupportedEncryptionTypes = string.Empty;
    /// <summary>Parsed domain-controller supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? DCSupportedEncryptionTypesFlags;
    /// <summary>Keys available for the issuing domain controller.</summary>
    public string DCAvailableKeys = string.Empty;
    /// <summary>Source IP address.</summary>
    public string IpAddress = string.Empty;
    /// <summary>Source port.</summary>
    public string IpPort = string.Empty;
    /// <summary>Ticket options bitmask.</summary>
    public string TicketOptions = string.Empty;
    /// <summary>Human-friendly ticket options with the raw mask retained.</summary>
    public string TicketOptionsText = string.Empty;
    /// <summary>Status or failure code with the raw value retained.</summary>
    public string StatusText = string.Empty;
    /// <summary>Encryption type used for the ticket.</summary>
    public TicketEncryptionType? EncryptionType;
    /// <summary>Human-friendly ticket encryption type with the raw value retained.</summary>
    public string EncryptionTypeText = string.Empty;
    /// <summary>Session-key encryption type.</summary>
    public TicketEncryptionType? SessionKeyEncryptionType;
    /// <summary>Human-friendly session-key encryption type with the raw value retained.</summary>
    public string SessionKeyEncryptionTypeText = string.Empty;
    /// <summary>Client-advertised encryption types preserved verbatim.</summary>
    public string ClientAdvertizedEncryptionTypes = string.Empty;
    /// <summary>Transited services reported for constrained delegation or referral flows.</summary>
    public string TransmittedServices = string.Empty;
    /// <summary>True when a weak encryption algorithm is present.</summary>
    public bool WeakEncryptionAlgorithm;
    /// <summary>True when the ticket session key uses DES or RC4.</summary>
    public bool WeakSessionKeyEncryptionAlgorithm;
    /// <summary>True when ticket options differ from the common default.</summary>
    public bool UnusualTicketOptions;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a Kerberos service ticket wrapper from an event record.</summary>
    public KerberosServiceTicket(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "KerberosServiceTicket";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        AccountName = SourceEvent.GetTargetAccountOrEmpty();
        AccountDomain = SourceEvent.GetDataValueOrEmpty(KnownEventField.TargetDomainName);
        LogonGuid = SourceEvent.GetDataValueOrEmpty("LogonGuid");
        AccountSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("AccountSupportedEncryptionTypes");
        AccountSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(AccountSupportedEncryptionTypes);
        AccountAvailableKeys = SourceEvent.GetDataValueOrEmpty("AccountAvailableKeys");
        ServiceName = SourceEvent.GetDataValueOrEmpty("ServiceName");
        ServiceSid = SourceEvent.GetDataValueOrEmpty("ServiceSid");
        ServiceSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ServiceSupportedEncryptionTypes");
        ServiceSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(ServiceSupportedEncryptionTypes);
        ServiceAvailableKeys = SourceEvent.GetDataValueOrEmpty("ServiceAvailableKeys");
        DCSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("DCSupportedEncryptionTypes");
        DCSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(DCSupportedEncryptionTypes);
        DCAvailableKeys = SourceEvent.GetDataValueOrEmpty("DCAvailableKeys");
        IpAddress = RuleHelpers.NormalizeIp(SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress));
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        TicketOptions = SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketOptions);
        TicketOptionsText = EventsHelper.DescribeTicketOptions(EventsHelper.GetTicketOptions(TicketOptions), TicketOptions);
        string rawStatus = SourceEvent.GetDataValueOrEmpty(KnownEventField.Status);
        StatusText = EventsHelper.DescribeStatus(EventsHelper.GetStatusCode(rawStatus), rawStatus);
        string rawTicketEncryptionType = SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketEncryptionType);
        EncryptionType = EventsHelper.GetTicketEncryptionType(rawTicketEncryptionType);
        EncryptionTypeText = EventsHelper.DescribeEncryption(EncryptionType, rawTicketEncryptionType);
        string rawSessionKeyEncryptionType = SourceEvent.GetDataValueOrEmpty("SessionKeyEncryptionType");
        SessionKeyEncryptionType = EventsHelper.GetTicketEncryptionType(rawSessionKeyEncryptionType);
        SessionKeyEncryptionTypeText = EventsHelper.DescribeEncryption(
            SessionKeyEncryptionType,
            rawSessionKeyEncryptionType);
        ClientAdvertizedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ClientAdvertizedEncryptionTypes");
        TransmittedServices = SourceEvent.GetDataValueOrEmpty(KnownEventField.TransmittedServices);
        When = SourceEvent.TimeCreated;

        WeakEncryptionAlgorithm = EventsHelper.IsWeakKerberosEncryption(EncryptionType);
        WeakSessionKeyEncryptionAlgorithm = EventsHelper.IsWeakKerberosEncryption(SessionKeyEncryptionType);

        UnusualTicketOptions = !(TicketOptions?.Equals("0x40810010", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}


