using System.Globalization;

namespace EventViewerX.Providers;

public static partial class EventProviderDefinitionValidator {
    private static void ValidateGeneratedNames(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        var symbols = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var localizationIds = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var templateIds = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        AddGeneratedName(
            symbols,
            EventProviderManifestNames.Symbol(
                definition.Symbol,
                definition.Name),
            "Symbol",
            "GeneratedSymbolCollision",
            issues);
        AddGeneratedName(
            localizationIds,
            "Provider.Name",
            "DisplayNames",
            "GeneratedLocalizationIdCollision",
            issues);

        for (int index = 0;
             index < definition.Channels.Count;
             index++) {
            EventProviderChannelDefinition channel =
                definition.Channels[index];
            AddGeneratedName(
                symbols,
                EventProviderManifestNames.Symbol(
                    channel.Symbol,
                    definition.Name + "_" + channel.Id),
                $"Channels[{index}].Symbol",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                "Channel." +
                EventProviderManifestNames.SafeId(channel.Id),
                $"Channels[{index}].Id",
                "GeneratedLocalizationIdCollision",
                issues);
        }

        for (int index = 0;
             index < definition.Levels.Count;
             index++) {
            EventProviderLevelDefinition level =
                definition.Levels[index];
            AddGeneratedName(
                symbols,
                EventProviderManifestNames.Symbol(
                    level.Symbol,
                    definition.Name + "_Level_" + level.Name),
                $"Levels[{index}].Symbol",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                "Level." +
                EventProviderManifestNames.SafeId(level.Name),
                $"Levels[{index}].Name",
                "GeneratedLocalizationIdCollision",
                issues);
        }

        for (int taskIndex = 0;
             taskIndex < definition.Tasks.Count;
             taskIndex++) {
            EventProviderTaskDefinition task =
                definition.Tasks[taskIndex];
            string taskPrefix =
                "Task." +
                EventProviderManifestNames.SafeId(task.Name);
            AddGeneratedName(
                symbols,
                EventProviderManifestNames.Symbol(
                    task.Symbol,
                    definition.Name + "_Task_" + task.Name),
                $"Tasks[{taskIndex}].Symbol",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                taskPrefix,
                $"Tasks[{taskIndex}].Name",
                "GeneratedLocalizationIdCollision",
                issues);
            for (int opcodeIndex = 0;
                 opcodeIndex < task.Opcodes.Count;
                 opcodeIndex++) {
                EventProviderOpcodeDefinition opcode =
                    task.Opcodes[opcodeIndex];
                string opcodePrefix = taskPrefix + ".Opcode";
                AddGeneratedName(
                    symbols,
                    EventProviderManifestNames.Symbol(
                        opcode.Symbol,
                        opcodePrefix + "_" + opcode.Name),
                    $"Tasks[{taskIndex}].Opcodes[{opcodeIndex}].Symbol",
                    "GeneratedSymbolCollision",
                    issues);
                AddGeneratedName(
                    localizationIds,
                    opcodePrefix + "." +
                    EventProviderManifestNames.SafeId(opcode.Name),
                    $"Tasks[{taskIndex}].Opcodes[{opcodeIndex}].Name",
                    "GeneratedLocalizationIdCollision",
                    issues);
            }
        }

        for (int index = 0;
             index < definition.Opcodes.Count;
             index++) {
            EventProviderOpcodeDefinition opcode =
                definition.Opcodes[index];
            AddGeneratedName(
                symbols,
                EventProviderManifestNames.Symbol(
                    opcode.Symbol,
                    "Opcode_" + opcode.Name),
                $"Opcodes[{index}].Symbol",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                "Opcode." +
                EventProviderManifestNames.SafeId(opcode.Name),
                $"Opcodes[{index}].Name",
                "GeneratedLocalizationIdCollision",
                issues);
        }

        for (int index = 0;
             index < definition.Keywords.Count;
             index++) {
            EventProviderKeywordDefinition keyword =
                definition.Keywords[index];
            AddGeneratedName(
                symbols,
                EventProviderManifestNames.Symbol(
                    keyword.Symbol,
                    definition.Name + "_Keyword_" + keyword.Name),
                $"Keywords[{index}].Symbol",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                "Keyword." +
                EventProviderManifestNames.SafeId(keyword.Name),
                $"Keywords[{index}].Name",
                "GeneratedLocalizationIdCollision",
                issues);
        }

        for (int mapIndex = 0;
             mapIndex < definition.Maps.Count;
             mapIndex++) {
            EventProviderMapDefinition map =
                definition.Maps[mapIndex];
            foreach (EventProviderMapEntryDefinition entry in
                     map.Entries) {
                AddGeneratedName(
                    localizationIds,
                    "Map." +
                    EventProviderManifestNames.SafeId(map.Name) +
                    "." +
                    EventProviderManifestNames.SafeId(
                        entry.Value.ToString(
                            CultureInfo.InvariantCulture)),
                    $"Maps[{mapIndex}].Entries",
                    "GeneratedLocalizationIdCollision",
                    issues);
            }
        }

        for (int index = 0;
             index < definition.Events.Count;
             index++) {
            EventProviderEventDefinition eventDefinition =
                definition.Events[index];
            string eventSymbol =
                EventProviderManifestNames.EventSymbol(
                    eventDefinition);
            AddGeneratedName(
                symbols,
                eventSymbol,
                $"Events[{index}].Name",
                "GeneratedSymbolCollision",
                issues);
            AddGeneratedName(
                templateIds,
                "T_" + eventSymbol,
                $"Events[{index}]",
                "GeneratedTemplateIdCollision",
                issues);
            AddGeneratedName(
                localizationIds,
                "Event." +
                eventDefinition.Id.ToString(
                    CultureInfo.InvariantCulture) +
                "." +
                eventDefinition.Version.ToString(
                    CultureInfo.InvariantCulture),
                $"Events[{index}]",
                "GeneratedLocalizationIdCollision",
                issues);
        }
    }

    private static void AddGeneratedName(
        IDictionary<string, string> generated,
        string value,
        string path,
        string code,
        List<EventProviderValidationIssue> issues) {

        if (generated.TryGetValue(
                value,
                out string? earlierPath)) {
            Error(
                code,
                path,
                $"Generated name '{value}' collides with {earlierPath}. Use an explicit unique symbol or a name that remains unique after manifest normalization.",
                issues);
            return;
        }
        generated.Add(value, path);
    }
}
