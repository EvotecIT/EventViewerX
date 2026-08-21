using System.Globalization;

namespace EventViewerX.Providers;

public static partial class EventProviderDefinitionValidator {
    /// <summary>
    /// Rejects null collections and collection members that can be introduced
    /// by JSON deserialization despite the strongly typed object defaults.
    /// </summary>
    private static bool ValidateObjectGraph(
        EventProviderDefinition definition,
        List<EventProviderValidationIssue> issues) {

        bool valid = true;
        valid &= ValidateDictionary(
            definition.DisplayNames,
            "DisplayNames",
            issues);
        valid &= ValidateDictionary(
            definition.Descriptions,
            "Descriptions",
            issues);
        valid &= RequiredObject(
            definition.Channels,
            "Channels",
            issues);
        valid &= RequiredObject(
            definition.Levels,
            "Levels",
            issues);
        valid &= RequiredObject(
            definition.Tasks,
            "Tasks",
            issues);
        valid &= RequiredObject(
            definition.Opcodes,
            "Opcodes",
            issues);
        valid &= RequiredObject(
            definition.Keywords,
            "Keywords",
            issues);
        valid &= RequiredObject(
            definition.Maps,
            "Maps",
            issues);
        valid &= RequiredObject(
            definition.Events,
            "Events",
            issues);
        if (!valid) {
            return false;
        }

        valid &= ValidateMembers(
            definition.Channels,
            "Channels",
            issues,
            static (channel, path, currentIssues) =>
                ValidateDictionary(
                    channel.DisplayNames,
                    path + ".DisplayNames",
                    currentIssues));
        valid &= ValidateMembers(
            definition.Levels,
            "Levels",
            issues,
            static (level, path, currentIssues) =>
                ValidateDictionary(
                    level.DisplayNames,
                    path + ".DisplayNames",
                    currentIssues));
        valid &= ValidateMembers(
            definition.Tasks,
            "Tasks",
            issues,
            ValidateTask);
        valid &= ValidateMembers(
            definition.Opcodes,
            "Opcodes",
            issues,
            static (opcode, path, currentIssues) =>
                ValidateDictionary(
                    opcode.DisplayNames,
                    path + ".DisplayNames",
                    currentIssues));
        valid &= ValidateMembers(
            definition.Keywords,
            "Keywords",
            issues,
            static (keyword, path, currentIssues) =>
                ValidateDictionary(
                    keyword.DisplayNames,
                    path + ".DisplayNames",
                    currentIssues));
        valid &= ValidateMembers(
            definition.Maps,
            "Maps",
            issues,
            ValidateMap);
        valid &= ValidateMembers(
            definition.Events,
            "Events",
            issues,
            ValidateEvent);
        return valid;
    }

    private static bool ValidateTask(
        EventProviderTaskDefinition task,
        string path,
        List<EventProviderValidationIssue> issues) {

        bool valid = ValidateDictionary(
            task.DisplayNames,
            path + ".DisplayNames",
            issues);
        valid &= RequiredObject(
            task.Opcodes,
            path + ".Opcodes",
            issues);
        if (task.Opcodes == null) {
            return false;
        }
        valid &= ValidateMembers(
            task.Opcodes,
            path + ".Opcodes",
            issues,
            static (opcode, opcodePath, currentIssues) =>
                ValidateDictionary(
                    opcode.DisplayNames,
                    opcodePath + ".DisplayNames",
                    currentIssues));
        return valid;
    }

    private static bool ValidateMap(
        EventProviderMapDefinition map,
        string path,
        List<EventProviderValidationIssue> issues) {

        if (!RequiredObject(
                map.Entries,
                path + ".Entries",
                issues)) {
            return false;
        }
        return ValidateMembers(
            map.Entries,
            path + ".Entries",
            issues,
            static (entry, entryPath, currentIssues) =>
                ValidateDictionary(
                    entry.Messages,
                    entryPath + ".Messages",
                    currentIssues));
    }

    private static bool ValidateEvent(
        EventProviderEventDefinition eventDefinition,
        string path,
        List<EventProviderValidationIssue> issues) {

        bool valid = RequiredObject(
            eventDefinition.Keywords,
            path + ".Keywords",
            issues);
        valid &= RequiredObject(
            eventDefinition.Fields,
            path + ".Fields",
            issues);
        valid &= ValidateDictionary(
            eventDefinition.Messages,
            path + ".Messages",
            issues);
        if (eventDefinition.Keywords != null) {
            for (int index = 0;
                 index < eventDefinition.Keywords.Count;
                 index++) {
                valid &= RequiredObject(
                    eventDefinition.Keywords[index],
                    $"{path}.Keywords[{index}]",
                    issues);
            }
        }
        if (eventDefinition.Fields != null) {
            valid &= ValidateMembers(
                eventDefinition.Fields,
                path + ".Fields",
                issues);
        }
        return valid;
    }

    private static bool ValidateMembers<T>(
        IReadOnlyList<T> values,
        string path,
        List<EventProviderValidationIssue> issues,
        Func<T, string, List<EventProviderValidationIssue>, bool>?
            validate = null) where T : class {

        bool valid = true;
        for (int index = 0; index < values.Count; index++) {
            T? value = values[index];
            string itemPath = $"{path}[{index}]";
            if (!RequiredObject(
                    value,
                    itemPath,
                    issues)) {
                valid = false;
                continue;
            }
            if (validate != null) {
                valid &= validate(
                    value,
                    itemPath,
                    issues);
            }
        }
        return valid;
    }

    private static bool ValidateDictionary(
        IReadOnlyDictionary<string, string>? values,
        string path,
        List<EventProviderValidationIssue> issues) {

        if (!RequiredObject(
                values,
                path,
                issues)) {
            return false;
        }
        bool valid = true;
        foreach (KeyValuePair<string, string> value in values!) {
            valid &= RequiredObject(
                value.Value,
                $"{path}[{value.Key}]",
                issues);
            try {
                _ = CultureInfo.GetCultureInfo(value.Key);
            } catch (CultureNotFoundException) {
                Error(
                    "LocalizationCultureInvalid",
                    $"{path}[{value.Key}]",
                    $"'{value.Key}' is not a recognized culture.",
                    issues);
            }
        }
        return valid;
    }

    private static bool RequiredObject<T>(
        T? value,
        string path,
        List<EventProviderValidationIssue> issues)
        where T : class {

        if (value != null) {
            return true;
        }
        Error(
            "DefinitionMemberNull",
            path,
            "A provider definition member cannot be null.",
            issues);
        return false;
    }
}
