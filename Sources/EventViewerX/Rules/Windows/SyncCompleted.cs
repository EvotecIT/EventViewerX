namespace EventViewerX.Rules.Windows;

/// <summary>
/// Sync process ended event
/// 907: Synchronization completed
/// </summary>
public class SyncCompleted : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 907 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.SyncCompleted;

    /// <summary>Accepts sync completion events.</summary>
    public override bool CanHandle(EventObject eventObject) => true;

    /// <summary>Machine that completed synchronization.</summary>
    public string Computer;
    /// <summary>Event timestamp.</summary>
    public DateTime When;
    /// <summary>Parsed key/value statistics from the message body.</summary>
    public Dictionary<string, int> Statistics = new();

    /// <summary>Initialises a sync completion wrapper from an event record.</summary>
    public SyncCompleted(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "SyncCompleted";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        var matches = System.Text.RegularExpressions.Regex.Matches(SourceEvent.Message ?? string.Empty, @"(\w+):\s*(\d+)");
        foreach (System.Text.RegularExpressions.Match match in matches) {
            var key = match.Groups[1].Value;
            if (int.TryParse(match.Groups[2].Value, out var val)) {
                Statistics[key] = val;
            }
        }
    }
}
