namespace EventViewerX;

/// <summary>Built-in monitoring selections that combine event types with exact semantic filters.</summary>
public enum EventMonitoringPreset {
    /// <summary>NTLMv1 logons, RC4/DES Kerberos tickets, and unsigned or cleartext LDAP binds.</summary>
    AuthenticationHealth,
    /// <summary>Scheduled-task lifecycle events 4698 through 4702.</summary>
    ScheduledTaskActivity,
    /// <summary>Windows Firewall rule lifecycle events 4946 through 4948.</summary>
    FirewallRuleActivity,
    /// <summary>Microsoft Defender events 1116, 1117, and 5007.</summary>
    DefenderSecurity
}

/// <summary>Resolved event types and optional exact predicate for a built-in monitoring preset.</summary>
public sealed class EventMonitoringPresetDefinition {
    internal EventMonitoringPresetDefinition(
        EventMonitoringPreset preset,
        IReadOnlyList<EventType> types,
        EventPredicate? predicate) {
        Preset = preset;
        Types = types;
        Predicate = predicate;
    }
    /// <summary>Preset identity.</summary>
    public EventMonitoringPreset Preset { get; }
    /// <summary>Canonical event types queried by the preset.</summary>
    public IReadOnlyList<EventType> Types { get; }
    /// <summary>Exact semantic filter, or null when the selected leaf types are already sufficient.</summary>
    public EventPredicate? Predicate { get; }
}

/// <summary>Resolves built-in security monitoring presets.</summary>
public static class EventMonitoringPresetCatalog {
    /// <summary>Returns a detached preset definition.</summary>
    public static EventMonitoringPresetDefinition Get(EventMonitoringPreset preset) => preset switch {
        EventMonitoringPreset.AuthenticationHealth => new EventMonitoringPresetDefinition(
            preset,
            new[] { EventType.AuthenticationHealth },
            EventPredicate.AnyOf(
                TypeIs(nameof(EventType.ADUserLogonNTLMv1)),
                EventPredicate.AllOf(
                    EventPredicate.Compare(
                        "TypeName",
                        EventPredicateOperator.In,
                        nameof(EventType.KerberosTGTRequest),
                        nameof(EventType.KerberosServiceTicket)),
                    EventPredicate.Compare("WeakEncryptionAlgorithm", EventPredicateOperator.Equal, true)),
                TypeIs(nameof(EventType.ADLdapBindingDetails)),
                EventPredicate.AllOf(
                    TypeIs(nameof(EventType.ADLdapBindingSummary)),
                    EventPredicate.AnyOf(
                        EventPredicate.Compare("SimpleBindsWithoutTls", EventPredicateOperator.GreaterThan, 0),
                        EventPredicate.Compare("NegotiateBindsWithoutSigning", EventPredicateOperator.GreaterThan, 0))))),
        EventMonitoringPreset.ScheduledTaskActivity => Simple(preset, EventType.ScheduledTaskActivity),
        EventMonitoringPreset.FirewallRuleActivity => Simple(preset, EventType.FirewallRuleActivity),
        EventMonitoringPreset.DefenderSecurity => Simple(preset, EventType.DefenderSecurity),
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    private static EventMonitoringPresetDefinition Simple(EventMonitoringPreset preset, EventType type) =>
        new(preset, new[] { type }, predicate: null);

    private static EventPredicate TypeIs(string name) => EventPredicate.Compare(
        "TypeName",
        EventPredicateOperator.Equal,
        name);
}
