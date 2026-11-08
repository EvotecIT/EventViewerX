namespace EventViewerX.Rules.DHCP;

/// <summary>
/// DHCP lease creation event
/// DHCP text audit code 10 is not a Windows Event Log identifier.
/// </summary>
public class DhcpLeaseCreated : EventRuleBase {
    /// <summary>Historical identifiers retained for compatibility; this built-in source is unavailable.</summary>
    public override List<int> EventIds => new() { 10 };
    /// <summary>Historical channel retained for compatibility; see the catalog definition's UnavailableReason.</summary>
    public override string LogName => "Microsoft-Windows-DHCP Server/Operational";
    /// <inheritdoc />
    public override EventType Type => EventType.DhcpLeaseCreated;

    /// <summary>Rejects the historical source because it does not establish the advertised event.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return false;
    }

    /// <summary>
    /// Computer that issued the lease.
    /// </summary>
    public string Computer;

    /// <summary>
    /// IP address leased to the client.
    /// </summary>
    public string IPAddress;

    /// <summary>
    /// MAC address of the client.
    /// </summary>
    public string MacAddress;

    /// <summary>
    /// Time when the lease was created.
    /// </summary>
    public DateTime When;

    /// <summary>Initialises a DHCP lease creation wrapper from an event record.</summary>
    public DhcpLeaseCreated(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "DhcpLeaseCreated";
        Computer = SourceEvent.ComputerName;
        IPAddress = SourceEvent.GetValueFromDataDictionary(KnownEventField.IpAddress, KnownEventField.ClientIp);
        MacAddress = SourceEvent.GetValueFromDataDictionary(KnownEventField.HwAddress, KnownEventField.MacAddress);
        When = SourceEvent.TimeCreated;
    }
}
