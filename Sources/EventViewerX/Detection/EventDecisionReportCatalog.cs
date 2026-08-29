namespace EventViewerX;

/// <summary>Catalog of built-in decision-oriented report profiles.</summary>
public static class EventDecisionReportCatalog {
    private static readonly IReadOnlyDictionary<EventDecisionReportKind, EventDecisionReportDefinition> Definitions =
        CreateDefinitions().ToDictionary(static definition => definition.Kind);

    /// <summary>Returns every built-in report profile in stable enum order.</summary>
    public static IReadOnlyList<EventDecisionReportDefinition> GetDefinitions() => Enum
        .GetValues(typeof(EventDecisionReportKind))
        .Cast<EventDecisionReportKind>()
        .Select(GetDefinition)
        .ToArray();

    /// <summary>Returns one report profile.</summary>
    public static EventDecisionReportDefinition GetDefinition(EventDecisionReportKind kind) =>
        Definitions.TryGetValue(kind, out EventDecisionReportDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(kind));

    private static IEnumerable<EventDecisionReportDefinition> CreateDefinitions() {
        yield return Definition(
            EventDecisionReportKind.CollectionCoverage,
            "Event collection coverage and cost",
            "Shows represented hosts, channels, volume, lag, failures, limits, and storage ownership.",
            Array.Empty<EventType>(),
            Array.Empty<string>(),
            new[] { "collection-gap", "eventing-integrity" },
            allObservations: true,
            allFindings: false,
            allPacks: true);
        yield return Definition(
            EventDecisionReportKind.EventingIntegrity,
            "Eventing integrity",
            "Highlights audit changes, cleared or full logs, time changes, source gaps, and delivery failures.",
            new[] {
                EventType.LogsClearedSecurity, EventType.LogsClearedOther, EventType.LogsFullSecurity,
                EventType.AuditPolicyChange, EventType.OSCrashOnAuditFailRecovery, EventType.OSTimeChange
            },
            new[] { "eventviewerx.eventing-integrity" },
            new[] { "eventing-integrity", "collection-gap" });
        yield return Definition(
            EventDecisionReportKind.AuthenticationPosture,
            "Authentication posture",
            "Summarizes NTLMv1, Kerberos encryption, LDAP signing, SMB1, failures, and successful access.",
            EventTypeCatalog.Expand(new[] { EventType.AuthenticationHealth }).ToArray(),
            new[] { "eventviewerx.authentication-modernization" },
            new[] { "authentication", "ntlmv1", "kerberos", "ldap-signing", "smb1" });
        yield return Definition(
            EventDecisionReportKind.IdentityLifecycle,
            "Identity lifecycle",
            "Builds account creation, state, password, membership, logon, and deletion timelines.",
            EventTypeCatalog.Expand(new[] { EventType.ActiveDirectoryAccountLifecycle }).ToArray(),
            new[] { "eventviewerx.identity-privilege" },
            new[] { "identity-lifecycle", "active-directory" });
        yield return Definition(
            EventDecisionReportKind.PrivilegedAccess,
            "Privileged access",
            "Connects privileged membership and rights changes to actors, targets, and logons.",
            new[] {
                EventType.ADGroupMembershipChange, EventType.ADUserRightsAssignment,
                EventType.ADUserPrivilegeUse, EventType.ADUserLogon, EventType.ADUserLogonFailed
            },
            new[] { "eventviewerx.identity-privilege" },
            new[] { "privilege", "privileged-logon" });
        yield return Definition(
            EventDecisionReportKind.GroupPolicyGovernance,
            "Group Policy governance",
            "Shows GPO creation, edits, links, enforcement flags, detailed changes, and deletion.",
            EventTypeCatalog.Expand(new[] { EventType.GroupPolicyActivity }).ToArray(),
            new[] { "eventviewerx.governance" },
            new[] { "group-policy", "governance" });
        yield return Definition(
            EventDecisionReportKind.CertificateServicesGovernance,
            "Certificate Services governance",
            "Shows certificate issuance evidence and declared CA or template coverage gaps.",
            new[] { EventType.CertificateIssued },
            new[] { "eventviewerx.governance" },
            new[] { "adcs", "certificate-services" });
        yield return Definition(
            EventDecisionReportKind.ExecutionAndPersistence,
            "Execution and persistence",
            "Connects scheduled tasks, firewall rules, Defender, device, driver, and network-monitor activity.",
            EventTypeCatalog.Expand(new[] {
                EventType.ScheduledTaskActivity, EventType.FirewallRuleActivity,
                EventType.DefenderSecurity, EventType.NetworkSecurity
            }).ToArray(),
            new[] { "eventviewerx.endpoint-protection" },
            new[] { "persistence", "scheduled-task", "firewall", "defender", "driver", "network" });
        yield return Definition(
            EventDecisionReportKind.DetectionHealth,
            "Detection health",
            "Shows enabled pack versions, integrity, required data, matches, and incomplete or error outcomes.",
            Array.Empty<EventType>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            allObservations: true,
            allFindings: true,
            allPacks: true);
        yield return Definition(
            EventDecisionReportKind.UnknownEventAndSchemaDrift,
            "Unknown events and schema drift",
            "Surfaces generic observations, unavailable fields, and incomplete or failed detections for definition work.",
            Array.Empty<EventType>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        yield return Definition(
            EventDecisionReportKind.IncidentTimeline,
            "Incident timeline",
            "Orders findings and raw evidence with source, receive, process, and pivot metadata.",
            Array.Empty<EventType>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            allObservations: true,
            allFindings: true,
            allPacks: true);
    }

    private static EventDecisionReportDefinition Definition(
        EventDecisionReportKind kind,
        string title,
        string description,
        IReadOnlyList<EventType> eventTypes,
        IReadOnlyList<string> packIds,
        IReadOnlyList<string> tags,
        bool allObservations = false,
        bool allFindings = false,
        bool allPacks = false) => new(
            kind,
            title,
            description,
            eventTypes,
            packIds,
            tags,
            allObservations,
            allFindings,
            allPacks);
}
