using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace EventViewerX.Providers;

/// <summary>Generates validated Windows instrumentation manifests.</summary>
public static class EventProviderManifestGenerator {
    private static readonly XNamespace ManifestNamespace =
        "http://schemas.microsoft.com/win/2004/08/events";
    private static readonly XNamespace WindowsNamespace =
        "http://manifests.microsoft.com/win/2004/08/windows/events";
    private static readonly XNamespace XmlSchemaNamespace =
        "http://www.w3.org/2001/XMLSchema";

    /// <summary>Generates an instrumentation manifest as UTF-8 XML.</summary>
    public static string Generate(
        EventProviderDefinition definition,
        string resourceFileName) {

        EventProviderDefinitionValidator.ValidateOrThrow(definition);
        if (string.IsNullOrWhiteSpace(resourceFileName)) {
            throw new ArgumentException(
                "Resource file name cannot be empty.",
                nameof(resourceFileName));
        }

        var strings = new LocalizedStringCatalog(
            definition.DefaultCulture);
        XElement provider = CreateProvider(
            definition,
            resourceFileName,
            strings);
        XDocument document = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                ManifestNamespace + "instrumentationManifest",
                new XAttribute(
                    XNamespace.Xmlns + "win",
                    WindowsNamespace),
                new XAttribute(
                    XNamespace.Xmlns + "xs",
                    XmlSchemaNamespace),
                new XElement(
                    ManifestNamespace + "instrumentation",
                    new XElement(
                        ManifestNamespace + "events",
                        provider)),
                CreateLocalization(strings)));

        using var output = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(
                   output,
                   new XmlWriterSettings {
                       OmitXmlDeclaration = false,
                       Indent = true,
                       Encoding = new UTF8Encoding(false),
                       NewLineChars = Environment.NewLine
                   })) {
            document.Save(writer);
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static XElement CreateProvider(
        EventProviderDefinition definition,
        string resourceFileName,
        LocalizedStringCatalog strings) {

        string providerSymbol = EventProviderManifestNames.Symbol(
            definition.Symbol,
            definition.Name);
        string providerNameId = strings.Add(
            "Provider.Name",
            definition.DisplayNames,
            definition.Name);
        var provider = new XElement(
            ManifestNamespace + "provider",
            new XAttribute("name", definition.Name),
            new XAttribute("guid", "{" +
                definition.Id.ToString("D").ToUpperInvariant() + "}"),
            new XAttribute("symbol", providerSymbol),
            new XAttribute("resourceFileName", resourceFileName),
            new XAttribute("messageFileName", resourceFileName),
            new XAttribute("message", Reference(providerNameId)));

        provider.Add(CreateChannels(definition, strings));
        if (definition.Levels.Count > 0) {
            provider.Add(CreateLevels(definition, strings));
        }
        if (definition.Tasks.Count > 0) {
            provider.Add(CreateTasks(definition, strings));
        }
        if (definition.Opcodes.Count > 0) {
            provider.Add(CreateOpcodes(
                definition.Opcodes,
                "Opcode",
                strings));
        }
        if (definition.Keywords.Count > 0) {
            provider.Add(CreateKeywords(definition, strings));
        }
        if (definition.Maps.Count > 0) {
            provider.Add(CreateMaps(definition, strings));
        }
        provider.Add(CreateTemplates(definition));
        provider.Add(CreateEvents(definition, strings));
        return provider;
    }

    private static XElement CreateChannels(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "channels",
            definition.Channels.Select(channel => {
                string stringId = strings.Add(
                    "Channel." + SafeId(channel.Id),
                    channel.DisplayNames,
                    channel.Name.Split('/').Last());
                var element = new XElement(
                    ManifestNamespace + "channel",
                    new XAttribute("chid", channel.Id),
                    new XAttribute("name", channel.Name),
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.Symbol(
                            channel.Symbol,
                            definition.Name + "_" + channel.Id)),
                    new XAttribute("type", channel.Type),
                    new XAttribute("isolation", channel.Isolation),
                    new XAttribute(
                        "enabled",
                        channel.Enabled
                            ? "true"
                            : "false"),
                    new XAttribute("message", Reference(stringId)));
                if (!string.IsNullOrWhiteSpace(channel.Access)) {
                    element.Add(
                        new XAttribute(
                            "access",
                            channel.Access.Trim()));
                }
                XElement? logging = CreateChannelLogging(channel);
                if (logging != null) {
                    element.Add(logging);
                }
                return element;
            }));
    }

    private static XElement? CreateChannelLogging(
        EventProviderChannelDefinition channel) {

        var logging = new XElement(ManifestNamespace + "logging");
        AddOptionalElement(
            logging,
            "autoBackup",
            channel.AutoBackup);
        AddOptionalElement(
            logging,
            "retention",
            channel.Retention);
        if (channel.MaximumSizeBytes.HasValue) {
            logging.Add(
                new XElement(
                    ManifestNamespace + "maxSize",
                    channel.MaximumSizeBytes.Value));
        }
        return logging.HasElements ? logging : null;
    }

    private static XElement CreateLevels(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "levels",
            definition.Levels.Select(level => {
                string id = strings.Add(
                    "Level." + SafeId(level.Name),
                    level.DisplayNames,
                    level.Name);
                return new XElement(
                    ManifestNamespace + "level",
                    new XAttribute("name", level.Name),
                    new XAttribute("value", level.Value),
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.Symbol(
                            level.Symbol,
                            definition.Name + "_Level_" +
                            level.Name)),
                    new XAttribute("message", Reference(id)));
            }));
    }

    private static XElement CreateTasks(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "tasks",
            definition.Tasks.Select(task => {
                string id = strings.Add(
                    "Task." + SafeId(task.Name),
                    task.DisplayNames,
                    task.Name);
                var element = new XElement(
                    ManifestNamespace + "task",
                    new XAttribute("name", task.Name),
                    new XAttribute("value", task.Value),
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.Symbol(
                            task.Symbol,
                            definition.Name + "_Task_" +
                            task.Name)),
                    new XAttribute("message", Reference(id)));
                if (task.EventGuid.HasValue) {
                    element.Add(
                        new XAttribute(
                            "eventGUID",
                            "{" +
                            task.EventGuid.Value
                                .ToString("D")
                                .ToUpperInvariant() +
                            "}"));
                }
                if (task.Opcodes.Count > 0) {
                    element.Add(CreateOpcodes(
                        task.Opcodes,
                        "Task." + SafeId(task.Name) + ".Opcode",
                        strings));
                }
                return element;
            }));
    }

    private static XElement CreateOpcodes(
        IEnumerable<EventProviderOpcodeDefinition> opcodes,
        string stringPrefix,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "opcodes",
            opcodes.Select(opcode => {
                string id = strings.Add(
                    stringPrefix + "." + SafeId(opcode.Name),
                    opcode.DisplayNames,
                    opcode.Name);
                return new XElement(
                    ManifestNamespace + "opcode",
                    new XAttribute("name", opcode.Name),
                    new XAttribute("value", opcode.Value),
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.Symbol(
                            opcode.Symbol,
                            stringPrefix + "_" + opcode.Name)),
                    new XAttribute("message", Reference(id)));
            }));
    }

    private static XElement CreateKeywords(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "keywords",
            definition.Keywords.Select(keyword => {
                string id = strings.Add(
                    "Keyword." + SafeId(keyword.Name),
                    keyword.DisplayNames,
                    keyword.Name);
                return new XElement(
                    ManifestNamespace + "keyword",
                    new XAttribute("name", keyword.Name),
                    new XAttribute(
                        "mask",
                        EventProviderManifestNames.Hex(keyword.Mask)),
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.Symbol(
                            keyword.Symbol,
                            definition.Name + "_Keyword_" +
                            keyword.Name)),
                    new XAttribute("message", Reference(id)));
            }));
    }

    private static XElement CreateMaps(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "maps",
            definition.Maps.Select(map => {
                string elementName =
                    map.Kind == EventProviderMapKind.Value
                        ? "valueMap"
                        : "bitMap";
                return new XElement(
                    ManifestNamespace + elementName,
                    new XAttribute("name", map.Name),
                    map.Entries.Select(entry => {
                        string id = strings.Add(
                            "Map." + SafeId(map.Name) + "." +
                            SafeId(entry.Value.ToString(
                                CultureInfo.InvariantCulture)),
                            entry.Messages,
                            entry.Value.ToString(
                                CultureInfo.InvariantCulture));
                        return new XElement(
                            ManifestNamespace + "map",
                            new XAttribute(
                                "value",
                                map.Kind == EventProviderMapKind.Bit
                                    ? EventProviderManifestNames.Hex(
                                        unchecked((ulong)entry.Value))
                                    : entry.Value.ToString(
                                        CultureInfo.InvariantCulture)),
                            new XAttribute(
                                "message",
                                Reference(id)));
                    }));
            }));
    }

    private static XElement CreateTemplates(
        EventProviderDefinition definition) {

        return new XElement(
            ManifestNamespace + "templates",
            definition.Events
                .Select(static eventDefinition =>
                    new {
                        Event = eventDefinition,
                        TemplateId = TemplateId(eventDefinition)
                    })
                .GroupBy(
                    static item => item.TemplateId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(static group => {
                    EventProviderEventDefinition eventDefinition =
                        group.First().Event;
                    return new XElement(
                        ManifestNamespace + "template",
                        new XAttribute(
                            "tid",
                            group.Key),
                        eventDefinition.Fields.Select(field => {
                            var element = new XElement(
                                ManifestNamespace + "data",
                                new XAttribute(
                                    "name",
                                    field.Name),
                                new XAttribute(
                                    "inType",
                                    EventProviderManifestNames.TypeName(
                                        field.Type)));
                            string outputType =
                                EventProviderManifestNames
                                    .OutputTypeName(field);
                            if (outputType.Length > 0) {
                                element.Add(
                                    new XAttribute(
                                        "outType",
                                        outputType));
                            }
                            AddOptionalAttribute(
                                element,
                                "map",
                                field.Map);
                            AddOptionalAttribute(
                                element,
                                "length",
                                field.Length);
                            AddOptionalAttribute(
                                element,
                                "count",
                                field.Count);
                            return element;
                        }));
                }));
    }

    private static XElement CreateEvents(
        EventProviderDefinition definition,
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "events",
            definition.Events.Select(eventDefinition => {
                Dictionary<string, string> messages =
                    eventDefinition.Messages.Count > 0
                        ? eventDefinition.Messages
                        : new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase) {
                            [definition.DefaultCulture] =
                                CreateFallbackEventMessage(
                                    eventDefinition)
                        };
                var compiledMessages = messages.ToDictionary(
                    static pair => pair.Key,
                    pair =>
                        EventProviderMessageTemplateCompiler.Compile(
                            pair.Value,
                            eventDefinition.Fields),
                    StringComparer.OrdinalIgnoreCase);
                string messageId = strings.Add(
                    "Event." +
                    eventDefinition.Id.ToString(
                        CultureInfo.InvariantCulture) +
                    "." +
                    eventDefinition.Version.ToString(
                        CultureInfo.InvariantCulture),
                    compiledMessages,
                    EventProviderMessageTemplateCompiler.Compile(
                        CreateFallbackEventMessage(eventDefinition),
                        eventDefinition.Fields));
                var element = new XElement(
                    ManifestNamespace + "event",
                    new XAttribute(
                        "symbol",
                        EventProviderManifestNames.EventSymbol(
                            eventDefinition)),
                    new XAttribute("value", eventDefinition.Id),
                    new XAttribute(
                        "version",
                        eventDefinition.Version),
                    new XAttribute(
                        "channel",
                        eventDefinition.Channel),
                    new XAttribute(
                        "level",
                        eventDefinition.Level),
                    new XAttribute(
                        "template",
                        TemplateId(eventDefinition)),
                    new XAttribute(
                        "message",
                        Reference(messageId)));
                AddOptionalAttribute(
                    element,
                    "task",
                    eventDefinition.Task);
                AddOptionalAttribute(
                    element,
                    "opcode",
                    eventDefinition.Opcode);
                if (eventDefinition.Keywords.Count > 0) {
                    element.Add(
                        new XAttribute(
                            "keywords",
                            string.Join(
                                " ",
                                eventDefinition.Keywords)));
                }
                return element;
            }));
    }

    private static XElement CreateLocalization(
        LocalizedStringCatalog strings) {

        return new XElement(
            ManifestNamespace + "localization",
            strings.Cultures.Select(culture =>
                new XElement(
                    ManifestNamespace + "resources",
                    new XAttribute("culture", culture),
                    new XElement(
                        ManifestNamespace + "stringTable",
                        strings.Entries.Select(entry =>
                            new XElement(
                                ManifestNamespace + "string",
                                new XAttribute("id", entry.Id),
                                new XAttribute(
                                    "value",
                                    entry.Value(culture))))))));
    }

    internal static string CreateFallbackEventMessage(
        EventProviderEventDefinition eventDefinition) {

        if (eventDefinition.Fields.Count == 0) {
            return eventDefinition.Name + ".";
        }
        string message =
            eventDefinition.Name + ": " +
            string.Join(
                "; ",
                eventDefinition.Fields
                    .Take(100)
                    .Select(field =>
                        field.Name + "={" + field.Name + "}"));
        return eventDefinition.Fields.Count > 100
            ? message + "; additional fields omitted."
            : message;
    }

    private static string TemplateId(
        EventProviderEventDefinition eventDefinition) {

        return "T_" +
               EventProviderManifestNames.EventSymbol(
                   eventDefinition);
    }

    private static string Reference(string id) {
        return "$(string." + id + ")";
    }

    private static string SafeId(string value) {
        return EventProviderManifestNames.SafeId(value);
    }

    private static void AddOptionalAttribute(
        XElement element,
        string name,
        string value) {

        if (!string.IsNullOrWhiteSpace(value)) {
            element.Add(
                new XAttribute(
                    name,
                    value.Trim()));
        }
    }

    private static void AddOptionalElement(
        XElement element,
        string name,
        bool? value) {

        if (value.HasValue) {
            element.Add(
                new XElement(
                    ManifestNamespace + name,
                    value.Value
                        ? "true"
                        : "false"));
        }
    }

    private sealed class LocalizedStringCatalog {
        private readonly string _defaultCulture;
        private readonly List<LocalizedStringEntry> _entries = new();
        private readonly HashSet<string> _cultures =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ids =
            new(StringComparer.OrdinalIgnoreCase);

        internal LocalizedStringCatalog(string defaultCulture) {
            _defaultCulture = defaultCulture;
            _cultures.Add(defaultCulture);
        }

        internal IReadOnlyList<string> Cultures => _cultures
            .OrderBy(static culture => culture, StringComparer.Ordinal)
            .ToArray();

        internal IReadOnlyList<LocalizedStringEntry> Entries => _entries;

        internal string Add(
            string id,
            IReadOnlyDictionary<string, string> values,
            string fallback) {

            if (!_ids.Add(id)) {
                throw new InvalidOperationException(
                    $"Localization string ID '{id}' is duplicated.");
            }
            foreach (string culture in values.Keys) {
                _cultures.Add(culture);
            }
            _entries.Add(
                new LocalizedStringEntry(
                    id,
                    values,
                    _defaultCulture,
                    fallback));
            return id;
        }
    }

    private sealed class LocalizedStringEntry {
        private readonly IReadOnlyDictionary<string, string> _values;
        private readonly string _defaultCulture;
        private readonly string _fallback;

        internal LocalizedStringEntry(
            string id,
            IReadOnlyDictionary<string, string> values,
            string defaultCulture,
            string fallback) {

            Id = id;
            _values = values;
            _defaultCulture = defaultCulture;
            _fallback = fallback;
        }

        internal string Id { get; }

        internal string Value(string culture) {
            if (_values.TryGetValue(culture, out string? value) &&
                !string.IsNullOrWhiteSpace(value)) {
                return value;
            }
            if (_values.TryGetValue(
                    _defaultCulture,
                    out string? defaultValue) &&
                !string.IsNullOrWhiteSpace(defaultValue)) {
                return defaultValue;
            }
            return _values.Values.FirstOrDefault(
                       static item =>
                           !string.IsNullOrWhiteSpace(item)) ??
                   _fallback;
        }
    }
}
