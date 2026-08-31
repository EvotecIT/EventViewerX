using System.Collections.Concurrent;
using System.Reflection;

namespace EventViewerX.Reporting;

internal static class EventReportProjectionFactory {
    private static readonly ConcurrentDictionary<(Type RecordType, EventType EventType), TypedProjectionPlan> TypedPlans = new();
    private static readonly EventReportSectionDefinition GroupPolicyAuditSection = CreateSectionDefinition(
        EventReportSectionKind.Custom,
        "GroupPolicyAudit",
        "Group Policy audit",
        "Group Policy directory changes enriched with event-time and current persistent context.",
        new[] {
            Column<GroupPolicyAuditEventKind>(nameof(GroupPolicyAuditRecord.Kind)),
            Column<string>(nameof(GroupPolicyAuditRecord.ObjectDistinguishedName)),
            Column<string>(nameof(GroupPolicyAuditRecord.OldObjectDistinguishedName)),
            Column<string>(nameof(GroupPolicyAuditRecord.NewObjectDistinguishedName)),
            Column<Guid?>(nameof(GroupPolicyAuditRecord.ObjectGuid)),
            Column<string>(nameof(GroupPolicyAuditRecord.ObjectClass)),
            Column<string>(nameof(GroupPolicyAuditRecord.AttributeName)),
            Column<string>(nameof(GroupPolicyAuditRecord.AttributeValue)),
            Column<string>(nameof(GroupPolicyAuditRecord.OperationType)),
            Column<string>(nameof(GroupPolicyAuditRecord.ActorSid)),
            Column<string>(nameof(GroupPolicyAuditRecord.Actor)),
            Column<string>(nameof(GroupPolicyAuditRecord.ActorLogonId)),
            Column<string>(nameof(GroupPolicyAuditRecord.DirectoryServiceName)),
            Column<string>(nameof(GroupPolicyAuditRecord.DirectoryServiceType)),
            Column<string>(nameof(GroupPolicyAuditRecord.OperationCorrelationId)),
            Column<string>(nameof(GroupPolicyAuditRecord.ApplicationCorrelationId)),
            Column<Guid?>(nameof(GroupPolicyAuditRecord.GroupPolicyId)),
            Column<GroupPolicyAuditTargetKind>(nameof(GroupPolicyAuditRecord.TargetKind)),
            Column<EventContextState>(nameof(GroupPolicyAuditRecord.ContextState)),
            Column<string>(nameof(GroupPolicyAuditRecord.GroupPolicyNameAtEventTime)),
            Column<string>(nameof(GroupPolicyAuditRecord.GroupPolicyLastKnownName)),
            Column<string>(nameof(GroupPolicyAuditRecord.GroupPolicyCurrentName)),
            Column<string>(nameof(GroupPolicyAuditRecord.ContextReason))
        });
    private static readonly EventReportSectionDefinition DetectionFindingSection = CreateSectionDefinition(
        EventReportSectionKind.Custom,
        "DetectionFinding",
        "Detection findings",
        "Explainable native and Sigma findings with pack provenance and stable evidence identities.",
        new[] {
            Column<string>(nameof(EventDetectionFinding.RuleId)),
            Column<string>(nameof(EventDetectionFinding.RuleVersion)),
            Column<string>(nameof(EventDetectionFinding.PackId)),
            Column<string>(nameof(EventDetectionFinding.PackVersion)),
            Column<string>(nameof(EventDetectionFinding.SourceKind)),
            Column<string>(nameof(EventDetectionFinding.SourceId)),
            Column<string>(nameof(EventDetectionFinding.SourceStatus)),
            Column<string>(nameof(EventDetectionFinding.SourceHash)),
            Column<string>(nameof(EventDetectionFinding.License)),
            Column<string>(nameof(EventDetectionFinding.Title)),
            Column<EventDetectionSeverity>(nameof(EventDetectionFinding.Severity)),
            Column<int>(nameof(EventDetectionFinding.Confidence)),
            Column<EventDetectionFindingStatus>(nameof(EventDetectionFinding.Status)),
            Column<DateTime>(nameof(EventDetectionFinding.StartTimeUtc)),
            Column<DateTime>(nameof(EventDetectionFinding.EndTimeUtc)),
            Column<int>("EvidenceCount"),
            Column<string>(nameof(EventDetectionFinding.EvidenceIdentities)),
            Column<string>(nameof(EventDetectionFinding.Tags)),
            Column<string>(nameof(EventDetectionFinding.Entities)),
            Column<bool>("CoverageDeclared"),
            Column<bool>("CoverageComplete"),
            Column<string>("MissingCoverage"),
            Column<string>("CoverageFailures"),
            Column<string>(nameof(EventDetectionFinding.Explanation)),
            Column<string>(nameof(EventDetectionFinding.FalsePositives)),
            Column<string>(nameof(EventDetectionFinding.CompletenessDiagnostic))
        });
    private static readonly EventReportSectionDefinition TimelineSection = CreateSectionDefinition(
        EventReportSectionKind.Custom,
        "IncidentTimeline",
        "Incident timeline",
        "Ordered evidence and findings with source, receive, and processing clocks.",
        new[] {
            Column<EventTimelineEntryKind>(nameof(EventTimelineEntry.Kind)),
            Column<string>(nameof(EventTimelineEntry.Identity)),
            Column<string>(nameof(EventTimelineEntry.Title)),
            Column<DateTime>(nameof(EventTimelineEntry.EventTimeUtc)),
            Column<DateTime>(nameof(EventTimelineEntry.ReceivedTimeUtc)),
            Column<DateTime>(nameof(EventTimelineEntry.ProcessedTimeUtc)),
            Column<string>(nameof(EventTimelineEntry.RuleId)),
            Column<EventDetectionSeverity?>(nameof(EventTimelineEntry.Severity)),
            Column<string>(nameof(EventTimelineEntry.EvidenceIdentities)),
            Column<string>(nameof(EventTimelineEntry.Pivots))
        });
    private static readonly EventReportSectionDefinition DecisionMetricSection = CreateSectionDefinition(
        EventReportSectionKind.Custom,
        "DecisionMetric",
        "Decision metrics",
        "Profile-specific counts, rates, and completeness indicators.",
        new[] {
            Column<string>(nameof(EventDecisionMetric.Name)),
            Column<string>(nameof(EventDecisionMetric.DisplayName)),
            Column<double>(nameof(EventDecisionMetric.Value)),
            Column<string>(nameof(EventDecisionMetric.Unit)),
            Column<string>(nameof(EventDecisionMetric.Description))
        });
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

    internal static EventReportProjection Create(GroupPolicyAuditRecord record) {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            [nameof(record.Kind)] = record.Kind,
            [nameof(record.ObjectDistinguishedName)] = record.ObjectDistinguishedName,
            [nameof(record.OldObjectDistinguishedName)] = record.OldObjectDistinguishedName,
            [nameof(record.NewObjectDistinguishedName)] = record.NewObjectDistinguishedName,
            [nameof(record.ObjectGuid)] = record.ObjectGuid,
            [nameof(record.ObjectClass)] = record.ObjectClass,
            [nameof(record.AttributeName)] = record.AttributeName,
            [nameof(record.AttributeValue)] = record.AttributeValue,
            [nameof(record.OperationType)] = record.OperationType,
            [nameof(record.ActorSid)] = record.ActorSid,
            [nameof(record.Actor)] = record.Actor,
            [nameof(record.ActorLogonId)] = record.ActorLogonId,
            [nameof(record.DirectoryServiceName)] = record.DirectoryServiceName,
            [nameof(record.DirectoryServiceType)] = record.DirectoryServiceType,
            [nameof(record.OperationCorrelationId)] = record.OperationCorrelationId,
            [nameof(record.ApplicationCorrelationId)] = record.ApplicationCorrelationId,
            [nameof(record.GroupPolicyId)] = record.GroupPolicyId,
            [nameof(record.TargetKind)] = record.TargetKind,
            [nameof(record.ContextState)] = record.ContextState,
            [nameof(record.GroupPolicyNameAtEventTime)] = record.GroupPolicyNameAtEventTime,
            [nameof(record.GroupPolicyLastKnownName)] = record.GroupPolicyLastKnownName,
            [nameof(record.GroupPolicyCurrentName)] = record.GroupPolicyCurrentName,
            [nameof(record.ContextReason)] = record.ContextReason
        };
        var row = new EventReportRow {
            TimeCreated = record.TimeCreatedUtc,
            Type = "GroupPolicyAudit",
            EventId = record.EventId,
            RecordId = record.RecordId,
            ActivityId = record.ActivityId,
            RelatedActivityId = record.RelatedActivityId,
            ProcessId = record.ProcessId,
            ThreadId = record.ThreadId,
            Provider = "Microsoft-Windows-Security-Auditing",
            SourceLog = record.OriginalLogName,
            ContainerLog = record.ContainerLogName,
            SourceKind = record.QuerySourceKind,
            SourceComputer = record.SourceComputer,
            CollectorComputer = record.QueryTarget,
            Message = record.Message,
            Values = values
        };
        EventValueNormalizationEngine.Populate(row);
        return new EventReportProjection(row, GroupPolicyAuditSection);
    }

    internal static EventReportProjection Create(EventDetectionFinding finding) {
        EventObservation evidence = finding.Evidence.First();
        EventObject source = evidence.SourceEvent;
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            [nameof(finding.RuleId)] = finding.RuleId,
            [nameof(finding.RuleVersion)] = finding.RuleVersion,
            [nameof(finding.PackId)] = finding.PackId,
            [nameof(finding.PackVersion)] = finding.PackVersion,
            [nameof(finding.SourceKind)] = finding.SourceKind,
            [nameof(finding.SourceId)] = finding.SourceId,
            [nameof(finding.SourceStatus)] = finding.SourceStatus,
            [nameof(finding.SourceHash)] = finding.SourceHash,
            [nameof(finding.License)] = finding.License,
            [nameof(finding.Title)] = finding.Title,
            [nameof(finding.Severity)] = finding.Severity,
            [nameof(finding.Confidence)] = finding.Confidence,
            [nameof(finding.Status)] = finding.Status,
            [nameof(finding.StartTimeUtc)] = finding.StartTimeUtc,
            [nameof(finding.EndTimeUtc)] = finding.EndTimeUtc,
            ["EvidenceCount"] = finding.Evidence.Count,
            [nameof(finding.EvidenceIdentities)] = string.Join(", ", finding.EvidenceIdentities),
            [nameof(finding.Tags)] = string.Join(", ", finding.Tags),
            [nameof(finding.Entities)] = string.Join(", ", finding.Entities.Select(static item => item.Key + "=" + item.Value)),
            ["CoverageDeclared"] = finding.Coverage.IsDeclared,
            ["CoverageComplete"] = finding.Coverage.IsComplete,
            ["MissingCoverage"] = FormatMissingCoverage(finding.Coverage),
            ["CoverageFailures"] = string.Join("; ", finding.Coverage.Failures),
            [nameof(finding.Explanation)] = finding.Explanation,
            [nameof(finding.FalsePositives)] = string.Join("; ", finding.FalsePositives),
            [nameof(finding.CompletenessDiagnostic)] = finding.CompletenessDiagnostic
        };
        EventReportRow row = CreateRow(source, "DetectionFinding", values);
        row.TimeCreated = finding.StartTimeUtc;
        row.Message = finding.Explanation;
        return new EventReportProjection(row, DetectionFindingSection);
    }

    private static string FormatMissingCoverage(EventDetectionCoverage coverage) {
        var missing = new List<string>();
        missing.AddRange(coverage.MissingTargets.Select(static value => "target:" + value));
        missing.AddRange(coverage.MissingChannels.Select(static value => "channel:" + value));
        missing.AddRange(coverage.MissingProviders.Select(static value => "provider:" + value));
        missing.AddRange(coverage.MissingEventIds.Select(static value =>
            "event-id:" + value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        missing.AddRange(coverage.MissingEventTypes.Select(static value => "type:" + value));
        return string.Join(", ", missing);
    }

    internal static EventReportProjection Create(EventTimelineEntry entry) {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            [nameof(entry.Kind)] = entry.Kind,
            [nameof(entry.Identity)] = entry.Identity,
            [nameof(entry.Title)] = entry.Title,
            [nameof(entry.EventTimeUtc)] = entry.EventTimeUtc,
            [nameof(entry.ReceivedTimeUtc)] = entry.ReceivedTimeUtc,
            [nameof(entry.ProcessedTimeUtc)] = entry.ProcessedTimeUtc,
            [nameof(entry.RuleId)] = entry.RuleId,
            [nameof(entry.Severity)] = entry.Severity,
            [nameof(entry.EvidenceIdentities)] = string.Join(", ", entry.EvidenceIdentities),
            [nameof(entry.Pivots)] = string.Join(", ", entry.Pivots.Select(static pivot =>
                pivot.Kind + ":" + pivot.Value))
        };
        var row = new EventReportRow {
            TimeCreated = entry.EventTimeUtc,
            Type = "IncidentTimeline",
            Message = entry.Title,
            Values = values
        };
        EventValueNormalizationEngine.Populate(row);
        return new EventReportProjection(row, TimelineSection);
    }

    internal static EventReportProjection Create(EventDecisionMetric metric) {
        var row = new EventReportRow {
            TimeCreated = DateTime.UtcNow,
            Type = "DecisionMetric",
            Message = metric.Description,
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                [nameof(metric.Name)] = metric.Name,
                [nameof(metric.DisplayName)] = metric.DisplayName,
                [nameof(metric.Value)] = metric.Value,
                [nameof(metric.Unit)] = metric.Unit,
                [nameof(metric.Description)] = metric.Description
            }
        };
        EventValueNormalizationEngine.Populate(row);
        return new EventReportProjection(row, DecisionMetricSection);
    }

    internal static EventReportSectionDefinition CreateGroupPolicyAuditDefinition() => GroupPolicyAuditSection;

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

    private static EventReportColumn Column<T>(string name) => new(
        name,
        EventReportTableProjection.SplitWords(name),
        typeof(T));

    private static EventReportRow CreateRow(
        EventObject source,
        string type,
        IReadOnlyDictionary<string, object?> values) {

        var row = new EventReportRow {
            TimeCreated = source.TimeCreated,
            ObservationIdentity = EventCheckpointBoundaryIdentity.Create(source),
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
            ActivityId = source.ActivityId,
            RelatedActivityId = source.RelatedActivityId,
            ProcessId = source.ProcessId,
            ThreadId = source.ThreadId,
            Message = source.Message,
            Values = values
        };
        EventValueNormalizationEngine.Populate(row);
        return row;
    }

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
