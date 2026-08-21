namespace EventViewerX.Rules.Windows;

/// <summary>
/// Client side group policy processing events from the Application log.
/// </summary>
public class ClientGroupPoliciesApplication : ClientGroupPoliciesBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4098 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.ClientGroupPoliciesApplication;

    /// <summary>Initialises a client-side GPO processing wrapper for Application log events.</summary>
    public ClientGroupPoliciesApplication(EventObject eventObject) : base(eventObject) { }
}

