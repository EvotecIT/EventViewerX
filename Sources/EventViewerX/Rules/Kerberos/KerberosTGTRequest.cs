namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Represents a Kerberos TGT request event.
/// </summary>
public class KerberosTGTRequest : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4768 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.KerberosTGTRequest;

    /// <summary>Checks whether the supplied event originates from the security auditing provider.</summary>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing");
    }

    /// <summary>Domain controller that issued the TGT.</summary>
    public string Computer;
    /// <summary>Action reported by the event (e.g., issued, failed).</summary>
    public string Action;
    /// <summary>Target account requesting the ticket.</summary>
    public string AccountName;
    /// <summary>Client IP address (normalized).</summary>
    public string IpAddress;
    /// <summary>Client source port.</summary>
    public string IpPort;
    /// <summary>Human-friendly ticket options.</summary>
    public string TicketOptionsText;
    /// <summary>Status with hex representation.</summary>
    public string StatusText;
    /// <summary>Encryption type used for the ticket.</summary>
    public TicketEncryptionType? EncryptionType;
    /// <summary>Human-friendly ticket encryption type with the raw value retained.</summary>
    public string EncryptionTypeText;
    /// <summary>Pre-authentication type used by the client.</summary>
    public PreAuthType? PreAuthType;
    /// <summary>Human-friendly pre-authentication type with the raw value retained.</summary>
    public string PreAuthTypeText;
    /// <summary>Session key encryption type (from SessionKeyEncryptionType).</summary>
    public TicketEncryptionType? SessionKeyEncryptionType;
    /// <summary>Human-friendly session-key encryption type with the raw value retained.</summary>
    public string SessionKeyEncryptionTypeText;
    /// <summary>Pre-auth encryption type (from PreAuthEncryptionType).</summary>
    public TicketEncryptionType? PreAuthEncryptionType;
    /// <summary>Human-friendly pre-auth encryption type with the raw value retained.</summary>
    public string PreAuthEncryptionTypeText;
    /// <summary>Client-advertised encryption types string.</summary>
    public string ClientAdvertizedEncryptionTypes;
    /// <summary>Supported/available encryption types reported by account/service/DC.</summary>
    public string AccountSupportedEncryptionTypes;
    /// <summary>Parsed account supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? AccountSupportedEncryptionTypesFlags;
    /// <summary>Keys currently available on the account (from event data).</summary>
    public string AccountAvailableKeys;
    /// <summary>Encryption types the service advertises as supported.</summary>
    public string ServiceSupportedEncryptionTypes;
    /// <summary>Parsed service supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? ServiceSupportedEncryptionTypesFlags;
    /// <summary>Keys actually available on the service account.</summary>
    public string ServiceAvailableKeys;
    /// <summary>Encryption types supported by the issuing domain controller.</summary>
    public string DCSupportedEncryptionTypes;
    /// <summary>Parsed domain-controller supported-encryption flags, or null when unavailable.</summary>
    public KerberosSupportedEncryptionTypes? DCSupportedEncryptionTypesFlags;
    /// <summary>Key material available to the domain controller.</summary>
    public string DCAvailableKeys;
    /// <summary>Response ticket hash when present.</summary>
    public string ResponseTicket;
    /// <summary>True when a weak encryption algorithm (e.g., RC4/DES) was used.</summary>
    public bool WeakEncryptionAlgorithm;
    /// <summary>True when the ticket session key uses DES or RC4.</summary>
    public bool WeakSessionKeyEncryptionAlgorithm;
    /// <summary>Time the event was created.</summary>
    public DateTime When;

    /// <summary>Initialises a TGT request wrapper from an event record.</summary>
    public KerberosTGTRequest(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "KerberosTGTRequest";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        AccountName = SourceEvent.GetTargetAccountOrEmpty();
        IpAddress = Rules.RuleHelpers.NormalizeIp(SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress));
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        var rawTicketOptions = SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketOptions);
        var ticketOptions = EventsHelper.GetTicketOptions(rawTicketOptions);
        TicketOptionsText = EventsHelper.DescribeTicketOptions(ticketOptions, rawTicketOptions);

        var rawStatus = SourceEvent.GetDataValueOrEmpty(KnownEventField.Status);
        var status = EventsHelper.GetStatusCode(rawStatus);
        StatusText = EventsHelper.DescribeStatus(status, rawStatus);

        var rawTicketEtype = SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketEncryptionType);
        EncryptionType = EventsHelper.GetTicketEncryptionType(rawTicketEtype);
        EncryptionTypeText = EventsHelper.DescribeEncryption(EncryptionType, rawTicketEtype);

        var rawPreAuth = SourceEvent.GetDataValueOrEmpty(KnownEventField.PreAuthType);
        PreAuthType = EventsHelper.GetPreAuthType(rawPreAuth);
        PreAuthTypeText = EventsHelper.DescribePreAuthType(PreAuthType, rawPreAuth);

        var rawSessionEtype = SourceEvent.GetDataValueOrEmpty("SessionKeyEncryptionType");
        SessionKeyEncryptionType = EventsHelper.GetTicketEncryptionType(rawSessionEtype);
        SessionKeyEncryptionTypeText = EventsHelper.DescribeEncryption(SessionKeyEncryptionType, rawSessionEtype);

        var rawPreAuthEtype = SourceEvent.GetDataValueOrEmpty("PreAuthEncryptionType");
        PreAuthEncryptionType = EventsHelper.GetTicketEncryptionType(rawPreAuthEtype);
        PreAuthEncryptionTypeText = EventsHelper.DescribeEncryption(PreAuthEncryptionType, rawPreAuthEtype);
        ClientAdvertizedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ClientAdvertizedEncryptionTypes");
        AccountSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("AccountSupportedEncryptionTypes");
        AccountSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(AccountSupportedEncryptionTypes);
        AccountAvailableKeys = SourceEvent.GetDataValueOrEmpty("AccountAvailableKeys");
        ServiceSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ServiceSupportedEncryptionTypes");
        ServiceSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(ServiceSupportedEncryptionTypes);
        ServiceAvailableKeys = SourceEvent.GetDataValueOrEmpty("ServiceAvailableKeys");
        DCSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("DCSupportedEncryptionTypes");
        DCSupportedEncryptionTypesFlags = EventsHelper.GetKerberosSupportedEncryptionTypes(DCSupportedEncryptionTypes);
        DCAvailableKeys = SourceEvent.GetDataValueOrEmpty("DCAvailableKeys");
        ResponseTicket = SourceEvent.GetDataValueOrEmpty("ResponseTicket");
        When = SourceEvent.TimeCreated;

        WeakEncryptionAlgorithm = EventsHelper.IsWeakKerberosEncryption(EncryptionType);
        WeakSessionKeyEncryptionAlgorithm = EventsHelper.IsWeakKerberosEncryption(SessionKeyEncryptionType);
    }
}


