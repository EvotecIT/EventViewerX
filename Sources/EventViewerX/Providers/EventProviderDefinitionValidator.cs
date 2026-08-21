using System.Globalization;

namespace EventViewerX.Providers;

/// <summary>
/// Validates provider definitions before manifest generation or installation.
/// </summary>
public static partial class EventProviderDefinitionValidator {
    private static readonly HashSet<EventProviderFieldType>
        DimensionReferenceTypes =
        new() {
            EventProviderFieldType.UInt8,
            EventProviderFieldType.UInt16,
            EventProviderFieldType.UInt32,
            EventProviderFieldType.HexInt32
        };
    /// <summary>Validates all schema, reference, and Windows limit rules.</summary>
    public static EventProviderValidationResult Validate(
        EventProviderDefinition definition) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        var issues = new List<EventProviderValidationIssue>();
        if (!ValidateObjectGraph(
                definition,
                issues)) {
            return new EventProviderValidationResult(
                issues);
        }
        ValidateProvider(definition, issues);
        ValidateChannels(definition, issues);
        ValidateMetadata(definition, issues);
        ValidateMaps(definition, issues);
        ValidateEvents(definition, issues);
        ValidateGeneratedNames(definition, issues);
        return new EventProviderValidationResult(issues);
    }

    /// <summary>Throws when the definition contains one or more errors.</summary>
    public static EventProviderValidationResult ValidateOrThrow(
        EventProviderDefinition definition) {

        EventProviderValidationResult result = Validate(definition);
        if (!result.IsValid) {
            throw new EventProviderValidationException(result);
        }
        return result;
    }

    private static void ValidateProvider(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        Required(definition.Name, "ProviderNameRequired", "Name", issues);
        if (definition.Name?.Length > 255) {
            Error(
                "ProviderNameTooLong",
                "Name",
                "Provider names cannot exceed 255 characters.",
                issues);
        }
        if (definition.Id == Guid.Empty) {
            Error(
                "ProviderIdRequired",
                "Id",
                "A stable non-empty provider GUID is required.",
                issues);
        }
        Required(
            definition.PackageVersion,
            "PackageVersionRequired",
            "PackageVersion",
            issues);
        if (!string.IsNullOrWhiteSpace(definition.PackageVersion)) {
            try {
                _ = EventProviderPackageVersion.Parse(
                    definition.PackageVersion);
            } catch (FormatException exception) {
                Error(
                    "PackageVersionInvalid",
                    "PackageVersion",
                    exception.Message,
                    issues);
            }
            if (definition.PackageVersion.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0) {
                Error(
                    "PackageVersionInvalid",
                    "PackageVersion",
                    "PackageVersion must be safe to use as a directory name.",
                    issues);
            }
        }
        Required(
            definition.DefaultCulture,
            "DefaultCultureRequired",
            "DefaultCulture",
            issues);
        if (!string.IsNullOrWhiteSpace(
                definition.DefaultCulture)) {
            try {
                _ = CultureInfo.GetCultureInfo(
                    definition.DefaultCulture);
            } catch (CultureNotFoundException) {
                Error(
                    "DefaultCultureInvalid",
                    "DefaultCulture",
                    $"'{definition.DefaultCulture}' is not a recognized culture.",
                    issues);
            }
        }
        if (definition.Channels.Count == 0) {
            Error(
                "ChannelRequired",
                "Channels",
                "At least one provider channel is required.",
                issues);
        }
        if (definition.Events.Count == 0) {
            Error(
                "EventRequired",
                "Events",
                "At least one event is required.",
                issues);
        }
    }

    private static void ValidateChannels(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        if (definition.Channels.Count > 16) {
            Error(
                "ChannelLimitExceeded",
                "Channels",
                "Windows event providers support at most 16 manifest channels.",
                issues);
        }
        Unique(
            definition.Channels,
            static channel => channel.Id,
            "DuplicateChannelId",
            "Channels",
            issues);
        Unique(
            definition.Channels,
            static channel => channel.Name,
            "DuplicateChannelName",
            "Channels",
            issues);
        for (int index = 0; index < definition.Channels.Count; index++) {
            EventProviderChannelDefinition channel =
                definition.Channels[index];
            string path = $"Channels[{index}]";
            Required(
                channel.Id,
                "ChannelIdRequired",
                path + ".Id",
                issues);
            Required(
                channel.Name,
                "ChannelNameRequired",
                path + ".Name",
                issues);
            if (!Enum.IsDefined(
                    typeof(EventProviderChannelType),
                    channel.Type)) {
                Error(
                    "ChannelTypeInvalid",
                    path + ".Type",
                    $"Channel type '{channel.Type}' is not supported.",
                    issues);
            }
            if (!Enum.IsDefined(
                    typeof(EventProviderChannelIsolation),
                    channel.Isolation)) {
                Error(
                    "ChannelIsolationInvalid",
                    path + ".Isolation",
                    $"Channel isolation '{channel.Isolation}' is not supported.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(definition.Name) &&
                !string.IsNullOrWhiteSpace(channel.Name) &&
                !channel.Name.StartsWith(
                    definition.Name + "/",
                    StringComparison.OrdinalIgnoreCase)) {
                Warning(
                    "ChannelNameConvention",
                    path + ".Name",
                    $"Channel names conventionally begin with '{definition.Name}/'.",
                    issues);
            }
            if (channel.MaximumSizeBytes.HasValue &&
                channel.MaximumSizeBytes.Value < 64 * 1024) {
                Error(
                    "ChannelMaximumSizeInvalid",
                    path + ".MaximumSizeBytes",
                    "Channel maximum size must be at least 65536 bytes.",
                    issues);
            }
            if (channel.Type is EventProviderChannelType.Analytic or
                EventProviderChannelType.Debug &&
                channel.Enabled) {
                Warning(
                    "HighVolumeChannelEnabled",
                    path + ".Enabled",
                    "Analytic and Debug channels are normally installed disabled.",
                    issues);
            }
        }
    }

    private static void ValidateMetadata(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        Unique(
            definition.Levels,
            static level => level.Name,
            "DuplicateLevelName",
            "Levels",
            issues);
        Unique(
            definition.Levels,
            static level => level.Value.ToString(
                CultureInfo.InvariantCulture),
            "DuplicateLevelValue",
            "Levels",
            issues);
        for (int index = 0; index < definition.Levels.Count; index++) {
            EventProviderLevelDefinition level = definition.Levels[index];
            Required(
                level.Name,
                "LevelNameRequired",
                $"Levels[{index}].Name",
                issues);
            ValidateMetadataIdentifier(
                level.Name,
                "LevelNameInvalid",
                $"Levels[{index}].Name",
                issues);
            if (level.Value < 16) {
                Error(
                    "CustomLevelReserved",
                    $"Levels[{index}].Value",
                    "Custom level values must be between 16 and 255.",
                    issues);
            }
        }

        Unique(
            definition.Tasks,
            static task => task.Name,
            "DuplicateTaskName",
            "Tasks",
            issues);
        Unique(
            definition.Tasks,
            static task => task.Value.ToString(
                CultureInfo.InvariantCulture),
            "DuplicateTaskValue",
            "Tasks",
            issues);
        Unique(
            definition.Opcodes,
            static opcode => opcode.Name,
            "DuplicateOpcodeName",
            "Opcodes",
            issues);
        Unique(
            definition.Opcodes,
            static opcode => opcode.Value.ToString(
                CultureInfo.InvariantCulture),
            "DuplicateOpcodeValue",
            "Opcodes",
            issues);
        for (int taskIndex = 0;
             taskIndex < definition.Tasks.Count;
             taskIndex++) {
            EventProviderTaskDefinition task =
                definition.Tasks[taskIndex];
            Required(
                task.Name,
                "TaskNameRequired",
                $"Tasks[{taskIndex}].Name",
                issues);
            ValidateMetadataIdentifier(
                task.Name,
                "TaskNameInvalid",
                $"Tasks[{taskIndex}].Name",
                issues);
            if (task.Value == 0) {
                Error(
                    "CustomTaskReserved",
                    $"Tasks[{taskIndex}].Value",
                    "Custom task values must be between 1 and 65535.",
                    issues);
            }
            Unique(
                task.Opcodes,
                static opcode => opcode.Name,
                "DuplicateOpcodeName",
                $"Tasks[{taskIndex}].Opcodes",
                issues);
            Unique(
                task.Opcodes,
                static opcode => opcode.Value.ToString(
                    CultureInfo.InvariantCulture),
                "DuplicateOpcodeValue",
                $"Tasks[{taskIndex}].Opcodes",
                issues);
            for (int opcodeIndex = 0;
                 opcodeIndex < task.Opcodes.Count;
                 opcodeIndex++) {
                Required(
                    task.Opcodes[opcodeIndex].Name,
                    "OpcodeNameRequired",
                    $"Tasks[{taskIndex}].Opcodes[{opcodeIndex}].Name",
                    issues);
                ValidateMetadataIdentifier(
                    task.Opcodes[opcodeIndex].Name,
                    "OpcodeNameInvalid",
                    $"Tasks[{taskIndex}].Opcodes[{opcodeIndex}].Name",
                    issues);
            }
        }
        for (int opcodeIndex = 0;
             opcodeIndex < definition.Opcodes.Count;
             opcodeIndex++) {
            Required(
                definition.Opcodes[opcodeIndex].Name,
                "OpcodeNameRequired",
                $"Opcodes[{opcodeIndex}].Name",
                issues);
            ValidateMetadataIdentifier(
                definition.Opcodes[opcodeIndex].Name,
                "OpcodeNameInvalid",
                $"Opcodes[{opcodeIndex}].Name",
                issues);
        }
        foreach (EventProviderOpcodeDefinition opcode in
                 definition.Opcodes.Concat(
                     definition.Tasks.SelectMany(static task =>
                         task.Opcodes))) {
            if (opcode.Value < 10 || opcode.Value > 239) {
                Error(
                    "CustomOpcodeReserved",
                    "Opcodes",
                    $"Custom opcode '{opcode.Name}' must use a value between 10 and 239.",
                    issues);
            }
        }

        Unique(
            definition.Keywords,
            static keyword => keyword.Name,
            "DuplicateKeywordName",
            "Keywords",
            issues);
        ulong combined = 0;
        for (int index = 0; index < definition.Keywords.Count; index++) {
            EventProviderKeywordDefinition keyword =
                definition.Keywords[index];
            Required(
                keyword.Name,
                "KeywordNameRequired",
                $"Keywords[{index}].Name",
                issues);
            ValidateMetadataIdentifier(
                keyword.Name,
                "KeywordNameInvalid",
                $"Keywords[{index}].Name",
                issues);
            if (keyword.Mask == 0 ||
                (keyword.Mask & (keyword.Mask - 1)) != 0 ||
                keyword.Mask > 0x0000FFFFFFFFFFFFUL) {
                Error(
                    "KeywordMaskInvalid",
                    $"Keywords[{index}].Mask",
                    "Keyword masks must contain one non-reserved bit in the low 48 bits.",
                    issues);
            }
            if ((combined & keyword.Mask) != 0) {
                Error(
                    "KeywordMaskDuplicate",
                    $"Keywords[{index}].Mask",
                    $"Keyword mask {EventProviderManifestNames.Hex(keyword.Mask)} is already used.",
                    issues);
            }
            combined |= keyword.Mask;
        }
    }

    private static void ValidateMaps(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        Unique(
            definition.Maps,
            static map => map.Name,
            "DuplicateMapName",
            "Maps",
            issues);
        for (int mapIndex = 0; mapIndex < definition.Maps.Count; mapIndex++) {
            EventProviderMapDefinition map = definition.Maps[mapIndex];
            Required(
                map.Name,
                "MapNameRequired",
                $"Maps[{mapIndex}].Name",
                issues);
            if (!string.IsNullOrWhiteSpace(map.Name) &&
                !EventProviderManifestNames
                    .IsUnqualifiedIdentifier(map.Name)) {
                Error(
                    "MapNameInvalid",
                    $"Maps[{mapIndex}].Name",
                    "Map names must be valid unqualified XML identifiers.",
                    issues);
            }
            if (!Enum.IsDefined(
                    typeof(EventProviderMapKind),
                    map.Kind)) {
                Error(
                    "MapKindInvalid",
                    $"Maps[{mapIndex}].Kind",
                    $"Map kind '{map.Kind}' is not supported.",
                    issues);
            }
            if (map.Entries.Count == 0) {
                Error(
                    "MapEntryRequired",
                    $"Maps[{mapIndex}].Entries",
                    "At least one map entry is required.",
                    issues);
            }
            var values = new HashSet<long>();
            for (int entryIndex = 0;
                 entryIndex < map.Entries.Count;
                 entryIndex++) {
                EventProviderMapEntryDefinition entry =
                    map.Entries[entryIndex];
                if (!values.Add(entry.Value)) {
                    Error(
                        "DuplicateMapValue",
                        $"Maps[{mapIndex}].Entries[{entryIndex}].Value",
                        $"Map value {entry.Value} is duplicated.",
                        issues);
                }
                bool valueInRange =
                    entry.Value >= uint.MinValue &&
                    entry.Value <= uint.MaxValue;
                if (!valueInRange) {
                    Error(
                        "MapValueOutOfRange",
                        $"Maps[{mapIndex}].Entries[{entryIndex}].Value",
                        "Map values must be unsigned 32-bit integers between 0 and 4294967295.",
                        issues);
                }
                if (map.Kind == EventProviderMapKind.Bit &&
                    valueInRange &&
                    (entry.Value == 0 ||
                     (entry.Value & (entry.Value - 1)) != 0)) {
                    Error(
                        "BitMapValueInvalid",
                        $"Maps[{mapIndex}].Entries[{entryIndex}].Value",
                        "Bit-map values must contain exactly one positive bit.",
                        issues);
                }
            }
        }
    }

    private static void ValidateEvents(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        var channelIds = new HashSet<string>(
            definition.Channels.Select(static channel => channel.Id),
            StringComparer.Ordinal);
        var customLevels = new HashSet<string>(
            definition.Levels.Select(static level => level.Name),
            StringComparer.Ordinal);
        var tasks = new HashSet<string>(
            definition.Tasks.Select(static task => task.Name),
            StringComparer.Ordinal);
        var opcodes = new HashSet<string>(
            definition.Opcodes.Select(static opcode => opcode.Name)
                .Concat(
                    definition.Tasks.SelectMany(static task =>
                        task.Opcodes.Select(opcode =>
                            task.Name + ":" + opcode.Name))),
            StringComparer.Ordinal);
        var keywords = new HashSet<string>(
            definition.Keywords.Select(static keyword => keyword.Name),
            StringComparer.Ordinal);
        var maps = new HashSet<string>(
            definition.Maps.Select(static map => map.Name),
            StringComparer.Ordinal);
        var identities = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int eventIndex = 0;
             eventIndex < definition.Events.Count;
             eventIndex++) {
            EventProviderEventDefinition eventDefinition =
                definition.Events[eventIndex];
            string path = $"Events[{eventIndex}]";
            Required(
                eventDefinition.Name,
                "EventNameRequired",
                path + ".Name",
                issues);
            string identity =
                eventDefinition.Id.ToString(CultureInfo.InvariantCulture) +
                ":" +
                eventDefinition.Version.ToString(
                    CultureInfo.InvariantCulture);
            if (!identities.Add(identity)) {
                Error(
                    "DuplicateEventIdentity",
                    path,
                    $"Event ID {eventDefinition.Id} version {eventDefinition.Version} is duplicated.",
                    issues);
            }
            string nameVersion = eventDefinition.Name + ":" +
                                 eventDefinition.Version.ToString(
                                     CultureInfo.InvariantCulture);
            if (!names.Add(nameVersion)) {
                Error(
                    "DuplicateEventName",
                    path + ".Name",
                    $"Event name '{eventDefinition.Name}' version {eventDefinition.Version} is duplicated.",
                    issues);
            }
            if (eventDefinition.Id < 0 ||
                eventDefinition.Id > ushort.MaxValue) {
                Error(
                    "EventIdOutOfRange",
                    path + ".Id",
                    "Event IDs must be between 0 and 65535.",
                    issues);
            }
            if (!channelIds.Contains(eventDefinition.Channel)) {
                Error(
                    "EventChannelUnknown",
                    path + ".Channel",
                    $"Channel '{eventDefinition.Channel}' is not declared.",
                    issues);
            }
            if (!IsStandardLevel(eventDefinition.Level) &&
                !customLevels.Contains(eventDefinition.Level)) {
                Error(
                    "EventLevelUnknown",
                    path + ".Level",
                    $"Level '{eventDefinition.Level}' is not a standard or declared custom level.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(eventDefinition.Task) &&
                !tasks.Contains(eventDefinition.Task)) {
                Error(
                    "EventTaskUnknown",
                    path + ".Task",
                    $"Task '{eventDefinition.Task}' is not declared.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(eventDefinition.Opcode)) {
                string taskOpcode = eventDefinition.Task + ":" +
                                    eventDefinition.Opcode;
                if (!opcodes.Contains(eventDefinition.Opcode) &&
                    !opcodes.Contains(taskOpcode) &&
                    !StandardOpcodes.Contains(
                        eventDefinition.Opcode)) {
                    Error(
                        "EventOpcodeUnknown",
                        path + ".Opcode",
                        $"Opcode '{eventDefinition.Opcode}' is not declared.",
                        issues);
                }
            }
            foreach (string keyword in eventDefinition.Keywords) {
                if (!keywords.Contains(keyword) &&
                    !StandardKeywords.Contains(keyword)) {
                    Error(
                        "EventKeywordUnknown",
                        path + ".Keywords",
                        $"Keyword '{keyword}' is not declared.",
                        issues);
                }
            }
            ValidateFields(
                eventDefinition,
                path,
                maps,
                issues);
            bool canCompileMessages =
                HasValidMessageFieldNames(
                    eventDefinition.Fields);
            foreach (KeyValuePair<string, string> message in
                     eventDefinition.Messages) {
                try {
                    _ = CultureInfo.GetCultureInfo(message.Key);
                    if (canCompileMessages) {
                        _ = EventProviderMessageTemplateCompiler.Compile(
                            message.Value,
                            eventDefinition.Fields);
                    }
                } catch (Exception exception)
                    when (exception is CultureNotFoundException ||
                          exception is FormatException) {
                    Error(
                        "EventMessageInvalid",
                        path + $".Messages[{message.Key}]",
                        exception.Message,
                        issues);
                }
            }
            if (canCompileMessages) {
                try {
                    _ = EventProviderMessageTemplateCompiler.Compile(
                        EventProviderManifestGenerator
                            .CreateFallbackEventMessage(
                                eventDefinition),
                        eventDefinition.Fields);
                } catch (FormatException exception) {
                    Error(
                        "EventFallbackMessageInvalid",
                        path + ".FallbackMessage",
                        exception.Message,
                        issues);
                }
            }
        }
    }

    private static bool HasValidMessageFieldNames(
        IReadOnlyList<EventProviderFieldDefinition> fields) {

        var names = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (EventProviderFieldDefinition field in fields) {
            if (string.IsNullOrWhiteSpace(field.Name) ||
                !names.Add(field.Name)) {
                return false;
            }
        }
        return true;
    }

    private static void ValidateFields(
        EventProviderEventDefinition eventDefinition,
        string path,
        HashSet<string> maps,
        List<EventProviderValidationIssue> issues) {

        if (eventDefinition.Fields.Count > 128) {
            Error(
                "PayloadFieldLimitExceeded",
                path + ".Fields",
                "Windows events support at most 128 top-level payload fields.",
                issues);
        }
        Unique(
            eventDefinition.Fields,
            static field => field.Name,
            "DuplicateFieldName",
            path + ".Fields",
            issues);
        for (int fieldIndex = 0;
             fieldIndex < eventDefinition.Fields.Count;
             fieldIndex++) {
            EventProviderFieldDefinition field =
                eventDefinition.Fields[fieldIndex];
            string fieldPath = $"{path}.Fields[{fieldIndex}]";
            Required(
                field.Name,
                "FieldNameRequired",
                fieldPath + ".Name",
                issues);
            if (!Enum.IsDefined(
                    typeof(EventProviderFieldType),
                    field.Type)) {
                Error(
                    "FieldTypeInvalid",
                    fieldPath + ".Type",
                    $"Field type '{field.Type}' is not supported.",
                    issues);
            } else if (field.Type == EventProviderFieldType.Auto) {
                Error(
                    "FieldTypeCannotBeAuto",
                    fieldPath + ".Type",
                    "Auto is only valid while inferring a typed payload. A provider definition requires a concrete Windows field type.",
                    issues);
            }
            if (!Enum.IsDefined(
                    typeof(EventProviderFieldOutputType),
                    field.OutputType)) {
                Error(
                    "FieldOutputTypeInvalid",
                    fieldPath + ".OutputType",
                    $"Field output type '{field.OutputType}' is not supported.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(
                    field.CustomOutputType) &&
                (!Enum.IsDefined(
                     typeof(EventProviderFieldType),
                     field.Type) ||
                 !EventProviderManifestNames
                     .IsSupportedOutputType(
                         field.Type,
                         field.CustomOutputType))) {
                Error(
                    "FieldCustomOutputTypeInvalid",
                    fieldPath + ".CustomOutputType",
                    $"Custom output type '{field.CustomOutputType}' is not supported for Windows input type '{field.Type}'.",
                    issues);
            }
            if (string.IsNullOrWhiteSpace(
                    field.CustomOutputType) &&
                Enum.IsDefined(
                    typeof(EventProviderFieldType),
                    field.Type) &&
                field.Type != EventProviderFieldType.Auto &&
                Enum.IsDefined(
                    typeof(EventProviderFieldOutputType),
                    field.OutputType)) {
                string outputType =
                    EventProviderManifestNames.OutputTypeName(
                        field);
                if (outputType.Length > 0 &&
                    !EventProviderManifestNames
                        .IsSupportedOutputType(
                            field.Type,
                            outputType)) {
                    Error(
                        "FieldOutputTypeIncompatible",
                        fieldPath + ".OutputType",
                        $"Output type '{outputType}' is not supported for Windows input type '{field.Type}'.",
                        issues);
                }
            }
            if (!string.IsNullOrWhiteSpace(field.Map) &&
                !maps.Contains(field.Map)) {
                Error(
                    "FieldMapUnknown",
                    fieldPath + ".Map",
                    $"Map '{field.Map}' is not declared.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(field.Map) &&
                field.Type is not EventProviderFieldType.UInt8 and
                    not EventProviderFieldType.UInt16 and
                    not EventProviderFieldType.UInt32 and
                    not EventProviderFieldType.HexInt32) {
                Error(
                    "FieldMapTypeIncompatible",
                    fieldPath + ".Map",
                    "Mapped fields must use UInt8, UInt16, UInt32, or HexInt32 input type.",
                    issues);
            }
            if (!string.IsNullOrWhiteSpace(field.Length) &&
                field.Type is not EventProviderFieldType.UnicodeString and
                    not EventProviderFieldType.AnsiString and
                    not EventProviderFieldType.Binary and
                    not EventProviderFieldType.Sid) {
                Error(
                    "FieldLengthTypeIncompatible",
                    fieldPath + ".Length",
                    "Length is supported only for UnicodeString, AnsiString, Binary, or Sid input types.",
                    issues);
            }
            ValidateDimension(
                field.Length,
                "Length",
                fieldIndex,
                eventDefinition.Fields,
                fieldPath,
                issues);
            ValidateDimension(
                field.Count,
                "Count",
                fieldIndex,
                eventDefinition.Fields,
                fieldPath,
                issues);
            if (field.Type == EventProviderFieldType.Binary &&
                string.IsNullOrWhiteSpace(field.Length)) {
                Error(
                    "BinaryLengthRequired",
                    fieldPath + ".Length",
                    "Binary fields require a fixed length or an earlier length field.",
                    issues);
            }
        }
    }

    private static void ValidateDimension(
        string expression,
        string name,
        int currentIndex,
        IReadOnlyList<EventProviderFieldDefinition> fields,
        string path,
        List<EventProviderValidationIssue> issues) {

        if (string.IsNullOrWhiteSpace(expression)) {
            return;
        }
        if (int.TryParse(
                expression,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int constant)) {
            if (constant < 0) {
                Error(
                    $"Field{name}Negative",
                    path + "." + name,
                    $"{name} cannot be negative.",
                    issues);
            } else if (constant > ushort.MaxValue) {
                Error(
                    $"Field{name}OutOfRange",
                    path + "." + name,
                    $"{name} cannot exceed {ushort.MaxValue}.",
                    issues);
            }
            return;
        }

        int referenceIndex = -1;
        for (int index = 0; index < currentIndex; index++) {
            if (string.Equals(
                    fields[index].Name,
                    expression,
                    StringComparison.Ordinal)) {
                referenceIndex = index;
                break;
            }
        }
        if (referenceIndex < 0) {
            Error(
                $"Field{name}ReferenceInvalid",
                path + "." + name,
                $"{name} must reference an earlier payload field or a non-negative integer.",
                issues);
            return;
        }
        if (!DimensionReferenceTypes.Contains(
                fields[referenceIndex].Type)) {
            Error(
                $"Field{name}ReferenceNotNumeric",
                path + "." + name,
                $"{name} reference '{expression}' must be a UInt8, UInt16, UInt32, or HexInt32 field.",
                issues);
        }
    }

    private static void Unique<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        string code,
        string path,
        List<EventProviderValidationIssue> issues) {

        var keys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (T value in values) {
            string key = keySelector(value) ?? string.Empty;
            if (key.Length > 0 && !keys.Add(key)) {
                Error(
                    code,
                    path,
                    $"Value '{key}' is duplicated.",
                    issues);
            }
        }
    }

    private static void Required(
        string value,
        string code,
        string path,
        List<EventProviderValidationIssue> issues) {

        if (string.IsNullOrWhiteSpace(value)) {
            Error(code, path, "A value is required.", issues);
        }
    }

    private static void ValidateMetadataIdentifier(
        string value,
        string code,
        string path,
        List<EventProviderValidationIssue> issues) {

        if (!string.IsNullOrWhiteSpace(value) &&
            !EventProviderManifestNames
                .IsUnqualifiedIdentifier(value)) {
            Error(
                code,
                path,
                $"Metadata name '{value}' must be an unqualified manifest identifier.",
                issues);
        }
    }

    private static void Error(
        string code,
        string path,
        string message,
        List<EventProviderValidationIssue> issues) {

        issues.Add(new EventProviderValidationIssue {
            Severity = EventProviderValidationSeverity.Error,
            Code = code,
            Path = path,
            Message = message
        });
    }

    private static void Warning(
        string code,
        string path,
        string message,
        List<EventProviderValidationIssue> issues) {

        issues.Add(new EventProviderValidationIssue {
            Severity = EventProviderValidationSeverity.Warning,
            Code = code,
            Path = path,
            Message = message
        });
    }
}
