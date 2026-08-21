using System.Text.Json;
using EventViewerX.Providers;

namespace PSEventViewer;

internal static class PowerShellEventProviderDefinitionAdapter {
    internal static EventProviderDefinition Convert(
        object inputObject) {

        if (inputObject is EventProviderDefinition definition) {
            EventProviderDefinitionValidator.ValidateOrThrow(
                definition);
            return definition;
        }
        object? plain = ToPlain(inputObject);
        if (plain is not Dictionary<string, object?> root) {
            throw new ArgumentException(
                "Provider definition must be an EventProviderDefinition, hashtable, or custom object.",
                nameof(inputObject));
        }
        NormalizeRoot(root);
        string json = JsonSerializer.Serialize(root);
        return EventProviderDefinitionJson.Parse(json);
    }

    private static void NormalizeRoot(
        Dictionary<string, object?> root) {

        Alias(root, "Name", "ProviderName");
        Alias(root, "Id", "ProviderId", "ProviderGuid", "Guid");
        Alias(root, "PackageVersion", "Version");
        string providerName = Text(root, "Name");
        string culture = Text(root, "DefaultCulture");
        if (culture.Length == 0) {
            culture = "en-US";
            root["DefaultCulture"] = culture;
        }
        Localize(root, "DisplayNames", "DisplayName", culture);
        Localize(root, "Descriptions", "Description", culture);

        if (!TryGet(root, "Channels", out object? channels) ||
            channels == null) {
            root["Channels"] = new object?[] {
                new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase) {
                    ["Id"] = "Operational",
                    ["Name"] =
                        providerName + "/Operational",
                    ["Type"] = "Operational",
                    ["Isolation"] = "Application",
                    ["Enabled"] = true
                }
            };
        } else {
            root["Channels"] = AsArray(
                channels,
                "Channels");
        }
        if (!TryGet(root, "Events", out object? eventsValue)) {
            throw new ArgumentException(
                "Provider definition requires Events.");
        }
        root["Events"] = AsArray(
            eventsValue,
            "Events");
        foreach (Dictionary<string, object?> eventDefinition in
                 Dictionaries(root["Events"], "Events")) {
            NormalizeEvent(
                eventDefinition,
                culture);
        }
        foreach (string collection in new[] {
                     "Levels",
                     "Opcodes",
                     "Keywords"
                 }) {
            NormalizeOptionalArray(root, collection);
        }
        if (TryGet(root, "Tasks", out object? tasks)) {
            root["Tasks"] = AsArray(tasks, "Tasks");
            foreach (Dictionary<string, object?> task in
                     Dictionaries(root["Tasks"], "Tasks")) {
                NormalizeOptionalArray(task, "Opcodes");
            }
        }
        if (TryGet(root, "Maps", out object? maps)) {
            root["Maps"] = AsArray(maps, "Maps");
            foreach (Dictionary<string, object?> map in
                     Dictionaries(root["Maps"], "Maps")) {
                NormalizeOptionalArray(map, "Entries");
            }
        }
    }

    private static void NormalizeEvent(
        Dictionary<string, object?> eventDefinition,
        string culture) {

        Alias(eventDefinition, "Id", "EventId");
        if (!TryGet(
                eventDefinition,
                "Channel",
                out object? channel) ||
            string.IsNullOrWhiteSpace(
                System.Convert.ToString(channel))) {
            eventDefinition["Channel"] = "Operational";
        }
        Localize(
            eventDefinition,
            "Messages",
            "Message",
            culture);
        NormalizeOptionalArray(
            eventDefinition,
            "Keywords");
        if (!TryGet(
                eventDefinition,
                "Fields",
                out object? fieldsValue) ||
            fieldsValue == null) {
            eventDefinition["Fields"] =
                Array.Empty<object?>();
            return;
        }

        if (fieldsValue is Dictionary<string, object?> fieldMap) {
            var fields = new List<object?>();
            foreach (KeyValuePair<string, object?> item in fieldMap) {
                if (item.Value is Dictionary<string, object?> detailed) {
                    if (!TryGet(detailed, "Name", out _)) {
                        detailed["Name"] = item.Key;
                    }
                    NormalizeField(detailed);
                    fields.Add(detailed);
                } else {
                    fields.Add(
                        new Dictionary<string, object?>(
                            StringComparer.OrdinalIgnoreCase) {
                            ["Name"] = item.Key,
                            ["Type"] = FriendlyType(item.Value)
                        });
                }
            }
            eventDefinition["Fields"] = fields;
            return;
        }
        foreach (Dictionary<string, object?> field in
                 Dictionaries(fieldsValue, "Fields")) {
            NormalizeField(field);
        }
    }

    private static void NormalizeField(
        Dictionary<string, object?> field) {

        if (TryGet(field, "Type", out object? type)) {
            field["Type"] = FriendlyType(type);
        }
    }

    private static object FriendlyType(object? value) {
        string type = System.Convert.ToString(value) ??
                      string.Empty;
        switch (type.Trim().ToLowerInvariant()) {
            case "string":
            case "text":
                return "UnicodeString";
            case "integer":
            case "int":
                return "Int32";
            case "unsignedinteger":
            case "uint":
                return "UInt32";
            case "long":
                return "Int64";
            case "unsignedlong":
            case "ulong":
                return "UInt64";
            case "datetime":
                return "FileTime";
            case "bool":
                return "Boolean";
            default:
                return type;
        }
    }

    private static void Localize(
        Dictionary<string, object?> values,
        string pluralName,
        string singularName,
        string culture) {

        if (TryGet(values, pluralName, out _)) {
            return;
        }
        if (TryGet(
                values,
                singularName,
                out object? singular) &&
            singular != null) {
            values.Remove(singularName);
            values[pluralName] =
                new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase) {
                    [culture] = singular
                };
        }
    }

    private static IEnumerable<Dictionary<string, object?>>
        Dictionaries(
            object? value,
            string propertyName) {

        if (value is Dictionary<string, object?> single) {
            yield return single;
            yield break;
        }
        if (value is IEnumerable enumerable &&
            value is not string) {
            foreach (object? item in enumerable) {
                if (item is not Dictionary<string, object?> dictionary) {
                    throw new ArgumentException(
                        $"{propertyName} must contain hashtables or custom objects.");
                }
                yield return dictionary;
            }
            yield break;
        }
        throw new ArgumentException(
            $"{propertyName} must be a hashtable, array, or list.");
    }

    private static void NormalizeOptionalArray(
        Dictionary<string, object?> values,
        string propertyName) {

        if (TryGet(values, propertyName, out object? value) &&
            value != null) {
            values[propertyName] = AsArray(
                value,
                propertyName);
        }
    }

    private static object?[] AsArray(
        object? value,
        string propertyName) {

        if (value is Dictionary<string, object?> dictionary) {
            return new object?[] {
                dictionary
            };
        }
        if (value is string) {
            return new[] {
                value
            };
        }
        if (value is IEnumerable enumerable) {
            var result = new List<object?>();
            foreach (object? item in enumerable) {
                result.Add(item);
            }
            return result.ToArray();
        }
        throw new ArgumentException(
            $"{propertyName} must be a hashtable, value, array, or list.");
    }

    private static object? ToPlain(object? value) {
        if (value == null) {
            return null;
        }
        if (value is PSObject wrapper &&
            wrapper.BaseObject != wrapper &&
            wrapper.BaseObject is not PSCustomObject) {
            return ToPlain(wrapper.BaseObject);
        }
        if (value is IDictionary dictionary) {
            var result = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary) {
                string key = System.Convert.ToString(entry.Key) ??
                             string.Empty;
                if (key.Length == 0 ||
                    result.ContainsKey(key)) {
                    throw new ArgumentException(
                        "Provider definition contains an empty or duplicate property name.");
                }
                result.Add(
                    key,
                    ToPlain(entry.Value));
            }
            return result;
        }
        if (IsScalar(value)) {
            return value is Enum
                ? value.ToString()
                : value;
        }
        if (value is IEnumerable enumerable &&
            value is not string) {
            var result = new List<object?>();
            foreach (object? item in enumerable) {
                result.Add(ToPlain(item));
            }
            return result;
        }

        PSObject psObject = PSObject.AsPSObject(value);
        var properties = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);
        foreach (PSPropertyInfo property in psObject.Properties) {
            if (!property.IsGettable) {
                continue;
            }
            properties[property.Name] =
                ToPlain(property.Value);
        }
        return properties;
    }

    private static bool IsScalar(object value) {
        Type type = Nullable.GetUnderlyingType(
            value.GetType()) ?? value.GetType();
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset);
    }

    private static void Alias(
        Dictionary<string, object?> values,
        string canonical,
        params string[] aliases) {

        if (TryGet(values, canonical, out _)) {
            return;
        }
        foreach (string alias in aliases) {
            if (!TryGet(values, alias, out object? value)) {
                continue;
            }
            values.Remove(alias);
            values[canonical] = value;
            return;
        }
    }

    private static string Text(
        Dictionary<string, object?> values,
        string name) {

        return TryGet(values, name, out object? value)
            ? System.Convert.ToString(value) ?? string.Empty
            : string.Empty;
    }

    private static bool TryGet(
        Dictionary<string, object?> values,
        string name,
        out object? value) {

        return values.TryGetValue(name, out value);
    }
}
