namespace EventViewerX.Rules.SQL;

/// <summary>
/// SQL Server database created
/// 17137: Starting up database
/// 1802: CREATE DATABASE failed; neither historical event proves creation.
/// </summary>
public class SqlDatabaseCreated : EventRuleBase {
    /// <summary>Historical identifiers retained for compatibility; this built-in source is unavailable.</summary>
    public override List<int> EventIds => new() { 17137, 1802 };
    /// <summary>Historical channel retained for compatibility; see the catalog definition's UnavailableReason.</summary>
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.SqlDatabaseCreated;

    /// <summary>Rejects the historical source because it does not establish the advertised event.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return false;
    }

    /// <summary>Server where the database was created.</summary>
    public string Computer;
    /// <summary>Name of the database that was created.</summary>
    public string DatabaseName;
    /// <summary>Timestamp of the creation event.</summary>
    public DateTime When;

    /// <summary>Initialises a SQL database creation wrapper from an event record.</summary>
    public SqlDatabaseCreated(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "SqlDatabaseCreated";
        Computer = SourceEvent.ComputerName;
        DatabaseName = SourceEvent.GetValueFromDataDictionary("Database", "Database Name");
        if (string.IsNullOrEmpty(DatabaseName)) {
            var match = System.Text.RegularExpressions.Regex.Match(SourceEvent.Message ?? string.Empty,
                "database ['\"]?(?<db>[^'\"]+)['\"]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            DatabaseName = match.Success ? match.Groups["db"].Value : string.Empty;
        }
        When = SourceEvent.TimeCreated;
    }
}
