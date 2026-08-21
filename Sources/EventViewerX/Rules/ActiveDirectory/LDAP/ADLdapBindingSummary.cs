namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Summarises unsigned/unencrypted LDAP binds on a domain controller (event 2887).
/// The event warns when SASL or simple binds are not using signing or TLS and recommends enforcing rejection.
/// </summary>
public class ADLdapBindingSummary : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 2887 };
    /// <inheritdoc />
    public override string LogName => "Directory Service";
    /// <inheritdoc />
    public override EventType Type => EventType.ADLdapBindingSummary;

    /// <summary>Accepts events based solely on ID/log matching.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }
    /// <summary>Domain controller emitting the warning.</summary>
    public string Computer;
    /// <summary>Short description from the event (e.g., warning text).</summary>
    public string Action;
    /// <summary>Count of simple binds performed without TLS.</summary>
    public int? SimpleBindsWithoutTls;
    /// <summary>Count of SASL/Negotiate binds performed without signing.</summary>
    public int? NegotiateBindsWithoutSigning;
    /// <summary>Task display name from the event metadata.</summary>
    public string TaskDisplayName;
    /// <summary>Level display name from the event metadata.</summary>
    public string LevelDisplayName;
    /// <summary>Timestamp of the event.</summary>
    public DateTime When;

    /// <summary>Initialises the LDAP binding summary wrapper from an event record.</summary>
    public ADLdapBindingSummary(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "ADLdapBindingSummary";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        SimpleBindsWithoutTls = RuleHelpers.GetInt(SourceEvent, "NoNameA0");
        NegotiateBindsWithoutSigning = RuleHelpers.GetInt(SourceEvent, "NoNameA1");
        When = SourceEvent.TimeCreated;
        LevelDisplayName = eventObject.LevelDisplayName;
        TaskDisplayName = eventObject.TaskDisplayName;
    }
}
