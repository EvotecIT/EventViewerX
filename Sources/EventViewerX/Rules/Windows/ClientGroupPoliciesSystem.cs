namespace EventViewerX.Rules.Windows;

/// <summary>
/// Client side group policy processing events from the System log.
/// </summary>
public class ClientGroupPoliciesSystem : ClientGroupPoliciesBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1085 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.ClientGroupPoliciesSystem;

    /// <summary>Initialises a client-side GPO processing wrapper for System log events.</summary>
    public ClientGroupPoliciesSystem(EventObject eventObject) : base(eventObject) { }
}

