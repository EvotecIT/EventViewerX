namespace EventViewerX.Rules.Windows;

/// <summary>
/// Network adapter entered promiscuous mode
/// Historical NDIS reset/timestamp identifiers do not establish promiscuous mode.
/// </summary>
public class NetworkPromiscuousMode : EventRuleBase {
    /// <summary>Historical identifiers retained for compatibility; this built-in source is unavailable.</summary>
    public override List<int> EventIds => new() { 10400, 10401 };
    /// <summary>Historical channel retained for compatibility; see the catalog definition's UnavailableReason.</summary>
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.NetworkPromiscuousMode;

    /// <summary>Rejects the historical source because it does not establish the advertised event.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return false;
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
