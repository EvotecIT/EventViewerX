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
    public override IReadOnlyList<string> ProviderNames => new[] {
        "Group Policy Applications",
        "Group Policy Client",
        "Group Policy Data Sources",
        "Group Policy Device Settings",
        "Group Policy Drive Maps",
        "Group Policy Environment",
        "Group Policy Files",
        "Group Policy Folder Options",
        "Group Policy Folders",
        "Group Policy Ini Files",
        "Group Policy Internet Settings",
        "Group Policy Local Users and Groups",
        "Group Policy Mail Profiles",
        "Group Policy Network Options",
        "Group Policy Network Shares",
        "Group Policy Power Options",
        "Group Policy Printers",
        "Group Policy Regional Options",
        "Group Policy Registry",
        "Group Policy Scheduled Tasks",
        "Group Policy Services",
        "Group Policy Shortcuts",
        "Group Policy Standard Edition",
        "Group Policy Start Menu Settings"
    };
    /// <inheritdoc />
    public override EventType Type => EventType.ClientGroupPoliciesApplication;

    /// <inheritdoc />
    public override bool CanHandle(EventObject eventObject) => RuleHelpers.IsProvider(eventObject, ProviderNames.ToArray());

    /// <summary>Initialises a client-side GPO processing wrapper for Application log events.</summary>
    public ClientGroupPoliciesApplication(EventObject eventObject) : base(eventObject) { }
}

