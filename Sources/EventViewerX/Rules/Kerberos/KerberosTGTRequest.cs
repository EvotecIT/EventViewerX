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
    public string EncryptionTypeText;
    /// <summary>Pre-authentication type used by the client.</summary>
    public string PreAuthTypeText;
    /// <summary>Session key encryption type (from SessionKeyEncryptionType).</summary>
    public string SessionKeyEncryptionTypeText;
    /// <summary>Pre-auth encryption type (from PreAuthEncryptionType).</summary>
    public string PreAuthEncryptionTypeText;
    /// <summary>Client-advertised encryption types string.</summary>
    public string ClientAdvertizedEncryptionTypes;
    /// <summary>Supported/available encryption types reported by account/service/DC.</summary>
    public string AccountSupportedEncryptionTypes;
    /// <summary>Keys currently available on the account (from event data).</summary>
    public string AccountAvailableKeys;
    /// <summary>Encryption types the service advertises as supported.</summary>
    public string ServiceSupportedEncryptionTypes;
    /// <summary>Keys actually available on the service account.</summary>
    public string ServiceAvailableKeys;
    /// <summary>Encryption types supported by the issuing domain controller.</summary>
    public string DCSupportedEncryptionTypes;
    /// <summary>Key material available to the domain controller.</summary>
    public string DCAvailableKeys;
    /// <summary>Response ticket hash when present.</summary>
    public string ResponseTicket;
    /// <summary>True when a weak encryption algorithm (e.g., RC4/DES) was used.</summary>
    public bool WeakEncryptionAlgorithm;
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
        var encryptionType = EventsHelper.GetTicketEncryptionType(rawTicketEtype);
        EncryptionTypeText = EventsHelper.DescribeEncryption(encryptionType, rawTicketEtype);

        var rawPreAuth = SourceEvent.GetDataValueOrEmpty(KnownEventField.PreAuthType);
        var preAuthType = EventsHelper.GetPreAuthType(rawPreAuth);
        PreAuthTypeText = EventsHelper.DescribePreAuthType(preAuthType, rawPreAuth);

        var rawSessionEtype = SourceEvent.GetDataValueOrEmpty("SessionKeyEncryptionType");
        var sessionKeyEncryptionType = EventsHelper.GetTicketEncryptionType(rawSessionEtype);
        SessionKeyEncryptionTypeText = EventsHelper.DescribeEncryption(sessionKeyEncryptionType, rawSessionEtype);

        var rawPreAuthEtype = SourceEvent.GetDataValueOrEmpty("PreAuthEncryptionType");
        var preAuthEncryptionType = EventsHelper.GetTicketEncryptionType(rawPreAuthEtype);
        PreAuthEncryptionTypeText = EventsHelper.DescribeEncryption(preAuthEncryptionType, rawPreAuthEtype);
        ClientAdvertizedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ClientAdvertizedEncryptionTypes");
        AccountSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("AccountSupportedEncryptionTypes");
        AccountAvailableKeys = SourceEvent.GetDataValueOrEmpty("AccountAvailableKeys");
        ServiceSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("ServiceSupportedEncryptionTypes");
        ServiceAvailableKeys = SourceEvent.GetDataValueOrEmpty("ServiceAvailableKeys");
        DCSupportedEncryptionTypes = SourceEvent.GetDataValueOrEmpty("DCSupportedEncryptionTypes");
        DCAvailableKeys = SourceEvent.GetDataValueOrEmpty("DCAvailableKeys");
        ResponseTicket = SourceEvent.GetDataValueOrEmpty("ResponseTicket");
        When = SourceEvent.TimeCreated;

        WeakEncryptionAlgorithm = encryptionType is TicketEncryptionType.DES_CBC_CRC
            or TicketEncryptionType.DES_CBC_MD5
            or TicketEncryptionType.RC4_HMAC
            or TicketEncryptionType.RC4_HMAC_EXP;
    }
}


