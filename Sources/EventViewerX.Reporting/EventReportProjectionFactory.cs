using System.Collections.Concurrent;
using System.Reflection;

namespace EventViewerX.Reporting;

internal static class EventReportProjectionFactory {
    private static readonly ConcurrentDictionary<(Type RecordType, EventType EventType), TypedProjectionPlan> TypedPlans = new();
    private static readonly HashSet<string> RoutingMembers = new(StringComparer.Ordinal) {
        nameof(IEventRule.EventIds),
        nameof(IEventRule.LogName),
        nameof(IEventRule.Type),
        nameof(EventTypeRecord.SourceEvent),
        nameof(EventTypeRecord.TypeName)
    };

    internal static EventReportProjection Create(EventTypeRecord record) {
        if (record is not IEventRule rule) {
            return Create(record.SourceEvent);
        }
        EventTypeDefinition definition = EventTypeCatalog.GetDefinition(rule.Type);
        TypedProjectionPlan plan = GetTypedPlan(record.GetType(), definition);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (ReportMember member in plan.Members) {
            values[member.Name] = member.GetValue(record);
        }
        EventReportRow row = CreateRow(record.SourceEvent, definition.Name, values);
        return new EventReportProjection(row, plan.Section);
    }

    internal static EventReportProjection Create(CustomEventRecord record) {
        EventDefinition definition = record.Definition;
        EventReportRow row = CreateRow(record.SourceEvent, record.TypeName, record.Values);
        return new EventReportProjection(row, Create(definition));
    }

    internal static EventReportProjection Create(EventObject source) {
        EventReportRow row = CreateRow(
            source,
            "Generic",
            source.Data.ToDictionary(static item => item.Key, static item => (object?)item.Value,
                StringComparer.OrdinalIgnoreCase));
        return new EventReportProjection(row, CreateGenericDefinition());
    }

    internal static EventReportSectionDefinition Create(EventType type) {
        EventTypeDefinition definition = EventTypeCatalog.GetDefinition(type);
        if (definition.IsComposite || definition.RecordType == null) {
            throw new ArgumentException(
                $"Event type '{type}' does not identify one reportable leaf definition.",
                nameof(type));
        }
        return Create(definition.RecordType, definition);
    }

    internal static EventReportSectionDefinition Create(
        Type recordType,
        EventTypeDefinition definition) => GetTypedPlan(recordType, definition).Section;

    internal static EventReportSectionDefinition Create(EventDefinition definition) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        definition.Validate();
        EventReportColumn[] columns = definition.Fields.Select(static field => new EventReportColumn(
            field.Name,
            string.IsNullOrWhiteSpace(field.DisplayName)
                ? EventReportTableProjection.SplitWords(field.Name)
                : field.DisplayName.Trim(),
            field.ValueType,
            field.Aliases)).ToArray();
        string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? EventReportTableProjection.SplitWords(definition.Name)
            : definition.DisplayName.Trim();
        return CreateSectionDefinition(
            EventReportSectionKind.Custom,
            definition.Name,
            displayName,
            definition.Description?.Trim() ?? string.Empty,
            columns);
    }

    internal static IReadOnlyList<EventReportSectionDefinition> CreateDefinitions(EventReportRequest request) {
        if (request.Types != null && request.Types.Count > 0) {
            return EventTypeCatalog.Expand(request.Types).Select(Create).ToArray();
        }
        if (request.Definition != null) {
            return new[] { Create(request.Definition) };
        }
        return new[] { CreateGenericDefinition() };
    }

    internal static EventReportSectionDefinition CreateGenericDefinition() => new(
            "Generic",
            "Generic",
            "Events",
            "Raw Windows Event Log records with provider and channel metadata.",
            EventReportSectionKind.Generic,
            EventReportTableProjection.BuildGenericColumns(Array.Empty<EventReportRow>()));

    internal static EventReportSectionDefinition CreateSectionDefinition(
        EventReportSectionKind kind,
        string name,
        string displayName,
        string description,
        IReadOnlyList<EventReportColumn> columns) {

        string signature = string.Join("|", columns.Select(static column =>
            column.Name + ":" +
            EventReportColumnSchema.GetStableTypeName(column.ValueType) + ":" +
            string.Join(",", column.Aliases.OrderBy(static alias => alias, StringComparer.OrdinalIgnoreCase))));
        return new EventReportSectionDefinition(
            $"{kind}:{name}:{signature}", name, displayName, description, kind, columns);
    }

    private static EventReportRow CreateRow(
        EventObject source,
        string type,
        IReadOnlyDictionary<string, object?> values) => new() {
            TimeCreated = source.TimeCreated,
            Type = type,
            EventId = source.Id,
            RecordId = source.RecordId,
            Provider = source.ProviderName,
            SourceLog = source.OriginalLogName,
            ContainerLog = source.ContainerLogName,
            SourceKind = source.QuerySourceKind,
            SourceComputer = source.SourceComputer,
            CollectorComputer = source.CollectorComputer,
            Level = source.LevelDisplayName,
            LevelValue = source.Level,
            Message = source.Message,
            Values = values
        };

    private static TypedProjectionPlan BuildPlan(Type recordType, EventTypeDefinition definition) {
        ReportMember[] members = BuildMembers(recordType);
        var fields = definition.Fields.ToDictionary(static field => field.Name, StringComparer.OrdinalIgnoreCase);
        EventReportColumn[] columns = members.Select(member => new EventReportColumn(
            member.Name,
            fields.TryGetValue(member.Name, out EventFieldDefinition? field)
                ? field.DisplayName
                : EventReportTableProjection.SplitWords(member.Name),
            member.ValueType,
            field?.Aliases)).ToArray();
        EventReportSectionDefinition section = CreateSectionDefinition(
            EventReportSectionKind.Typed,
            definition.Name,
            definition.DisplayName,
            definition.Description,
            columns);
        return new TypedProjectionPlan(members, section);
    }

    private static TypedProjectionPlan GetTypedPlan(Type recordType, EventTypeDefinition definition) =>
        TypedPlans.GetOrAdd(
            (recordType, definition.Type),
            _ => BuildPlan(recordType, definition));

    private static ReportMember[] BuildMembers(Type recordType) {
        var hierarchy = new Stack<Type>();
        for (Type? type = recordType;
             type != null && type != typeof(EventRuleBase) && type != typeof(EventTypeRecord);
             type = type.BaseType) {
            hierarchy.Push(type);
        }
        var members = new List<ReportMember>();
        while (hierarchy.Count > 0) {
            Type type = hierarchy.Pop();
            IEnumerable<MemberInfo> declared = type
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(static member => member is FieldInfo || member is PropertyInfo)
                .Where(static member => !RoutingMembers.Contains(member.Name))
                .Where(static member => member is not PropertyInfo property ||
                    property.CanRead && property.GetIndexParameters().Length == 0)
                .OrderBy(static member => member.MetadataToken);
            members.AddRange(declared.Select(static member => new ReportMember(member)));
        }
        return members
            .GroupBy(static member => member.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.Last())
            .ToArray();
    }

    private sealed class TypedProjectionPlan {
        internal TypedProjectionPlan(ReportMember[] members, EventReportSectionDefinition section) {
            Members = members;
            Section = section;
        }

        internal ReportMember[] Members { get; }
        internal EventReportSectionDefinition Section { get; }
    }

    private sealed class ReportMember {
        private readonly FieldInfo? _field;
        private readonly PropertyInfo? _property;

        internal ReportMember(MemberInfo member) {
            _field = member as FieldInfo;
            _property = member as PropertyInfo;
            Name = member.Name;
            ValueType = _field?.FieldType ?? _property!.PropertyType;
        }

        internal string Name { get; }
        internal Type ValueType { get; }
        internal object? GetValue(object instance) => _field != null
            ? _field.GetValue(instance)
            : _property!.GetValue(instance);
    }
}
