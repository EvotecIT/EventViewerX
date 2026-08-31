using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace EventViewerX;

/// <summary>Canonical report-neutral event view consumed by detection and correlation engines.</summary>
public sealed class EventObservation {
    private static readonly ConcurrentDictionary<Type, TypedMemberAccessor[]> TypedMemberAccessors = new();

    private EventObservation(
        EventObject source,
        string identity,
        string typeName,
        DateTime receivedTimeUtc,
        DateTime processedTimeUtc,
        IReadOnlyDictionary<string, object?> fields) {

        SourceEvent = source;
        Identity = identity;
        TypeName = typeName;
        EventId = source.Id;
        RecordId = source.RecordId;
        ProviderName = source.ProviderName;
        SourceLog = source.OriginalLogName;
        ContainerLog = source.ContainerLogName;
        SourceComputer = source.SourceComputer;
        CollectorComputer = source.CollectorComputer;
        EventTimeUtc = source.TimeCreated.ToUniversalTime();
        ReceivedTimeUtc = receivedTimeUtc;
        ProcessedTimeUtc = processedTimeUtc;
        Fields = fields;
    }

    /// <summary>Creates a canonical observation from a raw event and optional typed projection.</summary>
    public static EventObservation Create(
        EventObject source,
        EventTypeRecord? typedRecord = null,
        DateTime? receivedTimeUtc = null,
        DateTime? processedTimeUtc = null) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        if (typedRecord != null && !ReferenceEquals(typedRecord.SourceEvent, source)) {
            throw new ArgumentException("The typed record must be projected from the supplied source event.", nameof(typedRecord));
        }
        DateTime received = (receivedTimeUtc ?? DateTime.UtcNow).ToUniversalTime();
        DateTime processed = (processedTimeUtc ?? DateTime.UtcNow).ToUniversalTime();
        if (processed < received) {
            throw new ArgumentException("Processed time cannot be earlier than received time.", nameof(processedTimeUtc));
        }

        string? candidateTypeName = typedRecord?.TypeName;
        string typeName = string.IsNullOrWhiteSpace(candidateTypeName)
            ? typedRecord?.GetType().Name ?? "Generic"
            : candidateTypeName!;
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> item in source.Data) {
            fields[item.Key] = item.Value;
        }
        if (typedRecord != null) {
            AddTypedMembers(fields, typedRecord);
        }
        AddCanonicalAliases(fields, source);
        string identity = EventCheckpointBoundaryIdentity.Create(source);
        AddCanonicalFields(fields, source, identity, typeName);
        return new EventObservation(
            source,
            identity,
            typeName,
            received,
            processed,
            new ReadOnlyDictionary<string, object?>(fields));
    }

    internal static EventObservation Restore(
        EventObject source,
        string identity,
        string typeName,
        IReadOnlyDictionary<string, object?> storedFields,
        DateTime receivedTimeUtc,
        DateTime processedTimeUtc) {

        if (string.IsNullOrWhiteSpace(identity)) {
            throw new ArgumentException("A restored observation requires its stable identity.", nameof(identity));
        }
        if (string.IsNullOrWhiteSpace(typeName)) {
            throw new ArgumentException("A restored observation requires its stable type name.", nameof(typeName));
        }
        EventObservation baseline = Create(source, receivedTimeUtc: receivedTimeUtc, processedTimeUtc: processedTimeUtc);
        var fields = baseline.Fields.ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> field in storedFields) {
            fields[field.Key] = field.Value;
        }
        string restoredIdentity = identity.Trim();
        string restoredTypeName = typeName.Trim();
        AddCanonicalFields(fields, source, restoredIdentity, restoredTypeName);
        return new EventObservation(
            source,
            restoredIdentity,
            restoredTypeName,
            receivedTimeUtc.ToUniversalTime(),
            processedTimeUtc.ToUniversalTime(),
            new ReadOnlyDictionary<string, object?>(fields));
    }

    /// <summary>Stable SHA-256 evidence identity.</summary>
    public string Identity { get; }
    /// <summary>Projected EventViewerX type name, or Generic for a raw event.</summary>
    public string TypeName { get; }
    /// <summary>Windows event identifier.</summary>
    public int EventId { get; }
    /// <summary>Source event record identifier.</summary>
    public long? RecordId { get; }
    /// <summary>Provider that emitted the event.</summary>
    public string ProviderName { get; }
    /// <summary>Original source channel.</summary>
    public string SourceLog { get; }
    /// <summary>Channel or file containing the event.</summary>
    public string ContainerLog { get; }
    /// <summary>Computer that emitted the event.</summary>
    public string SourceComputer { get; }
    /// <summary>Collector or direct query target.</summary>
    public string CollectorComputer { get; }
    /// <summary>UTC source-event time.</summary>
    public DateTime EventTimeUtc { get; }
    /// <summary>UTC time at which a host received the event.</summary>
    public DateTime ReceivedTimeUtc { get; }
    /// <summary>UTC time at which detection processing began.</summary>
    public DateTime ProcessedTimeUtc { get; }
    /// <summary>Case-insensitive canonical and typed fields.</summary>
    public IReadOnlyDictionary<string, object?> Fields { get; }
    /// <summary>Original detached event snapshot for evidence drill-down.</summary>
    public EventObject SourceEvent { get; }

    private static void AddTypedMembers(IDictionary<string, object?> fields, EventTypeRecord typedRecord) {
        TypedMemberAccessor[] accessors = TypedMemberAccessors.GetOrAdd(
            typedRecord.GetType(),
            CreateTypedMemberAccessors);
        foreach (TypedMemberAccessor accessor in accessors) {
            fields[accessor.Name] = accessor.GetValue(typedRecord);
        }
    }

    private static void AddCanonicalAliases(IDictionary<string, object?> fields, EventObject source) {
        AddAccountAlias(fields, source, "Who", "SubjectDomainName", "SubjectUserName");
        AddAccountAlias(fields, source, "ObjectAffected", "TargetDomainName", "TargetUserName");
        if (!fields.ContainsKey("IpAddress")) {
            string address = source.GetDataValueOrEmpty(KnownEventField.IpAddress);
            if (address.Length == 0) {
                address = source.GetDataValueOrEmpty("SourceNetworkAddress");
            }
            if (address.Length != 0) {
                fields["IpAddress"] = address;
            }
        }
    }

    private static void AddCanonicalFields(
        IDictionary<string, object?> fields,
        EventObject source,
        string identity,
        string typeName) {

        fields["Identity"] = identity;
        fields["Type"] = typeName;
        fields["TypeName"] = typeName;
        fields["EventId"] = source.Id;
        fields["Id"] = source.Id;
        fields["RecordId"] = source.RecordId;
        fields["ProviderName"] = source.ProviderName;
        fields["Provider"] = source.ProviderName;
        fields["SourceLog"] = source.OriginalLogName;
        fields["LogName"] = source.OriginalLogName;
        fields["ContainerLog"] = source.ContainerLogName;
        fields["SourceComputer"] = source.SourceComputer;
        fields["Computer"] = source.SourceComputer;
        fields["CollectorComputer"] = source.CollectorComputer;
        fields["ActivityId"] = source.ActivityId;
        fields["RelatedActivityId"] = source.RelatedActivityId;
        fields["ProcessId"] = source.ProcessId;
        fields["ThreadId"] = source.ThreadId;
        fields["Message"] = source.Message;
        fields["EventTimeUtc"] = source.TimeCreated.ToUniversalTime();
        fields["TimeCreated"] = source.TimeCreated.ToUniversalTime();
    }

    private static void AddAccountAlias(
        IDictionary<string, object?> fields,
        EventObject source,
        string alias,
        string domainField,
        string accountField) {

        if (fields.ContainsKey(alias)) {
            return;
        }
        string account = source.GetDataValueOrEmpty(accountField);
        if (account.Length == 0) {
            return;
        }
        string domain = source.GetDataValueOrEmpty(domainField);
        fields[alias] = domain.Length == 0 ? account : domain + "\\" + account;
    }

    private static TypedMemberAccessor[] CreateTypedMemberAccessors(Type type) {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        IEnumerable<MemberInfo> properties = type.GetProperties(flags)
            .Where(static property =>
                property.GetIndexParameters().Length == 0 &&
                !string.Equals(property.Name, nameof(EventTypeRecord.SourceEvent), StringComparison.Ordinal) &&
                !string.Equals(property.Name, nameof(EventTypeRecord.Message), StringComparison.Ordinal));
        IEnumerable<MemberInfo> fields = type.GetFields(flags);
        return properties.Concat(fields)
            .Select(member => new TypedMemberAccessor(member.Name, CreateTypedMemberGetter(type, member)))
            .ToArray();
    }

    private static Func<EventTypeRecord, object?> CreateTypedMemberGetter(Type type, MemberInfo member) {
        ParameterExpression record = Expression.Parameter(typeof(EventTypeRecord), "record");
        UnaryExpression typed = Expression.Convert(record, type);
        Expression access = member is PropertyInfo property
            ? Expression.Property(typed, property)
            : Expression.Field(typed, (FieldInfo)member);
        UnaryExpression boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<EventTypeRecord, object?>>(boxed, record).Compile();
    }

    private sealed class TypedMemberAccessor {
        internal TypedMemberAccessor(string name, Func<EventTypeRecord, object?> getValue) {
            Name = name;
            GetValue = getValue;
        }

        internal string Name { get; }
        internal Func<EventTypeRecord, object?> GetValue { get; }
    }
}
