namespace EventViewerX.Providers;

/// <summary>One schema compatibility problem between provider versions.</summary>
public sealed class EventProviderCompatibilityIssue {
    /// <summary>Stable machine-readable compatibility code.</summary>
    public string Code { get; internal set; } = string.Empty;
    /// <summary>Candidate definition path associated with the change.</summary>
    public string Path { get; internal set; } = string.Empty;
    /// <summary>Explanation of why the change would break old events.</summary>
    public string Message { get; internal set; } = string.Empty;
}

/// <summary>Compatibility result for a provider package upgrade.</summary>
public sealed class EventProviderCompatibilityResult {
    internal EventProviderCompatibilityResult(
        IReadOnlyList<EventProviderCompatibilityIssue> issues) {

        Issues = issues;
    }

    /// <summary>All breaking compatibility issues.</summary>
    public IReadOnlyList<EventProviderCompatibilityIssue> Issues { get; }
    /// <summary>Whether the candidate can replace the baseline safely.</summary>
    public bool IsCompatible => Issues.Count == 0;
}

/// <summary>
/// Prevents upgrades from changing or removing metadata required to decode
/// previously written events.
/// </summary>
public static class EventProviderCompatibility {
    /// <summary>Compares an installed or baseline definition with a candidate.</summary>
    public static EventProviderCompatibilityResult Compare(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate) {

        if (baseline == null) {
            throw new ArgumentNullException(nameof(baseline));
        }
        if (candidate == null) {
            throw new ArgumentNullException(nameof(candidate));
        }
        EventProviderDefinitionValidator.ValidateOrThrow(baseline);
        EventProviderDefinitionValidator.ValidateOrThrow(candidate);
        var issues = new List<EventProviderCompatibilityIssue>();

        Equal(
            baseline.Name,
            candidate.Name,
            "ProviderNameChanged",
            "Name",
            "Provider name",
            issues);
        Equal(
            baseline.Id,
            candidate.Id,
            "ProviderIdChanged",
            "Id",
            "Provider GUID",
            issues);

        CompareChannels(baseline, candidate, issues);
        CompareMetadata(baseline, candidate, issues);
        CompareMaps(baseline, candidate, issues);
        CompareEvents(baseline, candidate, issues);
        return new EventProviderCompatibilityResult(issues);
    }

    /// <summary>Throws when a candidate would break previously written events.</summary>
    public static void EnsureCompatible(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate) {

        EventProviderCompatibilityResult result =
            Compare(baseline, candidate);
        if (!result.IsCompatible) {
            throw new InvalidOperationException(
                "The provider package is not schema-compatible:" +
                Environment.NewLine +
                string.Join(
                    Environment.NewLine,
                    result.Issues.Select(static issue =>
                        $"[{issue.Code}] {issue.Path}: {issue.Message}")));
        }
    }

    private static void CompareChannels(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate,
        List<EventProviderCompatibilityIssue> issues) {

        foreach (EventProviderChannelDefinition previous in
                 baseline.Channels) {
            EventProviderChannelDefinition? current =
                candidate.Channels.FirstOrDefault(channel =>
                    string.Equals(
                        channel.Id,
                        previous.Id,
                        StringComparison.OrdinalIgnoreCase));
            if (current == null) {
                Add(
                    "ChannelRemoved",
                    $"Channels[{previous.Id}]",
                    $"Previously published channel '{previous.Id}' cannot be removed.",
                    issues);
                continue;
            }
            Equal(
                previous.Id,
                current.Id,
                "ChannelIdChanged",
                $"Channels[{previous.Id}].Id",
                "Channel identifier",
                issues);
            Equal(
                previous.Name,
                current.Name,
                "ChannelNameChanged",
                $"Channels[{previous.Id}].Name",
                "Channel name",
                issues);
            Equal(
                previous.Type,
                current.Type,
                "ChannelTypeChanged",
                $"Channels[{previous.Id}].Type",
                "Channel type",
                issues);
            Equal(
                previous.Isolation,
                current.Isolation,
                "ChannelIsolationChanged",
                $"Channels[{previous.Id}].Isolation",
                "Channel isolation",
                issues);
        }
    }

    private static void CompareMetadata(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate,
        List<EventProviderCompatibilityIssue> issues) {

        CompareNamedValues(
            baseline.Levels,
            candidate.Levels,
            static value => value.Name,
            static value => value.Value,
            "Level",
            issues);
        CompareNamedValues(
            baseline.Tasks,
            candidate.Tasks,
            static value => value.Name,
            static value => value.Value,
            "Task",
            issues);
        CompareNamedValues(
            baseline.Opcodes,
            candidate.Opcodes,
            static value => value.Name,
            static value => value.Value,
            "Opcode",
            issues);
        CompareNamedValues(
            baseline.Keywords,
            candidate.Keywords,
            static value => value.Name,
            static value => value.Mask,
            "Keyword",
            issues);

        foreach (EventProviderTaskDefinition task in baseline.Tasks) {
            EventProviderTaskDefinition? current =
                candidate.Tasks.FirstOrDefault(value =>
                    string.Equals(
                        value.Name,
                        task.Name,
                        StringComparison.OrdinalIgnoreCase));
            if (current == null) {
                continue;
            }
            Equal(
                task.EventGuid,
                current.EventGuid,
                "TaskEventGuidChanged",
                $"Tasks[{task.Name}].EventGuid",
                "Task event GUID",
                issues);
            CompareNamedValues(
                task.Opcodes,
                current.Opcodes,
                static value => value.Name,
                static value => value.Value,
                $"TaskOpcode[{task.Name}]",
                issues);
        }
    }

    private static void CompareMaps(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate,
        List<EventProviderCompatibilityIssue> issues) {

        foreach (EventProviderMapDefinition previous in baseline.Maps) {
            EventProviderMapDefinition? current =
                candidate.Maps.FirstOrDefault(map =>
                    string.Equals(
                        map.Name,
                        previous.Name,
                        StringComparison.OrdinalIgnoreCase));
            if (current == null) {
                Add(
                    "MapRemoved",
                    $"Maps[{previous.Name}]",
                    $"Previously published map '{previous.Name}' cannot be removed.",
                    issues);
                continue;
            }
            Equal(
                previous.Name,
                current.Name,
                "MapNameChanged",
                $"Maps[{previous.Name}].Name",
                "Map name",
                issues);
            Equal(
                previous.Kind,
                current.Kind,
                "MapKindChanged",
                $"Maps[{previous.Name}].Kind",
                "Map kind",
                issues);
            foreach (EventProviderMapEntryDefinition previousEntry in
                     previous.Entries) {
                if (!current.Entries.Any(entry =>
                        entry.Value == previousEntry.Value)) {
                    Add(
                        "MapValueRemoved",
                        $"Maps[{previous.Name}].Entries",
                        $"Previously published map value {previousEntry.Value} cannot be removed.",
                        issues);
                }
            }
        }
    }

    private static void CompareEvents(
        EventProviderDefinition baseline,
        EventProviderDefinition candidate,
        List<EventProviderCompatibilityIssue> issues) {

        foreach (EventProviderEventDefinition previous in baseline.Events) {
            EventProviderEventDefinition? current =
                candidate.Events.FirstOrDefault(item =>
                    item.Id == previous.Id &&
                    item.Version == previous.Version);
            string path = $"Events[{previous.Id}:{previous.Version}]";
            if (current == null) {
                Add(
                    "EventRemoved",
                    path,
                    $"Previously published event {previous.Id} version {previous.Version} cannot be removed.",
                    issues);
                continue;
            }
            Equal(
                previous.Name,
                current.Name,
                "EventNameChanged",
                path + ".Name",
                "Event name",
                issues);
            Equal(
                previous.Channel,
                current.Channel,
                "EventChannelChanged",
                path + ".Channel",
                "Event channel",
                issues);
            Equal(
                previous.Level,
                current.Level,
                "EventLevelChanged",
                path + ".Level",
                "Event level",
                issues);
            Equal(
                previous.Task,
                current.Task,
                "EventTaskChanged",
                path + ".Task",
                "Event task",
                issues);
            Equal(
                previous.Opcode,
                current.Opcode,
                "EventOpcodeChanged",
                path + ".Opcode",
                "Event opcode",
                issues);
            if (!previous.Keywords.OrderBy(
                    static value => value,
                    StringComparer.Ordinal)
                .SequenceEqual(
                    current.Keywords.OrderBy(
                        static value => value,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal)) {
                Add(
                    "EventKeywordsChanged",
                    path + ".Keywords",
                    "Keywords for an existing event identity cannot change.",
                    issues);
            }
            CompareFields(previous, current, path, issues);
        }
    }

    private static void CompareFields(
        EventProviderEventDefinition baseline,
        EventProviderEventDefinition candidate,
        string path,
        List<EventProviderCompatibilityIssue> issues) {

        if (baseline.Fields.Count != candidate.Fields.Count) {
            Add(
                "EventFieldCountChanged",
                path + ".Fields",
                $"Field count changed from {baseline.Fields.Count} to {candidate.Fields.Count}. Publish a new event version instead.",
                issues);
            return;
        }
        for (int index = 0; index < baseline.Fields.Count; index++) {
            EventProviderFieldDefinition previous =
                baseline.Fields[index];
            EventProviderFieldDefinition current =
                candidate.Fields[index];
            string fieldPath = $"{path}.Fields[{index}]";
            Equal(
                previous.Name,
                current.Name,
                "EventFieldNameChanged",
                fieldPath + ".Name",
                "Field name",
                issues);
            Equal(
                previous.Type,
                current.Type,
                "EventFieldTypeChanged",
                fieldPath + ".Type",
                "Field type",
                issues);
            Equal(
                previous.OutputType,
                current.OutputType,
                "EventFieldOutputChanged",
                fieldPath + ".OutputType",
                "Field output type",
                issues);
            Equal(
                previous.CustomOutputType,
                current.CustomOutputType,
                "EventFieldOutputChanged",
                fieldPath + ".CustomOutputType",
                "Custom output type",
                issues);
            Equal(
                previous.Map,
                current.Map,
                "EventFieldMapChanged",
                fieldPath + ".Map",
                "Field map",
                issues);
            Equal(
                previous.Length,
                current.Length,
                "EventFieldLengthChanged",
                fieldPath + ".Length",
                "Field length",
                issues);
            Equal(
                previous.Count,
                current.Count,
                "EventFieldCountChanged",
                fieldPath + ".Count",
                "Field count",
                issues);
        }
    }

    private static void CompareNamedValues<T, TValue>(
        IEnumerable<T> baseline,
        IEnumerable<T> candidate,
        Func<T, string> name,
        Func<T, TValue> value,
        string kind,
        List<EventProviderCompatibilityIssue> issues) {

        foreach (T previous in baseline) {
            string previousName = name(previous);
            T? current = candidate.FirstOrDefault(item =>
                string.Equals(
                    name(item),
                    previousName,
                    StringComparison.OrdinalIgnoreCase));
            if (current == null) {
                Add(
                    kind + "Removed",
                    kind + "s[" + previousName + "]",
                    $"Previously published {kind.ToLowerInvariant()} '{previousName}' cannot be removed.",
                    issues);
                continue;
            }
            Equal(
                previousName,
                name(current),
                kind + "NameChanged",
                kind + "s[" + previousName + "].Name",
                kind + " name",
                issues);
            Equal(
                value(previous),
                value(current),
                kind + "ValueChanged",
                kind + "s[" + previousName + "].Value",
                kind + " value",
                issues);
        }
    }

    private static void Equal<T>(
        T baseline,
        T candidate,
        string code,
        string path,
        string label,
        List<EventProviderCompatibilityIssue> issues) {

        bool equal = baseline is string left &&
                     candidate is string right
            ? string.Equals(
                left,
                right,
                StringComparison.Ordinal)
            : EqualityComparer<T>.Default.Equals(
                baseline,
                candidate);
        if (!equal) {
            Add(
                code,
                path,
                $"{label} changed from '{baseline}' to '{candidate}'.",
                issues);
        }
    }

    private static void Add(
        string code,
        string path,
        string message,
        List<EventProviderCompatibilityIssue> issues) {

        issues.Add(new EventProviderCompatibilityIssue {
            Code = code,
            Path = path,
            Message = message
        });
    }
}
