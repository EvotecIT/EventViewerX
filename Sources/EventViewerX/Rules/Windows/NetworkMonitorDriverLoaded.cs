namespace EventViewerX.Rules.Windows;

/// <summary>
/// Network monitor driver loaded
/// 6: Filter Manager driver loaded
/// </summary>
public class NetworkMonitorDriverLoaded : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 6, 7035, 7045 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.NetworkMonitorDriverLoaded;

    private static readonly string[] _driverNames = ["npcap", "npf", "netmon"];

    /// <summary>Checks for netmon/Npcap driver load or service events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider and IDs match supported driver events.</returns>
    public override bool CanHandle(EventObject eventObject) {
        if (eventObject.ProviderName.Equals("Service Control Manager", StringComparison.OrdinalIgnoreCase) &&
            (eventObject.Id == 7035 || eventObject.Id == 7045)) {
            var msg = eventObject.MessageSubject ?? string.Empty;
            return _driverNames.Any(name => msg.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (eventObject.ProviderName.Equals("Microsoft-Windows-FilterManager", StringComparison.OrdinalIgnoreCase) &&
            eventObject.Id == 6) {
            var drv = eventObject.GetValueFromDataDictionary("DriverName", "FilterName", "ImageName") ?? string.Empty;
            return _driverNames.Any(name => drv.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return false;
    }

    /// <summary>Machine where the driver was loaded or service started.</summary>
    public string Computer;
    /// <summary>Detected driver/service name (npcap/npf/netmon).</summary>
    public string DriverName;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a network monitor driver detection wrapper from an event record.</summary>
    public NetworkMonitorDriverLoaded(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "NetworkMonitorDriverLoaded";
        Computer = SourceEvent.ComputerName;
        DriverName = SourceEvent.GetValueFromDataDictionary("DriverName", "FilterName", "ImageName") ??
                     _driverNames.FirstOrDefault(n => (SourceEvent.MessageSubject ?? string.Empty)
                         .IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) ?? string.Empty;
        When = SourceEvent.TimeCreated;
    }
}
