namespace EventViewerX;

/// <summary>Single source of built-in event requirements for readiness and generated guidance.</summary>
public static class EventRequirementCatalog {
    private const string AuditBase = "https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/";
    private static readonly IReadOnlyDictionary<EventType, EventPrerequisite[]> Specific =
        new Dictionary<EventType, EventPrerequisite[]> {
            [EventType.ADUserLogon] = new[] { Audit("logon-success", "Audit Logon", EventAuditOutcome.Success, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLogonNTLMv1] = new[] { Audit("logon-success", "Audit Logon", EventAuditOutcome.Success, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLogonFailed] = new[] { Audit("logon-failure", "Audit Logon", EventAuditOutcome.Failure, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLockouts] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserUnlocked] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserCreateChange] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserStatus] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADGroupMembershipChange] = new[] { Audit("security-group-management", "Audit Security Group Management", EventAuditOutcome.Success, "Domain controllers", "audit-security-group-management") },
            [EventType.ADGroupChange] = new[] { Audit("security-group-management", "Audit Security Group Management", EventAuditOutcome.Success, "Domain controllers", "audit-security-group-management") },
            [EventType.ADGroupCreateDelete] = new[] { Audit("security-group-management", "Audit Security Group Management", EventAuditOutcome.Success, "Domain controllers", "audit-security-group-management") },
            [EventType.ADComputerCreateChange] = new[] { Audit("computer-account-management", "Audit Computer Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-computer-account-management") },
            [EventType.ADComputerDeleted] = new[] { Audit("computer-account-management", "Audit Computer Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-computer-account-management") },
            [EventType.KerberosTGTRequest] = new[] {
                Audit("kerberos-authentication", "Audit Kerberos Authentication Service", EventAuditOutcome.Success | EventAuditOutcome.Failure, "Domain controllers", "audit-kerberos-authentication-service")
            },
            [EventType.KerberosServiceTicket] = new[] {
                Audit("kerberos-service-ticket", "Audit Kerberos Service Ticket Operations", EventAuditOutcome.Success | EventAuditOutcome.Failure, "Domain controllers", "audit-kerberos-service-ticket-operations", EventRequirementVolume.VeryHigh)
            },
            [EventType.KerberosTicketFailure] = new[] {
                Audit("kerberos-authentication-failure", "Audit Kerberos Authentication Service", EventAuditOutcome.Failure, "Domain controllers", "audit-kerberos-authentication-service")
            },
            [EventType.AuditPolicyChange] = new[] { Audit("audit-policy-change", "Audit Audit Policy Change", EventAuditOutcome.Success, "Target computer", "audit-audit-policy-change") },
            [EventType.FirewallRuleChange] = new[] { Audit("mpssvc-rule-change", "Audit MPSSVC Rule-Level Policy Change", EventAuditOutcome.Success, "Target computer", "audit-mpssvc-rule-level-policy-change") },
            [EventType.ScheduledTaskCreated] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ScheduledTaskDeleted] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ADGroupPolicyChanges] = DirectoryChanges(),
            [EventType.ADGroupPolicyEdits] = DirectoryChanges(),
            [EventType.ADGroupPolicyLinks] = DirectoryChanges(),
            [EventType.ADGroupPolicyChangesDetailed] = DirectoryChanges(),
            [EventType.GpoCreated] = DirectoryChanges(),
            [EventType.GpoDeleted] = DirectoryChanges(),
            [EventType.GpoModified] = DirectoryChanges(),
            [EventType.ADUserChangeDetailed] = DirectoryChanges(),
            [EventType.ADGroupChangeDetailed] = DirectoryChanges(),
            [EventType.ADComputerChangeDetailed] = DirectoryChanges(),
            [EventType.ADOrganizationalUnitChangeDetailed] = DirectoryChanges(),
            [EventType.ADOtherChangeDetailed] = DirectoryChanges(),
            [EventType.ADLdapBindingDetails] = new[] {
                Configuration(
                    "ntds-ldap-interface-events-2",
                    "NTDS LDAP Interface Events diagnostic level 2",
                    "Event 2889 requires the domain controller registry value '16 LDAP Interface Events' at level 2. Readiness reports this setting but does not change it.",
                    "Domain controllers",
                    "https://learn.microsoft.com/en-us/troubleshoot/windows-server/active-directory/enable-ldap-signing-in-windows-server")
            }
        };

    /// <summary>Returns requirements for all built-in event types.</summary>
    public static IReadOnlyList<EventTypeRequirement> GetRequirements() =>
        Enum.GetValues(typeof(EventType)).Cast<EventType>().Select(GetRequirement).ToArray();

    /// <summary>Returns requirements for one leaf or composite event type.</summary>
    public static EventTypeRequirement GetRequirement(EventType type) {
        EventTypeDefinition definition = EventTypeCatalog.GetDefinition(type);
        EventType[] leaves = EventTypeCatalog.Expand(new[] { type }).ToArray();
        var prerequisites = new List<EventPrerequisite>();
        foreach (EventSourceDefinition source in definition.Sources) {
            prerequisites.Add(Channel(source.LogName));
        }
        foreach (EventType leaf in leaves) {
            if (Specific.TryGetValue(leaf, out EventPrerequisite[]? specific)) {
                prerequisites.AddRange(specific);
            }
        }
        if (prerequisites.Any(static prerequisite =>
                string.Equals(prerequisite.AppliesTo, "Domain controllers", StringComparison.OrdinalIgnoreCase))) {
            prerequisites.Add(DomainControllerRole());
        }
        EventPrerequisite[] distinct = prerequisites
            .GroupBy(static requirement => requirement.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static requirement => requirement.Kind)
            .ThenBy(static requirement => requirement.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new EventTypeRequirement(type, definition.Sources, distinct, leaves);
    }

    private static EventPrerequisite Channel(string logName) => new(
        "channel:" + logName.ToUpperInvariant(),
        EventRequirementKind.EventChannel,
        logName,
        "The channel must exist, be enabled when applicable, and be readable by the collection identity.",
        "Event source computer");

    private static EventPrerequisite DomainControllerRole() => new(
        "target-role:domain-controller",
        EventRequirementKind.TargetRole,
        "Domain controller source role",
        "The selected events are emitted by a domain controller, so querying another Windows role cannot prove coverage.",
        "Event source computer");

    private static EventPrerequisite Audit(
        string key,
        string name,
        EventAuditOutcome outcomes,
        string appliesTo,
        string documentationLeaf,
        EventRequirementVolume volume = EventRequirementVolume.Unknown) => new(
            "audit:" + key,
            EventRequirementKind.AuditPolicy,
            name,
            "The effective advanced audit policy must produce the selected event outcomes.",
            appliesTo,
            outcomes,
            volume,
            AuditBase + documentationLeaf,
            ResolveAuditSubcategoryGuid(key));

    private static EventPrerequisite Configuration(
        string key,
        string name,
        string description,
        string appliesTo,
        string documentationUri) => new(
            "configuration:" + key,
            EventRequirementKind.Configuration,
            name,
            description,
            appliesTo,
            documentationUri: documentationUri);

    private static EventPrerequisite[] DirectoryChanges() => new[] {
        Audit("directory-service-changes", "Audit Directory Service Changes", EventAuditOutcome.Success, "Domain controllers", "audit-directory-service-changes"),
        Configuration(
            "directory-object-sacl",
            "Directory object auditing SACL",
            "The affected directory objects must have an auditing SACL that emits the requested change events.",
            "Selected Active Directory objects",
            AuditBase + "audit-directory-service-changes")
    };

    private static Guid ResolveAuditSubcategoryGuid(string key) => key switch {
        "logon-success" or "logon-failure" => new Guid("0CCE9215-69AE-11D9-BED3-505054503030"),
        "other-object-access" => new Guid("0CCE9227-69AE-11D9-BED3-505054503030"),
        "audit-policy-change" => new Guid("0CCE922F-69AE-11D9-BED3-505054503030"),
        "mpssvc-rule-change" => new Guid("0CCE9232-69AE-11D9-BED3-505054503030"),
        "user-account-management" => new Guid("0CCE9235-69AE-11D9-BED3-505054503030"),
        "computer-account-management" => new Guid("0CCE9236-69AE-11D9-BED3-505054503030"),
        "security-group-management" => new Guid("0CCE9237-69AE-11D9-BED3-505054503030"),
        "directory-service-changes" => new Guid("0CCE923C-69AE-11D9-BED3-505054503030"),
        "kerberos-service-ticket" => new Guid("0CCE9240-69AE-11D9-BED3-505054503030"),
        "kerberos-authentication" or "kerberos-authentication-failure" => new Guid("0CCE9242-69AE-11D9-BED3-505054503030"),
        _ => throw new InvalidOperationException($"No audit subcategory GUID is registered for '{key}'.")
    };
}
