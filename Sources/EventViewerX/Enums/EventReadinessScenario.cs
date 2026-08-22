namespace EventViewerX;

/// <summary>Built-in convenience scenario that resolves to typed event requirements.</summary>
public enum EventReadinessScenario {
    /// <summary>No scenario; explicit event types are required.</summary>
    None,
    /// <summary>Daily account, directory, Group Policy, and audit-policy change reporting.</summary>
    DailyActiveDirectoryReport,
    /// <summary>Account lockout collection and reporting.</summary>
    AccountLockoutMonitoring,
    /// <summary>Group Policy change collection and reporting.</summary>
    GroupPolicyMonitoring,
    /// <summary>Logon, NTLMv1, Kerberos, and LDAP authentication monitoring.</summary>
    AuthenticationMonitoring
}
