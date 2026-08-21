namespace EventViewerX.Rules.Windows;

/// <summary>
/// Network adapter entered promiscuous mode
/// 104: NDIS indicates adapter promiscuous mode
/// </summary>
public class NetworkPromiscuousMode : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 10400, 10401 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.NetworkPromiscuousMode;

    /// <summary>Accepts NDIS provider events signalling promiscuous mode toggles.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return eventObject.ProviderName.Equals("Ndis", StringComparison.OrdinalIgnoreCase) &&
               (eventObject.Id == 10400 || eventObject.Id == 10401);
    }

    /// <summary>Machine whose adapter changed mode.</summary>
    public string Computer;
    /// <summary>Adapter name.</summary>
    public string Adapter;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises a promiscuous-mode event wrapper from an event record.</summary>
    public NetworkPromiscuousMode(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "NetworkPromiscuousMode";
        Computer = SourceEvent.ComputerName;
        Adapter = SourceEvent.GetValueFromDataDictionary("AdapterName", "Name");
        When = SourceEvent.TimeCreated;
    }
}
