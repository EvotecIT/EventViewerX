namespace EventViewerX;

/// <summary>Built-in versioned native detections distributed with the core engine.</summary>
public static partial class EventDetectionCatalog {
    private static readonly DateTime PackCreatedUtc =
        new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Returns detached built-in packs grouped by operational ownership.</summary>
    public static IReadOnlyList<EventDetectionPack> GetBuiltInPacks() => new[] {
        CreateEventingIntegrityPack(),
        CreateIdentityPrivilegePack(),
        CreateAuthenticationPack(),
        CreateGovernancePack(),
        CreateEndpointProtectionPack()
    };

    /// <summary>Returns detached built-in rules suitable for tuning and plan compilation.</summary>
    public static IReadOnlyList<IEventDetectionRule> GetBuiltInRules() =>
        GetBuiltInPacks().SelectMany(static pack => pack.GetRules()).ToArray();

    /// <summary>Returns positive, negative, boundary, and known-benign fixtures for every built-in rule.</summary>
    public static IReadOnlyList<EventDetectionFixture> GetBuiltInFixtures() =>
        GetBuiltInPacks()
            .SelectMany(CreateFixtures)
            .ToArray();

    /// <summary>Executes every built-in fixture against only the rule it owns.</summary>
    public static IReadOnlyList<EventDetectionFixtureResult> TestBuiltInFixtures() {
        Dictionary<string, IEventDetectionRule> rules = GetBuiltInRules().ToDictionary(
            static rule => rule.Definition.RuleId,
            StringComparer.OrdinalIgnoreCase);
        return GetBuiltInFixtures()
            .Select(fixture => EventDetectionEngine.TestFixture(
                fixture,
                EventDetectionPlan.Compile(new[] { rules[fixture.RuleId] })))
            .ToArray();
    }

    private static EventDetectionPack CreateEventingIntegrityPack() => Pack(
        "eventviewerx.eventing-integrity",
        new[] {
            Definition("EVX-EVENTING-0001", "Security event log cleared", EventDetectionSeverity.Critical,
                EventType.LogsClearedSecurity, 99, "eventing-integrity", "attack.t1070.001"),
            Definition("EVX-EVENTING-0002", "Security event log reached capacity", EventDetectionSeverity.High,
                EventType.LogsFullSecurity, 95, "eventing-integrity", "collection-gap"),
            Definition("EVX-EVENTING-0003", "Audit policy changed", EventDetectionSeverity.High,
                EventType.AuditPolicyChange, 85, "eventing-integrity", "attack.t1562.002"),
            Definition("EVX-EVENTING-0004", "Application or System event log cleared", EventDetectionSeverity.High,
                EventType.LogsClearedOther, 90, "eventing-integrity", "attack.t1070.001"),
            Definition("EVX-EVENTING-0005", "Crash-on-audit-fail recovery occurred", EventDetectionSeverity.Critical,
                EventType.OSCrashOnAuditFailRecovery, 95, "eventing-integrity", "audit-failure"),
            Definition("EVX-EVENTING-0006", "System time changed", EventDetectionSeverity.Medium,
                EventType.OSTimeChange, 75, "eventing-integrity", "attack.t1070.006")
        });

    private static EventDetectionPack CreateIdentityPrivilegePack() => Pack(
        "eventviewerx.identity-privilege",
        new[] {
            new EventDetectionRuleDefinition {
                RuleId = "EVX-IDENTITY-0001",
                Title = "Privileged group membership changed",
                Description = "Detects membership changes involving commonly privileged Active Directory groups.",
                Severity = EventDetectionSeverity.High,
                Confidence = 80,
                EventTypes = new[] { EventType.ADGroupMembershipChange },
                Predicate = EventPredicate.Compare(
                    "GroupName",
                    EventPredicateOperator.In,
                    "Domain Admins", "Enterprise Admins", "Schema Admins", "Administrators"),
                Tags = new[] { "active-directory", "privilege", "attack.t1098" },
                FalsePositives = new[] { "Approved privileged access administration." }
            },
            Definition("EVX-IDENTITY-0002", "Directory user SID history changed", EventDetectionSeverity.High,
                EventType.ADUserCreateChange, 75, "active-directory", "sid-history", "attack.t1134.005",
                EventPredicate.AllOf(
                    EventPredicate.Compare("SidHistory", EventPredicateOperator.IsNotNull),
                    EventPredicate.Compare("SidHistory", EventPredicateOperator.NotEqual, string.Empty))),
            Definition("EVX-IDENTITY-0003", "User account lifecycle state changed", EventDetectionSeverity.Low,
                EventType.ADUserStatus, 90, "active-directory", "identity-lifecycle"),
            Definition("EVX-IDENTITY-0004", "User right assignment changed", EventDetectionSeverity.High,
                EventType.ADUserRightsAssignment, 85, "active-directory", "privilege", "attack.t1098"),
            Definition("EVX-IDENTITY-0005", "Special privileges assigned at logon", EventDetectionSeverity.Medium,
                EventType.ADUserPrivilegeUse, 80, "active-directory", "privileged-logon", "attack.t1078"),
            Definition("EVX-IDENTITY-0006", "Active Directory group changed", EventDetectionSeverity.Medium,
                EventType.ADGroupChange, 80, "active-directory", "group-governance"),
            Definition("EVX-IDENTITY-0007", "Audited object deleted", EventDetectionSeverity.Medium,
                EventType.ObjectDeletion, 80, "active-directory", "object-deletion"),
            Definition("EVX-IDENTITY-0008", "Computer account deleted", EventDetectionSeverity.Medium,
                EventType.ADComputerDeleted, 90, "active-directory", "computer-lifecycle"),
            ThresholdDefinition(
                "EVX-IDENTITY-0009",
                "Repeated account lockouts",
                EventDetectionSeverity.Medium,
                EventType.ADUserLockouts,
                threshold: 3,
                window: TimeSpan.FromMinutes(15),
                groupBy: "ObjectAffected",
                "active-directory", "lockout", "attack.t1110")
        });

    private static EventDetectionPack CreateAuthenticationPack() => Pack(
        "eventviewerx.authentication-modernization",
        new[] {
            Definition("EVX-AUTH-0001", "NTLMv1 authentication observed", EventDetectionSeverity.Medium,
                EventType.ADUserLogonNTLMv1, 95, "authentication", "ntlmv1", "attack.t1557"),
            new EventDetectionRuleDefinition {
                RuleId = "EVX-AUTH-0002",
                Title = "Repeated failed logons by account",
                Description = "Detects a bounded burst of failed logons for the same account.",
                Severity = EventDetectionSeverity.Medium,
                Confidence = 70,
                Kind = EventDetectionRuleKind.Threshold,
                EventTypes = new[] { EventType.ADUserLogonFailed },
                Threshold = 5,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "ObjectAffected",
                Tags = new[] { "authentication", "password-spray", "attack.t1110" },
                FalsePositives = new[] { "Stale service credentials or a user repeatedly entering an old password." }
            },
            new EventDetectionRuleDefinition {
                RuleId = "EVX-AUTH-0003",
                Title = "Failed logon followed by successful logon",
                Description = "Detects a successful logon after a failed logon for the same account.",
                Severity = EventDetectionSeverity.Medium,
                Confidence = 65,
                Kind = EventDetectionRuleKind.OrderedTemporal,
                Window = TimeSpan.FromMinutes(10),
                GroupBy = "ObjectAffected",
                Steps = new[] {
                    new EventDetectionStepDefinition {
                        Name = "failed-logon",
                        EventTypes = new[] { EventType.ADUserLogonFailed }
                    },
                    new EventDetectionStepDefinition {
                        Name = "successful-logon",
                        EventTypes = new[] { EventType.ADUserLogon }
                    }
                },
                Tags = new[] { "authentication", "account-compromise", "attack.t1078" },
                FalsePositives = new[] { "A user corrected an accidentally mistyped password." }
            },
            Definition("EVX-AUTH-0004", "Weak Kerberos TGT encryption observed", EventDetectionSeverity.Medium,
                EventType.KerberosTGTRequest, 90, "authentication", "kerberos-weak-encryption", "attack.t1558",
                EventPredicate.Compare("WeakEncryptionAlgorithm", EventPredicateOperator.Equal, true)),
            Definition("EVX-AUTH-0005", "Weak Kerberos service-ticket encryption observed", EventDetectionSeverity.Medium,
                EventType.KerberosServiceTicket, 90, "authentication", "kerberos-weak-encryption", "attack.t1558",
                EventPredicate.Compare("WeakEncryptionAlgorithm", EventPredicateOperator.Equal, true)),
            ThresholdDefinition(
                "EVX-AUTH-0006",
                "Repeated Kerberos ticket failures",
                EventDetectionSeverity.Medium,
                EventType.KerberosTicketFailure,
                threshold: 5,
                window: TimeSpan.FromMinutes(5),
                groupBy: "ObjectAffected",
                "authentication", "kerberos", "attack.t1110"),
            Definition("EVX-AUTH-0007", "Unsigned or cleartext LDAP bind observed", EventDetectionSeverity.Medium,
                EventType.ADLdapBindingDetails, 95, "authentication", "ldap-signing", "attack.t1557"),
            Definition("EVX-AUTH-0008", "SMB1 access observed", EventDetectionSeverity.High,
                EventType.ADSMBServerAuditV1, 95, "authentication", "smb1", "attack.t1021.002"),
            Definition("EVX-AUTH-0009", "Kerberos policy changed", EventDetectionSeverity.High,
                EventType.KerberosPolicyChange, 90, "authentication", "kerberos-policy")
        });

    private static EventDetectionPack CreateGovernancePack() => Pack(
        "eventviewerx.governance",
        new[] {
            Definition("EVX-GOVERNANCE-0001", "Group Policy Object deleted", EventDetectionSeverity.High,
                EventType.GpoDeleted, 90, "group-policy", "governance"),
            Definition("EVX-GOVERNANCE-0002", "Group Policy link changed", EventDetectionSeverity.Medium,
                EventType.ADGroupPolicyLinks, 80, "group-policy", "governance"),
            Definition("EVX-GOVERNANCE-0003", "Certificate issued", EventDetectionSeverity.Informational,
                EventType.CertificateIssued, 95, "adcs", "certificate-services"),
            Definition("EVX-GOVERNANCE-0004", "Group Policy Object created", EventDetectionSeverity.Low,
                EventType.GpoCreated, 90, "group-policy", "governance"),
            Definition("EVX-GOVERNANCE-0005", "Group Policy Object modified", EventDetectionSeverity.Medium,
                EventType.GpoModified, 85, "group-policy", "governance"),
            Definition("EVX-GOVERNANCE-0006", "Detailed Group Policy directory change", EventDetectionSeverity.Medium,
                EventType.ADGroupPolicyChangesDetailed, 85, "group-policy", "governance"),
            Definition("EVX-GOVERNANCE-0007", "BitLocker protection suspended", EventDetectionSeverity.High,
                EventType.BitLockerSuspended, 90, "bitlocker", "security-control", "attack.t1562.001"),
            Definition("EVX-GOVERNANCE-0008", "BitLocker recovery material changed", EventDetectionSeverity.Medium,
                EventType.BitLockerKeyChange, 90, "bitlocker", "key-governance")
        });

    private static EventDetectionPack CreateEndpointProtectionPack() => Pack(
        "eventviewerx.endpoint-protection",
        new[] {
            Definition("EVX-ENDPOINT-0001", "Microsoft Defender detected a threat", EventDetectionSeverity.High,
                EventType.DefenderThreatDetected, 95, "defender", "threat"),
            Definition("EVX-ENDPOINT-0002", "Microsoft Defender configuration changed", EventDetectionSeverity.Medium,
                EventType.DefenderConfigurationChanged, 80, "defender", "configuration", "attack.t1562.001"),
            Definition("EVX-ENDPOINT-0003", "Scheduled task created", EventDetectionSeverity.Medium,
                EventType.ScheduledTaskCreated, 75, "scheduled-task", "persistence", "attack.t1053.005"),
            Definition("EVX-ENDPOINT-0004", "Firewall rule added", EventDetectionSeverity.Low,
                EventType.FirewallRuleAdded, 70, "firewall", "configuration"),
            Definition("EVX-ENDPOINT-0005", "Microsoft Defender acted on a threat", EventDetectionSeverity.Medium,
                EventType.DefenderThreatAction, 95, "defender", "remediation"),
            Definition("EVX-ENDPOINT-0006", "Scheduled task updated", EventDetectionSeverity.Medium,
                EventType.ScheduledTaskUpdated, 75, "scheduled-task", "persistence", "attack.t1053.005"),
            Definition("EVX-ENDPOINT-0007", "Scheduled task enabled", EventDetectionSeverity.Low,
                EventType.ScheduledTaskEnabled, 80, "scheduled-task", "persistence", "attack.t1053.005"),
            Definition("EVX-ENDPOINT-0008", "Scheduled task deleted", EventDetectionSeverity.Low,
                EventType.ScheduledTaskDeleted, 85, "scheduled-task", "defense-evasion"),
            Definition("EVX-ENDPOINT-0009", "Firewall rule deleted", EventDetectionSeverity.Medium,
                EventType.FirewallRuleDeleted, 80, "firewall", "configuration"),
            Definition("EVX-ENDPOINT-0010", "Network monitoring driver loaded", EventDetectionSeverity.High,
                EventType.NetworkMonitorDriverLoaded, 80, "network", "driver", "attack.t1014"),
            Definition("EVX-ENDPOINT-0011", "Network adapter entered promiscuous mode", EventDetectionSeverity.High,
                EventType.NetworkPromiscuousMode, 85, "network", "packet-capture", "attack.t1040"),
            Definition("EVX-ENDPOINT-0012", "External device recognized", EventDetectionSeverity.Low,
                EventType.DeviceRecognized, 65, "device", "removable-media")
        });

    private static EventDetectionPack Pack(
        string packId,
        IEnumerable<EventDetectionRuleDefinition> rules) =>
        EventDetectionPack.Create(
            packId,
            "1.0.0",
            rules,
            new[] { "EvotecIT" },
            "MIT",
            createdUtc: PackCreatedUtc);

    private static EventDetectionRuleDefinition Definition(
        string id,
        string title,
        EventDetectionSeverity severity,
        EventType eventType,
        int confidence,
        string firstTag,
        string secondTag,
        string? thirdTag = null,
        EventPredicate? predicate = null) {

        return new EventDetectionRuleDefinition {
            RuleId = id,
            Version = "1.0.0",
            Title = title,
            Description = title + ".",
            Severity = severity,
            Confidence = confidence,
            Kind = EventDetectionRuleKind.Stateless,
            EventTypes = new[] { eventType },
            Predicate = predicate,
            Tags = new[] { firstTag, secondTag, thirdTag }
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Select(static tag => tag!)
                .ToArray()
        };
    }

    private static EventDetectionRuleDefinition ThresholdDefinition(
        string id,
        string title,
        EventDetectionSeverity severity,
        EventType eventType,
        int threshold,
        TimeSpan window,
        string groupBy,
        params string[] tags) => new() {
            RuleId = id,
            Version = "1.0.0",
            Title = title,
            Description = title + ".",
            Severity = severity,
            Confidence = 75,
            Kind = EventDetectionRuleKind.Threshold,
            EventTypes = new[] { eventType },
            Threshold = threshold,
            Window = window,
            GroupBy = groupBy,
            Tags = tags
        };
}
