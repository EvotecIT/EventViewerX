namespace EventViewerX.Rules.Kerberos;

/// <summary>
/// Represents a failed Kerberos ticket request event.
/// </summary>
public class KerberosTicketFailure : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4771, 4772 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.KerberosTicketFailure;

    /// <summary>Checks whether the supplied event is a Kerberos ticket failure from the security auditing provider.</summary>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing");
    }
    /// <summary>Domain controller processing the ticket.</summary>
    public string Computer;
    /// <summary>Action reported in the event (e.g., failure reason).</summary>
    public string Action;
    /// <summary>Account associated with the failed ticket request.</summary>
    public string AccountName;
    /// <summary>Status or failure code returned by the KDC.</summary>
    public string FailureCode;
    /// <summary>Source IP address.</summary>
    public string IpAddress;
    /// <summary>Source port.</summary>
    public string IpPort;
    /// <summary>Encryption type requested/used.</summary>
    public TicketEncryptionType? EncryptionType;
    /// <summary>True when a weak encryption algorithm was involved.</summary>
    public bool WeakEncryptionAlgorithm;
    /// <summary>Time the event was created.</summary>
    public DateTime When;

    /// <summary>Initialises a Kerberos ticket failure wrapper from an event record.</summary>
    public KerberosTicketFailure(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "KerberosTicketFailure";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        AccountName = SourceEvent.GetTargetAccountOrEmpty();
        FailureCode = SourceEvent.GetDataValueOrEmpty(KnownEventField.Status);
        IpAddress = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress);
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        EncryptionType = EventsHelper.GetTicketEncryptionType(SourceEvent.GetDataValueOrEmpty(KnownEventField.TicketEncryptionType));
        When = SourceEvent.TimeCreated;

        WeakEncryptionAlgorithm = EventsHelper.IsWeakKerberosEncryption(EncryptionType);
    }
}



