namespace EventViewerX;

/// <summary>Resolves built-in readiness scenarios to the canonical typed-event catalog.</summary>
public static class EventReadinessScenarioCatalog {
    /// <summary>Returns the event types selected by a built-in scenario.</summary>
    public static IReadOnlyList<EventType> GetTypes(EventReadinessScenario scenario) => scenario switch {
        EventReadinessScenario.DailyActiveDirectoryReport => new[] {
            EventType.ActiveDirectoryChanges,
            EventType.ActiveDirectoryAuthentication
        },
        EventReadinessScenario.AccountLockoutMonitoring => new[] {
            EventType.ADUserLockouts,
            EventType.ADUserUnlocked,
            EventType.ADUserLogonFailed
        },
        EventReadinessScenario.GroupPolicyMonitoring => new[] {
            EventType.GroupPolicyActivity
        },
        EventReadinessScenario.AuthenticationMonitoring => new[] {
            EventType.AuthenticationHealth
        },
        EventReadinessScenario.SecurityMonitoring => new[] {
            EventType.ScheduledTaskActivity,
            EventType.FirewallRuleActivity,
            EventType.DefenderSecurity,
            EventType.AuthenticationHealth
        },
        EventReadinessScenario.None => throw new ArgumentException("Scenario None does not select event types.", nameof(scenario)),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };
}
