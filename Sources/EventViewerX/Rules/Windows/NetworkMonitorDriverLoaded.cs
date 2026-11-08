namespace EventViewerX.Rules.Windows;

/// <summary>
/// Packet-monitor driver service installation or control activity.
/// The historical type name is retained; SCM events do not prove a driver load.
/// </summary>
public class NetworkMonitorDriverLoaded : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 7035, 7045 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Service Control Manager" };
    /// <inheritdoc />
    public override EventType Type => EventType.NetworkMonitorDriverLoaded;

    private static readonly string[] _driverNames = ["npcap", "npf", "netmon"];

    /// <summary>Checks for netmon/Npcap service installation or control events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider and IDs match supported driver events.</returns>
    public override bool CanHandle(EventObject eventObject) {
        if (eventObject.ProviderName.Equals("Service Control Manager", StringComparison.OrdinalIgnoreCase) &&
            (eventObject.Id == 7035 || eventObject.Id == 7045)) {
            string service = eventObject.GetDataValueOrEmpty("ServiceName");
            if (service.Length == 0) {
                service = eventObject.GetDataValueOrEmpty("param1");
            }
            string evidence = service.Length > 0 ? service : eventObject.Message;
            return _driverNames.Any(name => evidence.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return false;
    }

    /// <summary>Machine where the driver service activity occurred.</summary>
    public string Computer;
    /// <summary>Detected driver/service name (npcap/npf/netmon).</summary>
    public string DriverName;
    /// <summary>Whether the driver service was installed or received a control request.</summary>
    public string Action;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a network monitor driver detection wrapper from an event record.</summary>
    public NetworkMonitorDriverLoaded(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "NetworkMonitorDriverLoaded";
        Computer = SourceEvent.ComputerName;
        DriverName = Value("ServiceName", "param1");
        Action = SourceEvent.Id == 7045 ? "Driver service installed" : "Driver service control request";
        if (DriverName.Length == 0) {
            DriverName = _driverNames.FirstOrDefault(name => SourceEvent.Message
                .IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) ?? string.Empty;
        }
        When = SourceEvent.TimeCreated;
    }
}
