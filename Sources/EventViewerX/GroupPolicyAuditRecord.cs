using System.Text.RegularExpressions;

namespace EventViewerX;

/// <summary>
/// Source-neutral Group Policy audit event read directly from a domain controller, from Windows Event
/// Collector, or from an offline event-log file.
/// </summary>
public sealed class GroupPolicyAuditRecord {
    private static readonly Regex GroupPolicyIdPattern = new(
        @"CN=\{(?<id>[0-9A-Fa-f-]{36})\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal GroupPolicyAuditRecord(CustomEventRecord record) {
        EventObject source = record.SourceEvent;
        EventId = source.Id;
        Kind = (GroupPolicyAuditEventKind)source.Id;
        TimeCreatedUtc = source.TimeCreated.Kind == DateTimeKind.Utc
            ? source.TimeCreated
            : source.TimeCreated.ToUniversalTime();
        RecordId = source.RecordId;
        ActivityId = source.ActivityId;
        RelatedActivityId = source.RelatedActivityId;
        SourceComputer = source.SourceComputer;
        QueryTarget = string.IsNullOrWhiteSpace(source.CollectorComputer)
            ? Environment.MachineName
            : source.CollectorComputer;
        QuerySourceKind = source.QuerySourceKind;
        OriginalLogName = source.OriginalLogName;
        ContainerLogName = string.IsNullOrWhiteSpace(source.ContainerLogName)
            ? source.GatheredLogName
            : source.ContainerLogName;
        BookmarkXml = source.BookmarkXml;
        OldObjectDistinguishedName = Value(record, "OldObjectDistinguishedName");
        NewObjectDistinguishedName = Value(record, "NewObjectDistinguishedName");
        ObjectDistinguishedName = FirstValue(
            NewObjectDistinguishedName,
            Value(record, "ObjectDistinguishedName"),
            OldObjectDistinguishedName);
        ObjectGuid = ParseGuid(Value(record, "ObjectGuid"));
        ObjectClass = Value(record, "ObjectClass");
        AttributeName = Value(record, "AttributeName");
        AttributeValue = Value(record, "AttributeValue");
        OperationType = Value(record, "OperationType");
        ActorSid = Value(record, "ActorSid");
        ActorUserName = Value(record, "ActorUserName");
        ActorDomainName = Value(record, "ActorDomainName");
        ActorLogonId = Value(record, "ActorLogonId");
        DirectoryServiceName = Value(record, "DirectoryServiceName");
        DirectoryServiceType = Value(record, "DirectoryServiceType");
        OperationCorrelationId = Value(record, "OperationCorrelationId");
        ApplicationCorrelationId = Value(record, "ApplicationCorrelationId");
        GroupPolicyId = string.Equals(
                ObjectClass,
                "groupPolicyContainer",
                StringComparison.OrdinalIgnoreCase)
            ? ParseGroupPolicyId(ObjectDistinguishedName)
            : null;
        TargetKind = ResolveTargetKind(ObjectClass, AttributeName);
    }

    /// <summary>Windows Security event identifier.</summary>
    public int EventId { get; }

    /// <summary>Directory operation represented by the event.</summary>
    public GroupPolicyAuditEventKind Kind { get; }

    /// <summary>UTC time recorded by the event source.</summary>
    public DateTime TimeCreatedUtc { get; }

    /// <summary>Record identifier exposed by the event.</summary>
    public long? RecordId { get; }

    /// <summary>Windows system activity identifier retained from the source event.</summary>
    public Guid? ActivityId { get; }

    /// <summary>Windows system related-activity identifier retained from the source event.</summary>
    public Guid? RelatedActivityId { get; }

    /// <summary>Domain controller that emitted the event.</summary>
    public string SourceComputer { get; }

    /// <summary>Computer or offline file from which the event was queried.</summary>
    public string QueryTarget { get; }

    /// <summary>Whether the record came from a live channel or an offline event-log file.</summary>
    public EventLogQuerySourceKind QuerySourceKind { get; }

    /// <summary>Original event channel, normally Security.</summary>
    public string OriginalLogName { get; }

    /// <summary>Container channel or file, for example Security or ForwardedEvents.</summary>
    public string ContainerLogName { get; }

    /// <summary>Portable bookmark for the container position.</summary>
    public string? BookmarkXml { get; }

    /// <summary>Affected directory object distinguished name.</summary>
    public string ObjectDistinguishedName { get; }

    /// <summary>Previous distinguished name for a moved directory object.</summary>
    public string OldObjectDistinguishedName { get; }

    /// <summary>New distinguished name for a moved directory object.</summary>
    public string NewObjectDistinguishedName { get; }

    /// <summary>Affected directory object GUID, when supplied by the event.</summary>
    public Guid? ObjectGuid { get; }

    /// <summary>Affected directory object class.</summary>
    public string ObjectClass { get; }

    /// <summary>LDAP display name of the affected attribute.</summary>
    public string AttributeName { get; }

    /// <summary>Attribute value emitted by the audit provider.</summary>
    public string AttributeValue { get; }

    /// <summary>Raw provider operation value.</summary>
    public string OperationType { get; }

    /// <summary>SID of the account that performed the operation.</summary>
    public string ActorSid { get; }

    /// <summary>User name of the account that performed the operation.</summary>
    public string ActorUserName { get; }

    /// <summary>Domain of the account that performed the operation.</summary>
    public string ActorDomainName { get; }

    /// <summary>Logon identifier of the account session that performed the operation.</summary>
    public string ActorLogonId { get; }

    /// <summary>Directory service name emitted by the provider.</summary>
    public string DirectoryServiceName { get; }

    /// <summary>Raw directory service type emitted by the provider.</summary>
    public string DirectoryServiceType { get; }

    /// <summary>Provider operation correlation identifier.</summary>
    public string OperationCorrelationId { get; }

    /// <summary>Application correlation identifier, when supplied.</summary>
    public string ApplicationCorrelationId { get; }

    /// <summary>Canonical Group Policy identifier parsed from a Group Policy container DN.</summary>
    public Guid? GroupPolicyId { get; }

    /// <summary>Group Policy surface affected by the event.</summary>
    public GroupPolicyAuditTargetKind TargetKind { get; }

    /// <summary>Persistent context state at the event time.</summary>
    public EventContextState ContextState { get; private set; } = EventContextState.Unknown;

    /// <summary>Best known Group Policy name at the event time.</summary>
    public string? GroupPolicyNameAtEventTime { get; private set; }

    /// <summary>Last known historical name without implying that a deleted object is current.</summary>
    public string? GroupPolicyLastKnownName { get; private set; }

    /// <summary>Current name only when the latest stored context is live and unambiguous.</summary>
    public string? GroupPolicyCurrentName { get; private set; }

    /// <summary>Explanation when persistent context is unknown or ambiguous.</summary>
    public string? ContextReason { get; private set; }

    /// <summary>Domain-qualified actor name when both components are available.</summary>
    public string Actor => string.IsNullOrWhiteSpace(ActorDomainName)
        ? ActorUserName
        : ActorDomainName + "\\" + ActorUserName;

    /// <summary>Stable source key for checkpoint lookup.</summary>
    public string SourceKey => GroupPolicyAuditCheckpoint.CreateSourceKey(QueryTarget, ContainerLogName);

    internal void ApplyContext(EventContextResolution resolution) {
        ContextState = resolution.State;
        GroupPolicyNameAtEventTime = resolution.NameAtEventTime;
        GroupPolicyLastKnownName = resolution.LastKnownName;
        GroupPolicyCurrentName = resolution.CurrentName;
        ContextReason = resolution.Reason;
    }

    private static string Value(CustomEventRecord record, string name) {
        return record.Values.TryGetValue(name, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static Guid? ParseGuid(string value) {
        return Guid.TryParse(value.Trim('{', '}'), out Guid result) ? result : null;
    }

    private static Guid? ParseGroupPolicyId(string distinguishedName) {
        Match match = GroupPolicyIdPattern.Match(distinguishedName ?? string.Empty);
        return match.Success && Guid.TryParse(match.Groups["id"].Value, out Guid result)
            ? result
            : null;
    }

    private static string FirstValue(params string[] values) {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static GroupPolicyAuditTargetKind ResolveTargetKind(string objectClass, string attributeName) {
        if (string.Equals(attributeName, "gPLink", StringComparison.OrdinalIgnoreCase)) {
            return GroupPolicyAuditTargetKind.ScopeLinks;
        }
        if (string.Equals(attributeName, "gPOptions", StringComparison.OrdinalIgnoreCase)) {
            return GroupPolicyAuditTargetKind.ScopeInheritance;
        }
        if (string.Equals(attributeName, "gPCWQLFilter", StringComparison.OrdinalIgnoreCase)) {
            return GroupPolicyAuditTargetKind.WmiFilterAssignment;
        }
        if (string.Equals(objectClass, "msWMI-Som", StringComparison.OrdinalIgnoreCase)) {
            return GroupPolicyAuditTargetKind.WmiFilterDefinition;
        }
        return GroupPolicyAuditTargetKind.GroupPolicyObject;
    }
}
