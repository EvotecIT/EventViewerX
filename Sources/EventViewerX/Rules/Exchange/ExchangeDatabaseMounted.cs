namespace EventViewerX.Rules.Exchange;

/// <summary>
/// Exchange mailbox database mounted successfully
/// 9526: The Microsoft Exchange Information Store service successfully mounted the database
/// </summary>
public class ExchangeDatabaseMounted : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 9526 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.ExchangeDatabaseMounted;

    /// <summary>Accepts Exchange Information Store database mount success events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    /// <summary>Exchange server name.</summary>
    public string Computer;
    /// <summary>Mounted mailbox database name.</summary>
    public string MailboxDatabase;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises an Exchange DB mounted wrapper from an event record.</summary>
    public ExchangeDatabaseMounted(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ExchangeDatabaseMounted";
        Computer = SourceEvent.ComputerName;
        MailboxDatabase = SourceEvent.GetValueFromDataDictionary("Database", "Mailbox Database");
        if (string.IsNullOrEmpty(MailboxDatabase)) {
            var match = System.Text.RegularExpressions.Regex.Match(SourceEvent.Message ?? string.Empty,
                "database '(?<db>[^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            MailboxDatabase = match.Success ? match.Groups["db"].Value : string.Empty;
        }
        When = SourceEvent.TimeCreated;
    }
}
