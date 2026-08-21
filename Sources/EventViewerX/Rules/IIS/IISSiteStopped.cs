namespace EventViewerX.Rules.IIS;

/// <summary>
/// IIS site stopped
/// 1005: Website stopped
/// </summary>
public class IISSiteStopped : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1005 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.IISSiteStopped;

    /// <summary>Accepts IIS site stopped events (1005).</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    /// <summary>Machine hosting IIS.</summary>
    public string Computer;
    /// <summary>Site that stopped.</summary>
    public string SiteName;
    /// <summary>User that stopped the site (if present).</summary>
    public string User;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises an IIS site stopped wrapper from an event record.</summary>
    public IISSiteStopped(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "IISSiteStopped";
        Computer = SourceEvent.ComputerName;
        SiteName = SourceEvent.GetValueFromDataDictionary("SiteName", "Name");
        if (string.IsNullOrEmpty(SiteName)) {
            SiteName = SourceEvent.MessageSubject;
        }
        User = SourceEvent.GetValueFromDataDictionary("User", "UserName");
        if (string.IsNullOrEmpty(User)) {
            User = SourceEvent.GetSubjectAccountOrEmpty();
        }
        When = SourceEvent.TimeCreated;
    }
}


