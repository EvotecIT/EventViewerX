namespace EventViewerX;

/// <summary>Single source of built-in event requirements for readiness and generated guidance.</summary>
public static class EventRequirementCatalog {
    private const string AuditBase = "https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/security/threat-protection/auditing/";
    // Security events 1102/1104/1105 and 4616 are emitted independently of a configurable
    // advanced-audit subcategory, so their channel requirement is intentionally sufficient.
    private static readonly IReadOnlyDictionary<EventType, EventPrerequisite[]> Specific =
        new Dictionary<EventType, EventPrerequisite[]> {
            [EventType.ADUserLogon] = new[] { Audit("logon-success", "Audit Logon", EventAuditOutcome.Success, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLogonNTLMv1] = new[] { Audit("logon-success", "Audit Logon", EventAuditOutcome.Success, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLogonFailed] = new[] { Audit("logon-failure", "Audit Logon", EventAuditOutcome.Failure, "Target member server, workstation, or domain controller", "audit-logon") },
            [EventType.ADUserLockouts] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserUnlocked] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserCreateChange] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserStatus] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success | EventAuditOutcome.Failure, "Domain controllers", "audit-user-account-management") },
            [EventType.ADUserPrivilegeUse] = new[] { Audit("special-logon", "Audit Special Logon", EventAuditOutcome.Success, "Target computer", "audit-special-logon") },
            [EventType.ADUserRightsAssignment] = new[] { Audit("authorization-policy-change", "Audit Authorization Policy Change", EventAuditOutcome.Success, "Target computer", "audit-authorization-policy-change") },
            [EventType.ADGroupMembershipChange] = GroupManagement(),
            [EventType.ADGroupChange] = GroupManagement(),
            [EventType.ADGroupCreateDelete] = GroupManagement(),
            [EventType.ADGroupEnumeration] = new[] { Audit("user-account-management", "Audit User Account Management", EventAuditOutcome.Success, "Target computer", "audit-user-account-management") },
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
            [EventType.KerberosPolicyChange] = new[] {
                Audit("authentication-policy-change", "Audit Authentication Policy Change", EventAuditOutcome.Success, "Domain controllers", "audit-authentication-policy-change")
            },
            [EventType.KerberosKdcRc4Audit] = new[] {
                DomainControllerRole(),
                Configuration(
                    "kdcsvc-rc4-event-schema",
                    "KDCsvc RC4 event schema",
                    "The domain controller must include the Kerberos update that registers Kdcsvc System events 201 through 209. Event emission also depends on the effective rollout phase and a matching request or configuration; absence of an event is not proof that every client and service is compatible.",
                    "Domain controllers",
                    "https://support.microsoft.com/en-US/servicing/os/windows/2025/11/how-to-manage-kerberos-kdc-usage-of-rc4-for-service-account-ticket-issuance-changes-related-to-cve-2")
            },
            [EventType.AuditPolicyChange] = new[] { Audit("audit-policy-change", "Audit Audit Policy Change", EventAuditOutcome.Success, "Target computer", "audit-audit-policy-change") },
            [EventType.FirewallRuleChange] = new[] { Audit("mpssvc-rule-change", "Audit MPSSVC Rule-Level Policy Change", EventAuditOutcome.Success, "Target computer", "audit-mpssvc-rule-level-policy-change") },
            [EventType.FirewallRuleAdded] = new[] { Audit("mpssvc-rule-change", "Audit MPSSVC Rule-Level Policy Change", EventAuditOutcome.Success, "Target computer", "audit-mpssvc-rule-level-policy-change") },
            [EventType.FirewallRuleDeleted] = new[] { Audit("mpssvc-rule-change", "Audit MPSSVC Rule-Level Policy Change", EventAuditOutcome.Success, "Target computer", "audit-mpssvc-rule-level-policy-change") },
            [EventType.ScheduledTaskCreated] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ScheduledTaskDeleted] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ScheduledTaskEnabled] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ScheduledTaskDisabled] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ScheduledTaskUpdated] = new[] { Audit("other-object-access", "Audit Other Object Access Events", EventAuditOutcome.Success, "Target computer", "audit-other-object-access-events") },
            [EventType.ADSMBServerAuditV1] = Smb1Auditing(),
            [EventType.NetworkAccessAuthenticationPolicy] = NetworkPolicyServerAuditing(),
            [EventType.CertificateIssued] = CertificateIssuance(),
            [EventType.DeviceRecognized] = new[] { Audit("pnp-activity", "Audit PNP Activity", EventAuditOutcome.Success, "Target computer", "audit-pnp-activity") },
            [EventType.DeviceDisabled] = new[] { Audit("pnp-activity", "Audit PNP Activity", EventAuditOutcome.Success, "Target computer", "audit-pnp-activity") },
            [EventType.BitLockerKeyChange] = BitLockerKeyAuditing(),
            [EventType.ObjectDeletion] = ObjectDeletionAuditing(),
            [EventType.OSStartupSecurity] = new[] { Audit("security-state-change", "Audit Security State Change", EventAuditOutcome.Success, "Target computer", "audit-security-state-change") },
            [EventType.OSCrashOnAuditFailRecovery] = new[] { Audit("security-state-change", "Audit Security State Change", EventAuditOutcome.Success, "Target computer", "audit-security-state-change") },
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
            [EventType.GroupPolicyDirectoryAudit] = DirectoryChanges(),
            [EventType.ADLdapBindingDetails] = new[] {
                Configuration(
                    "ntds-ldap-interface-events-2",
                    "NTDS LDAP Interface Events diagnostic level 2",
                    "Event 2889 requires the domain controller registry value '16 LDAP Interface Events' at level 2. Readiness reports this setting but does not change it.",
                    "Domain controllers",
                    "https://learn.microsoft.com/en-us/troubleshoot/windows-server/active-directory/enable-ldap-signing-in-windows-server")
            },
            [EventType.ADLdapBindingSummary] = new[] { DomainControllerRole() }
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
            .Select(MergePrerequisites)
            .OrderBy(static requirement => requirement.Kind)
            .ThenBy(static requirement => requirement.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new EventTypeRequirement(type, definition.Sources, distinct, leaves);
    }

    internal static EventPrerequisite MergePrerequisites(IEnumerable<EventPrerequisite> requirements) {
        EventPrerequisite[] items = requirements.ToArray();
        EventPrerequisite first = items[0];
        EventAuditOutcome outcomes = items.Aggregate(
            EventAuditOutcome.None,
            static (current, requirement) => current | requirement.AuditOutcomes);
        EventRequirementVolume volume = items.Max(static requirement => requirement.Volume);
        if (outcomes == first.AuditOutcomes && volume == first.Volume) {
            return first;
        }
        return new EventPrerequisite(
            first.Key,
            first.Kind,
            first.Name,
            first.Description,
            first.AppliesTo,
            outcomes,
            volume,
            first.DocumentationUri,
            first.AuditSubcategoryGuid);
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

    private static EventPrerequisite CertificateAuthorityRole() => new(
        "target-role:certification-authority",
        EventRequirementKind.TargetRole,
        "Certification Authority source role",
        "Certificate request and issuance events are emitted by an Active Directory Certificate Services Certification Authority.",
        "Event source computer");

    private static EventPrerequisite NetworkPolicyServerRole() => new(
        "target-role:network-policy-server",
        EventRequirementKind.TargetRole,
        "Network Policy Server source role",
        "Network access grant and denial events are emitted by a Windows Network Policy Server.",
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

    private static EventPrerequisite[] GroupManagement() => new[] {
        Audit("security-group-management", "Audit Security Group Management", EventAuditOutcome.Success, "Target computer", "audit-security-group-management"),
        Audit("distribution-group-management", "Audit Distribution Group Management", EventAuditOutcome.Success, "Target computer", "audit-distribution-group-management")
    };

    private static EventPrerequisite[] Smb1Auditing() => new[] {
        Configuration(
            "smb1-access-auditing",
            "SMB1 access auditing",
            "SMB server configuration must enable AuditSmb1Access for event 3000 to be emitted.",
            "SMB server",
            "https://learn.microsoft.com/en-us/windows-server/storage/file-server/troubleshoot/detect-enable-and-disable-smbv1-v2-v3")
    };

    private static EventPrerequisite[] NetworkPolicyServerAuditing() => new[] {
        NetworkPolicyServerRole(),
        Audit(
            "network-policy-server",
            "Audit Network Policy Server",
            EventAuditOutcome.Success | EventAuditOutcome.Failure,
            "Network Policy Servers",
            "audit-network-policy-server")
    };

    private static EventPrerequisite[] ObjectDeletionAuditing() => new[] {
        Configuration(
            "object-deletion-audit-subcategory",
            "Object deletion audit subcategory",
            "The Object Access subcategory matching the selected file, registry, kernel, SAM, directory, or other object category must be enabled for the requested outcomes.",
            "Target computer and selected object category",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/advanced-audit-policy-configuration"),
        Configuration(
            "object-deletion-sacl",
            "Object deletion auditing SACL",
            "The selected object must have an auditing SACL that covers deletion by the identities being monitored.",
            "Selected object",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/advanced-audit-policy-configuration")
    };

    private static EventPrerequisite[] BitLockerKeyAuditing() => new[] {
        Audit("sensitive-privilege-use", "Audit Sensitive Privilege Use", EventAuditOutcome.Success | EventAuditOutcome.Failure, "Target computer", "audit-sensitive-privilege-use"),
        Audit("dpapi-activity", "Audit DPAPI Activity", EventAuditOutcome.Success | EventAuditOutcome.Failure, "Target computer", "audit-dpapi-activity")
    };

    private static EventPrerequisite[] CertificateIssuance() => new[] {
        CertificateAuthorityRole(),
        Audit(
            "certification-services",
            "Audit Certification Services",
            EventAuditOutcome.Success,
            "Certification Authority servers",
            "audit-certification-services",
            EventRequirementVolume.Medium),
        Configuration(
            "certification-authority-audit-filter-requests",
            "Certification Authority request audit filter",
            "The Certification Authority audit filter must include 'Issue and manage certificate requests' (bit 4) for events 4886 and 4887.",
            "Certification Authority servers",
            "https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-server-2012-r2-and-2012/dn786422(v=ws.11)")
    };

    private static Guid ResolveAuditSubcategoryGuid(string key) => key switch {
        "logon-success" or "logon-failure" => new Guid("0CCE9215-69AE-11D9-BED3-505054503030"),
        "security-state-change" => new Guid("0CCE9210-69AE-11D9-BED3-505054503030"),
        "special-logon" => new Guid("0CCE921B-69AE-11D9-BED3-505054503030"),
        "certification-services" => new Guid("0CCE9221-69AE-11D9-BED3-505054503030"),
        "other-object-access" => new Guid("0CCE9227-69AE-11D9-BED3-505054503030"),
        "sensitive-privilege-use" => new Guid("0CCE9228-69AE-11D9-BED3-505054503030"),
        "non-sensitive-privilege-use" => new Guid("0CCE9229-69AE-11D9-BED3-505054503030"),
        "dpapi-activity" => new Guid("0CCE922D-69AE-11D9-BED3-505054503030"),
        "audit-policy-change" => new Guid("0CCE922F-69AE-11D9-BED3-505054503030"),
        "authentication-policy-change" => new Guid("0CCE9230-69AE-11D9-BED3-505054503030"),
        "authorization-policy-change" => new Guid("0CCE9231-69AE-11D9-BED3-505054503030"),
        "mpssvc-rule-change" => new Guid("0CCE9232-69AE-11D9-BED3-505054503030"),
        "user-account-management" => new Guid("0CCE9235-69AE-11D9-BED3-505054503030"),
        "computer-account-management" => new Guid("0CCE9236-69AE-11D9-BED3-505054503030"),
        "security-group-management" => new Guid("0CCE9237-69AE-11D9-BED3-505054503030"),
        "distribution-group-management" => new Guid("0CCE9238-69AE-11D9-BED3-505054503030"),
        "directory-service-changes" => new Guid("0CCE923C-69AE-11D9-BED3-505054503030"),
        "kerberos-service-ticket" => new Guid("0CCE9240-69AE-11D9-BED3-505054503030"),
        "kerberos-authentication" or "kerberos-authentication-failure" => new Guid("0CCE9242-69AE-11D9-BED3-505054503030"),
        "network-policy-server" => new Guid("0CCE9243-69AE-11D9-BED3-505054503030"),
        "pnp-activity" => new Guid("0CCE9248-69AE-11D9-BED3-505054503030"),
        _ => throw new InvalidOperationException($"No audit subcategory GUID is registered for '{key}'.")
    };
}
