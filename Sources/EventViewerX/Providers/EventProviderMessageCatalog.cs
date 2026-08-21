using System.Globalization;

namespace EventViewerX.Providers;

/// <summary>
/// Assigns stable numeric message identifiers and resolves every declared
/// culture to a complete Windows message table.
/// </summary>
internal sealed class EventProviderMessageCatalog {
    private const uint FirstMessageId = 0x90000001;
    private readonly string _defaultCulture;
    private readonly Dictionary<string, uint> _ids =
        new(StringComparer.Ordinal);
    private readonly List<Entry> _entries = new();
    private readonly HashSet<string> _cultures =
        new(StringComparer.OrdinalIgnoreCase);

    private EventProviderMessageCatalog(string defaultCulture) {
        _defaultCulture = defaultCulture;
        _cultures.Add(defaultCulture);
    }

    internal IReadOnlyList<string> Cultures => _cultures
        .OrderBy(static culture => culture, StringComparer.Ordinal)
        .ToArray();

    internal static EventProviderMessageCatalog Create(
        EventProviderDefinition definition) {

        var catalog = new EventProviderMessageCatalog(
            definition.DefaultCulture);
        catalog.Add(
            ProviderKey,
            definition.DisplayNames,
            definition.Name);
        for (int index = 0; index < definition.Channels.Count; index++) {
            EventProviderChannelDefinition channel =
                definition.Channels[index];
            catalog.Add(
                ChannelKey(index),
                channel.DisplayNames,
                channel.Name.Split('/').Last());
        }
        foreach (EventProviderLevelDefinition level in definition.Levels) {
            catalog.Add(
                LevelKey(level.Name),
                level.DisplayNames,
                level.Name);
        }
        foreach (EventProviderTaskDefinition task in definition.Tasks) {
            catalog.Add(
                TaskKey(task.Name),
                task.DisplayNames,
                task.Name);
            foreach (EventProviderOpcodeDefinition opcode in task.Opcodes) {
                catalog.Add(
                    TaskOpcodeKey(task.Name, opcode.Name),
                    opcode.DisplayNames,
                    opcode.Name);
            }
        }
        foreach (EventProviderOpcodeDefinition opcode in definition.Opcodes) {
            catalog.Add(
                OpcodeKey(opcode.Name),
                opcode.DisplayNames,
                opcode.Name);
        }
        foreach (EventProviderKeywordDefinition keyword in
                 definition.Keywords) {
            catalog.Add(
                KeywordKey(keyword.Name),
                keyword.DisplayNames,
                keyword.Name);
        }
        for (int mapIndex = 0;
             mapIndex < definition.Maps.Count;
             mapIndex++) {
            EventProviderMapDefinition map = definition.Maps[mapIndex];
            for (int entryIndex = 0;
                 entryIndex < map.Entries.Count;
                 entryIndex++) {
                EventProviderMapEntryDefinition entry =
                    map.Entries[entryIndex];
                catalog.Add(
                    MapEntryKey(mapIndex, entryIndex),
                    entry.Messages,
                    entry.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
        for (int index = 0; index < definition.Events.Count; index++) {
            EventProviderEventDefinition eventDefinition =
                definition.Events[index];
            IReadOnlyDictionary<string, string> messages =
                eventDefinition.Messages.Count > 0
                    ? eventDefinition.Messages
                    : new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase) {
                        [definition.DefaultCulture] =
                            EventProviderManifestGenerator
                                .CreateFallbackEventMessage(
                                    eventDefinition)
                    };
            var compiled = messages.ToDictionary(
                static pair => pair.Key,
                pair => EventProviderMessageTemplateCompiler.Compile(
                    pair.Value,
                    eventDefinition.Fields),
                StringComparer.OrdinalIgnoreCase);
            catalog.Add(
                EventKey(index),
                compiled,
                EventProviderMessageTemplateCompiler.Compile(
                    EventProviderManifestGenerator
                        .CreateFallbackEventMessage(eventDefinition),
                    eventDefinition.Fields));
        }
        return catalog;
    }

    internal uint ProviderMessageId => Get(ProviderKey);
    internal uint ChannelMessageId(int index) => Get(ChannelKey(index));
    internal uint LevelMessageId(string name) => Get(LevelKey(name));
    internal uint TaskMessageId(string name) => Get(TaskKey(name));
    internal uint OpcodeMessageId(string name) => Get(OpcodeKey(name));
    internal uint TaskOpcodeMessageId(string task, string opcode) =>
        Get(TaskOpcodeKey(task, opcode));
    internal uint KeywordMessageId(string name) => Get(KeywordKey(name));
    internal uint MapEntryMessageId(int map, int entry) =>
        Get(MapEntryKey(map, entry));
    internal uint EventMessageId(int index) => Get(EventKey(index));

    internal IReadOnlyDictionary<uint, string> Messages(string culture) {
        return _entries.ToDictionary(
            static entry => entry.Id,
            entry => entry.Value(culture, _defaultCulture));
    }

    private uint Get(string key) {
        return _ids[key];
    }

    private void Add(
        string key,
        IReadOnlyDictionary<string, string> values,
        string fallback) {

        if (_ids.ContainsKey(key)) {
            return;
        }
        uint id = checked(FirstMessageId + (uint)_entries.Count);
        _ids.Add(key, id);
        foreach (string culture in values.Keys) {
            _cultures.Add(culture);
        }
        _entries.Add(new Entry(id, values, fallback));
    }

    private const string ProviderKey = "provider";
    private static string ChannelKey(int index) => "channel:" + index;
    private static string LevelKey(string name) => "level:" + name;
    private static string TaskKey(string name) => "task:" + name;
    private static string OpcodeKey(string name) => "opcode:" + name;
    private static string TaskOpcodeKey(string task, string opcode) =>
        "task-opcode:" + task + ":" + opcode;
    private static string KeywordKey(string name) => "keyword:" + name;
    private static string MapEntryKey(int map, int entry) =>
        "map:" + map + ":" + entry;
    private static string EventKey(int index) => "event:" + index;

    private sealed class Entry {
        private readonly IReadOnlyDictionary<string, string> _values;
        private readonly string _fallback;

        internal Entry(
            uint id,
            IReadOnlyDictionary<string, string> values,
            string fallback) {

            Id = id;
            _values = values;
            _fallback = fallback;
        }

        internal uint Id { get; }

        internal string Value(string culture, string defaultCulture) {
            if (_values.TryGetValue(culture, out string? value) &&
                !string.IsNullOrWhiteSpace(value)) {
                return value;
            }
            if (_values.TryGetValue(
                    defaultCulture,
                    out string? defaultValue) &&
                !string.IsNullOrWhiteSpace(defaultValue)) {
                return defaultValue;
            }
            return _values.Values.FirstOrDefault(
                       static item => !string.IsNullOrWhiteSpace(item)) ??
                   _fallback;
        }
    }
}
