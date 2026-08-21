using System.Reflection;
using System.Text.RegularExpressions;

namespace EventViewerX;

public static partial class EventTypeCatalog {
    private static readonly ConcurrentDictionary<EventType, EventTypeDefinition> DefinitionCache = new();

    private static readonly IReadOnlyDictionary<EventType, EventType[]> CompositeMembers =
        new Dictionary<EventType, EventType[]> {
            [EventType.ActiveDirectoryAuthentication] = new[] {
                EventType.ADUserLogon,
                EventType.ADUserLogonFailed,
                EventType.ADUserLogonNTLMv1,
                EventType.ADUserLockouts,
                EventType.ADUserUnlocked,
                EventType.ADUserPrivilegeUse,
                EventType.KerberosActivity
            },
            [EventType.ActiveDirectoryAccountLifecycle] = new[] {
                EventType.ADUserCreateChange,
                EventType.ADUserStatus,
                EventType.ADUserChangeDetailed,
                EventType.ADGroupMembershipChange,
                EventType.ADGroupChange,
                EventType.ADGroupCreateDelete,
                EventType.ADGroupChangeDetailed,
                EventType.ADComputerCreateChange,
                EventType.ADComputerDeleted,
                EventType.ADComputerChangeDetailed,
                EventType.ADOrganizationalUnitChangeDetailed,
                EventType.ADOtherChangeDetailed
            },
            [EventType.ActiveDirectoryChanges] = new[] {
                EventType.ActiveDirectoryAccountLifecycle,
                EventType.GroupPolicyActivity,
                EventType.ADUserRightsAssignment,
                EventType.AuditPolicyChange
            },
            [EventType.GroupPolicyActivity] = new[] {
                EventType.ADGroupPolicyChanges,
                EventType.ADGroupPolicyEdits,
                EventType.ADGroupPolicyLinks,
                EventType.ADGroupPolicyChangesDetailed,
                EventType.GpoCreated,
                EventType.GpoDeleted,
                EventType.GpoModified,
                EventType.ClientGroupPoliciesApplication,
                EventType.ClientGroupPoliciesSystem
            },
            [EventType.KerberosActivity] = new[] {
                EventType.KerberosTGTRequest,
                EventType.KerberosServiceTicket,
                EventType.KerberosTicketFailure,
                EventType.KerberosPolicyChange
            },
            [EventType.OperatingSystemLifecycle] = new[] {
                EventType.OSStartup,
                EventType.OSStartupSecurity,
                EventType.OSShutdown,
                EventType.OSUncleanShutdown,
                EventType.OSCrash,
                EventType.OSBugCheck,
                EventType.OSCrashOnAuditFailRecovery,
                EventType.OSTimeChange
            },
            [EventType.WindowsSecurityChanges] = new[] {
                EventType.AuditPolicyChange,
                EventType.FirewallRuleChange,
                EventType.BitLockerKeyChange,
                EventType.BitLockerSuspended,
                EventType.DeviceDisabled,
                EventType.DeviceRecognized,
                EventType.ObjectDeletion,
                EventType.ScheduledTaskCreated,
                EventType.ScheduledTaskDeleted,
                EventType.LogsClearedSecurity,
                EventType.LogsClearedOther,
                EventType.LogsFullSecurity
            },
            [EventType.EntraConnectHealth] = new[] {
                EventType.AADConnectStagingEnabled,
                EventType.AADConnectStagingDisabled,
                EventType.AADConnectPasswordSyncFailed,
                EventType.AADConnectRunProfile,
                EventType.AADSyncCycleStage,
                EventType.AADSyncProvisionCredentialsPing,
                EventType.AADSyncPasswordHashSyncStatus,
                EventType.AADSyncImportStatus,
                EventType.AADSyncFilterStatus,
                EventType.SyncCompleted
            },
            [EventType.NetworkSecurity] = new[] {
                EventType.NetworkAccessAuthenticationPolicy,
                EventType.FirewallRuleChange,
                EventType.ADSMBServerAuditV1,
                EventType.NetworkMonitorDriverLoaded,
                EventType.NetworkPromiscuousMode,
                EventType.DhcpLeaseCreated
            },
            [EventType.InfrastructureHealth] = new[] {
                EventType.OperatingSystemLifecycle,
                EventType.WindowsUpdateFailure,
                EventType.DfsReplicationError,
                EventType.HyperVVirtualMachineStarted,
                EventType.HyperVVirtualMachineShutdown,
                EventType.HyperVCheckpointCreated,
                EventType.IISSiteBindingFailure,
                EventType.IISSiteStopped,
                EventType.ExchangeDatabaseMounted,
                EventType.SqlDatabaseCreated,
                EventType.EntraConnectHealth
            }
        };

    /// <summary>Returns every built-in leaf and composite event definition.</summary>
    public static IReadOnlyList<EventTypeDefinition> GetDefinitions() {
        return Enum.GetValues(typeof(EventType))
            .Cast<EventType>()
            .Select(GetDefinition)
            .ToArray();
    }

    /// <summary>Returns metadata for one built-in event definition.</summary>
    public static EventTypeDefinition GetDefinition(EventType type) {
        if (!Enum.IsDefined(typeof(EventType), type)) {
            throw new ArgumentOutOfRangeException(nameof(type), type, "The event type is not defined.");
        }
        return DefinitionCache.GetOrAdd(type, CreateDefinition);
    }

    /// <summary>Expands leaf and composite definitions into a distinct ordered leaf list.</summary>
    public static IReadOnlyList<EventType> Expand(IEnumerable<EventType> eventTypes) {
        if (eventTypes == null) {
            throw new ArgumentNullException(nameof(eventTypes));
        }

        var result = new List<EventType>();
        var emitted = new HashSet<EventType>();
        var active = new HashSet<EventType>();
        foreach (EventType type in eventTypes) {
            ExpandOne(type, result, emitted, active);
        }
        return result;
    }

    /// <summary>Returns the distinct native sources selected by one or more leaf or composite definitions.</summary>
    public static IReadOnlyList<EventSourceDefinition> GetSources(IEnumerable<EventType> eventTypes) {
        IReadOnlyList<EventType> expanded = Expand(eventTypes);
        return GetSourceMap(expanded)
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new EventSourceDefinition(pair.Key, pair.Value))
            .ToArray();
    }

    private static void ExpandOne(
        EventType type,
        List<EventType> result,
        HashSet<EventType> emitted,
        HashSet<EventType> active) {

        if (!Enum.IsDefined(typeof(EventType), type)) {
            throw new ArgumentOutOfRangeException(nameof(type), type, "The event type is not defined.");
        }
        if (!CompositeMembers.TryGetValue(type, out EventType[]? members)) {
            if (emitted.Add(type)) {
                result.Add(type);
            }
            return;
        }
        if (!active.Add(type)) {
            throw new InvalidOperationException($"Composite event definition '{type}' contains a cycle.");
        }
        foreach (EventType member in members) {
            ExpandOne(member, result, emitted, active);
        }
        active.Remove(type);
    }

    private static EventTypeDefinition CreateDefinition(EventType type) {
        string displayName = SplitWords(type.ToString());
        string category = ResolveCategory(type);
        EventType[] includedTypes = CompositeMembers.TryGetValue(type, out EventType[]? members)
            ? members.ToArray()
            : Array.Empty<EventType>();
        Type? recordType = includedTypes.Length == 0
            ? GetEventRuleType(type)
            : null;
        IReadOnlyList<EventFieldDefinition> fields = recordType == null
            ? Expand(new[] { type })
                .Select(GetDefinition)
                .SelectMany(static definition => definition.Fields)
                .GroupBy(static field => field.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray()
            : GetFields(recordType);
        string description = includedTypes.Length == 0
            ? $"Typed projection for {displayName} events."
            : $"Composite view combining {Expand(new[] { type }).Count} typed event definitions.";

        return new EventTypeDefinition(
            type,
            displayName,
            description,
            category,
            GetSources(new[] { type }),
            fields,
            recordType,
            includedTypes);
    }

    private static IReadOnlyList<EventFieldDefinition> GetFields(Type recordType) {
        var fields = new List<EventFieldDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo property in recordType.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
            if (property.GetIndexParameters().Length != 0 || !seen.Add(property.Name)) {
                continue;
            }
            fields.Add(new EventFieldDefinition(
                property.Name,
                SplitWords(property.Name),
                property.PropertyType,
                property.DeclaringType == typeof(EventTypeRecord)));
        }
        foreach (FieldInfo field in recordType.GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            if (!seen.Add(field.Name)) {
                continue;
            }
            fields.Add(new EventFieldDefinition(
                field.Name,
                SplitWords(field.Name),
                field.FieldType,
                field.DeclaringType == typeof(EventTypeRecord)));
        }
        return fields;
    }

    internal static IReadOnlyList<EventFieldDefinition> GetCommonFields() {
        return GetFields(typeof(EventTypeRecord));
    }

    private static string ResolveCategory(EventType type) {
        string name = type.ToString();
        if (name.StartsWith("AD", StringComparison.Ordinal) ||
            name.StartsWith("Gpo", StringComparison.Ordinal) ||
            name.StartsWith("Kerberos", StringComparison.Ordinal) ||
            name.StartsWith("ActiveDirectory", StringComparison.Ordinal) ||
            name.StartsWith("GroupPolicy", StringComparison.Ordinal)) {
            return "Active Directory";
        }
        if (name.StartsWith("AAD", StringComparison.Ordinal) ||
            name.StartsWith("Entra", StringComparison.Ordinal) ||
            name == nameof(EventType.SyncCompleted)) {
            return "Entra Connect";
        }
        if (name.StartsWith("OS", StringComparison.Ordinal) ||
            name.StartsWith("Windows", StringComparison.Ordinal) ||
            name.StartsWith("Logs", StringComparison.Ordinal)) {
            return "Windows";
        }
        if (name.Contains("Network", StringComparison.Ordinal) ||
            name.Contains("Firewall", StringComparison.Ordinal) ||
            name.Contains("Dhcp", StringComparison.Ordinal) ||
            name.Contains("SMB", StringComparison.Ordinal)) {
            return "Network";
        }
        return "Infrastructure";
    }

    private static string SplitWords(string value) {
        string separated = Regex.Replace(
            value,
            "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
            " ");
        return string.Join(" ", separated.Split(' ').Select(static word => word switch {
            "Dhcp" => "DHCP",
            "Dns" => "DNS",
            "Gpo" => "GPO",
            "Guid" => "GUID",
            "Id" => "ID",
            "Ip" => "IP",
            "Rdp" => "RDP",
            "Sid" => "SID",
            "Smb" => "SMB",
            "Tgt" => "TGT",
            "Url" => "URL",
            "Wec" => "WEC",
            "Xml" => "XML",
            _ => word
        }));
    }
}
