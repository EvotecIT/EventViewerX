namespace EventViewerX.Rules.DHCP;

/// <summary>
/// DHCP lease creation event
/// 10: A new IP address was leased to a client
/// </summary>
public class DhcpLeaseCreated : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 10 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-DHCP Server/Operational";
    /// <inheritdoc />
    public override EventType Type => EventType.DhcpLeaseCreated;

    /// <summary>Accepts DHCP lease events from the DHCP Server provider.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-DHCP-Server");
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
