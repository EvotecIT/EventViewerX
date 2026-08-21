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
    /// <summary>Service principal targeted by the ticket.</summary>
    public string ServiceName = string.Empty;
    /// <summary>Source IP address.</summary>
    public string IpAddress = string.Empty;
    /// <summary>Source port.</summary>
    public string IpPort = string.Empty;
    /// <summary>Ticket options bitmask.</summary>
    public string TicketOptions = string.Empty;
    /// <summary>Encryption type used for the ticket.</summary>
    public TicketEncryptionType? EncryptionType;
    /// <summary>True when a weak encryption algorithm is present.</summary>
    public bool WeakEncryptionAlgorithm;
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
        ServiceName = SourceEvent.GetDataValueOrEmpty("ServiceName");
        IpAddress = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress);
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        TicketOptions = SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketOptions);
        EncryptionType = EventsHelper.GetTicketEncryptionType(SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketEncryptionType));
        When = SourceEvent.TimeCreated;

        WeakEncryptionAlgorithm = EncryptionType is TicketEncryptionType.DES_CBC_CRC
            or TicketEncryptionType.DES_CBC_MD5
            or TicketEncryptionType.RC4_HMAC
            or TicketEncryptionType.RC4_HMAC_EXP;

        UnusualTicketOptions = !(TicketOptions?.Equals("0x40810010", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}


