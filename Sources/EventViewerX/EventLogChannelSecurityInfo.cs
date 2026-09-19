namespace EventViewerX;

/// <summary>
/// Read-only interpretation of a channel security descriptor. Access rules are raw DACL entries,
/// not a calculation of effective access for a particular Windows token.
/// </summary>
public sealed class EventLogChannelSecurityInfo {
    private EventLogChannelSecurityInfo(
        string? securityDescriptor,
        SecurityDescriptorDaclState daclState,
        IReadOnlyList<EventLogAccessRule>? accessRules,
        string? diagnostic) {
        SecurityDescriptor = securityDescriptor;
        DaclState = daclState;
        AccessRules = accessRules;
        Diagnostic = diagnostic;
    }

    /// <summary>Original SDDL, retained even if its DACL cannot be interpreted.</summary>
    public string? SecurityDescriptor { get; }

    /// <summary>Presence and shape of the DACL; unavailable does not mean empty.</summary>
    public SecurityDescriptorDaclState DaclState { get; }

    /// <summary>Ordered DACL entries, or null when the descriptor is unavailable or unparseable.</summary>
    public IReadOnlyList<EventLogAccessRule>? AccessRules { get; }

    /// <summary>Reason the DACL is unavailable, when known.</summary>
    public string? Diagnostic { get; }

    /// <summary>Parses channel SDDL without changing Windows configuration.</summary>
    public static EventLogChannelSecurityInfo Inspect(string? securityDescriptor) {
        if (string.IsNullOrWhiteSpace(securityDescriptor)) {
            return new EventLogChannelSecurityInfo(
                securityDescriptor,
                SecurityDescriptorDaclState.Unavailable,
                null,
                "The channel security descriptor is unavailable.");
        }

        try {
            EventLogParsedSecurityDescriptor parsed = EventLogSecurityDescriptor.Parse(securityDescriptor!);
            return new EventLogChannelSecurityInfo(securityDescriptor, parsed.DaclState, parsed.AccessRules, null);
        } catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException or FormatException) {
            return new EventLogChannelSecurityInfo(
                securityDescriptor,
                SecurityDescriptorDaclState.Unavailable,
                null,
                exception.Message);
        }
    }
}
