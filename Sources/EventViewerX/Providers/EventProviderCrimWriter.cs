using System.Security.Cryptography;

namespace EventViewerX.Providers;

/// <summary>
/// Compiles one validated provider definition to the Windows CRIM 5.1
/// WEVT_TEMPLATE representation used by modern Windows Event Log APIs.
/// </summary>
internal static class EventProviderCrimWriter {
    private const uint CrimVersion = 0x00010005;
    internal static byte[] Write(
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        if (definition.Channels.Count > 16) {
            throw new InvalidDataException(
                "Windows event providers support at most 16 manifest channels.");
        }

        using var output = new EventProviderBinaryBuffer();
        output.WriteAscii("CRIM");
        int crimSize = output.ReserveUInt32();
        output.WriteUInt32(CrimVersion);
        output.WriteUInt32(1);
        output.WriteGuid(definition.Id);
        int providerOffset = output.ReserveUInt32();
        output.PatchUInt32(providerOffset, checked((uint)output.Position));

        int providerStart = output.Position;
        output.WriteAscii("WEVT");
        int providerSize = output.ReserveUInt32();
        output.WriteUInt32(messages.ProviderMessageId);
        int elementCount = definition.Maps.Count > 0 ? 9 : 8;
        output.WriteUInt32(checked((uint)elementCount));

        var elementPatches = new Dictionary<uint, int>();
        WriteElementDescriptor(output, elementPatches, 5);
        if (definition.Maps.Count > 0) {
            WriteElementDescriptor(output, elementPatches, 6);
        }
        WriteElementDescriptor(output, elementPatches, 7);
        WriteElementDescriptor(output, elementPatches, 13);
        WriteElementDescriptor(output, elementPatches, 2);
        WriteElementDescriptor(output, elementPatches, 0);
        WriteElementDescriptor(output, elementPatches, 1);
        WriteElementDescriptor(output, elementPatches, 3);
        WriteElementDescriptor(output, elementPatches, 4);

        var channelValues = definition.Channels
            .Select((channel, index) => new {
                channel.Id,
                Value = checked((byte)(16 + index)),
                Keyword = 1UL << (63 - index)
            })
            .ToDictionary(
                static item => item.Id,
                static item => (item.Value, item.Keyword),
                StringComparer.Ordinal);
        WriteChannels(
            output,
            elementPatches[5],
            definition,
            messages);

        Dictionary<string, uint> mapOffsets =
            definition.Maps.Count > 0
                ? WriteMaps(
                    output,
                    elementPatches[6],
                    definition,
                    messages)
                : new Dictionary<string, uint>(StringComparer.Ordinal);
        Dictionary<int, uint> templateOffsets = WriteTemplates(
            output,
            elementPatches[7],
            definition,
            mapOffsets);
        WriteProviderAttributes(
            output,
            elementPatches[13],
            definition.Name);

        List<OpcodeMetadata> opcodes = CreateOpcodes(definition, messages);
        Dictionary<string, uint> opcodeOffsets = WriteOpcodes(
            output,
            elementPatches[2],
            opcodes);
        List<LevelMetadata> levels = CreateLevels(definition, messages);
        Dictionary<string, uint> levelOffsets = WriteLevels(
            output,
            elementPatches[0],
            levels);
        Dictionary<string, uint> taskOffsets = WriteTasks(
            output,
            elementPatches[1],
            definition,
            messages);
        List<KeywordMetadata> keywords = CreateKeywords(
            definition,
            messages);
        WriteKeywords(
            output,
            elementPatches[3],
            keywords);
        output.Align(8);
        WriteEvents(
            output,
            elementPatches[4],
            definition,
            messages,
            channelValues,
            templateOffsets,
            levelOffsets,
            taskOffsets,
            opcodeOffsets,
            keywords);

        output.PatchUInt32(
            providerSize,
            checked((uint)(output.Position - providerStart)));
        output.PatchUInt32(crimSize, checked((uint)output.Position));
        return output.ToArray();
    }

    private static void WriteElementDescriptor(
        EventProviderBinaryBuffer output,
        IDictionary<uint, int> patches,
        uint type) {

        output.WriteUInt32(type);
        patches.Add(type, output.ReserveUInt32());
    }

    private static void BeginElement(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        string signature) {

        output.Align(4);
        output.PatchUInt32(offsetPatch, checked((uint)output.Position));
        output.WriteAscii(signature);
    }

    private static void WriteChannels(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        BeginElement(output, offsetPatch, "CHAN");
        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(checked((uint)definition.Channels.Count));
        var names = new List<int>(definition.Channels.Count);
        for (int index = 0; index < definition.Channels.Count; index++) {
            output.WriteUInt32(checked((uint)index));
            names.Add(output.ReserveUInt32());
            output.WriteUInt32(checked((uint)(16 + index)));
            output.WriteUInt32(messages.ChannelMessageId(index));
        }
        for (int index = 0; index < definition.Channels.Count; index++) {
            output.PatchUInt32(names[index], checked((uint)output.Position));
            output.WriteSizedUtf16(definition.Channels[index].Name);
        }
        output.PatchUInt32(size, checked((uint)(output.Position - start)));
    }

    private static Dictionary<string, uint> WriteMaps(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        BeginElement(output, offsetPatch, "MAPS");
        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(checked((uint)definition.Maps.Count));
        var mapPatches = new List<int>(definition.Maps.Count);
        for (int index = 0; index < definition.Maps.Count; index++) {
            mapPatches.Add(output.ReserveUInt32());
        }

        var offsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        var namePatches = new List<int>(definition.Maps.Count);
        for (int mapIndex = 0;
             mapIndex < definition.Maps.Count;
             mapIndex++) {
            EventProviderMapDefinition map = definition.Maps[mapIndex];
            uint mapOffset = checked((uint)output.Position);
            offsets.Add(map.Name, mapOffset);
            output.PatchUInt32(mapPatches[mapIndex], mapOffset);
            output.WriteAscii(
                map.Kind == EventProviderMapKind.Value
                    ? "VMAP"
                    : "BMAP");
            int mapStart = output.Position - 4;
            int mapSize = output.ReserveUInt32();
            namePatches.Add(output.ReserveUInt32());
            output.WriteUInt32(
                map.Kind == EventProviderMapKind.Bit ? 1U : 0U);
            output.WriteUInt32(checked((uint)map.Entries.Count));
            for (int entryIndex = 0;
                 entryIndex < map.Entries.Count;
                 entryIndex++) {
                output.WriteUInt32(
                    checked((uint)map.Entries[entryIndex].Value));
                output.WriteUInt32(
                    messages.MapEntryMessageId(mapIndex, entryIndex));
            }
            output.PatchUInt32(
                mapSize,
                checked((uint)(output.Position - mapStart)));
        }
        for (int index = 0; index < definition.Maps.Count; index++) {
            output.PatchUInt32(
                namePatches[index],
                checked((uint)output.Position));
            output.WriteSizedUtf16(definition.Maps[index].Name);
        }
        output.PatchUInt32(size, checked((uint)(output.Position - start)));
        return offsets;
    }

    private static Dictionary<int, uint> WriteTemplates(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        EventProviderDefinition definition,
        IReadOnlyDictionary<string, uint> mapOffsets) {

        BeginElement(output, offsetPatch, "TTBL");
        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(checked((uint)definition.Events.Count));
        var offsets = new Dictionary<int, uint>();
        for (int eventIndex = 0;
             eventIndex < definition.Events.Count;
             eventIndex++) {
            EventProviderEventDefinition eventDefinition =
                definition.Events[eventIndex];
            int templateStart = output.Position;
            offsets.Add(eventIndex, checked((uint)templateStart));
            output.WriteAscii("TEMP");
            int templateSize = output.ReserveUInt32();
            output.WriteUInt32(checked((uint)eventDefinition.Fields.Count));
            output.WriteUInt32(checked((uint)eventDefinition.Fields.Count));
            int itemsOffset = output.ReserveUInt32();
            output.WriteUInt32(1);
            output.WriteGuid(CreateTemplateId(definition, eventDefinition));
            output.WriteBytes(
                EventProviderBinXmlWriter.Write(eventDefinition.Fields));
            output.PatchUInt32(itemsOffset, checked((uint)output.Position));

            var namePatches = new List<int>(eventDefinition.Fields.Count);
            for (int fieldIndex = 0;
                 fieldIndex < eventDefinition.Fields.Count;
                 fieldIndex++) {
                EventProviderFieldDefinition field =
                    eventDefinition.Fields[fieldIndex];
                Dimension dimension = ResolveDimension(
                    field,
                    eventDefinition.Fields,
                    fieldIndex);
                output.WriteUInt32(dimension.Flags);
                output.WriteByte(EventProviderBinaryTypes.Input(field.Type));
                output.WriteByte(EventProviderBinaryTypes.Output(field));
                output.WriteUInt16(0);
                output.WriteUInt32(
                    string.IsNullOrWhiteSpace(field.Map)
                        ? 0
                        : mapOffsets[field.Map]);
                output.WriteUInt32(dimension.Value);
                namePatches.Add(output.ReserveUInt32());
            }
            for (int fieldIndex = 0;
                 fieldIndex < eventDefinition.Fields.Count;
                 fieldIndex++) {
                output.PatchUInt32(
                    namePatches[fieldIndex],
                    checked((uint)output.Position));
                output.WriteSizedUtf16(
                    eventDefinition.Fields[fieldIndex].Name);
            }
            output.PatchUInt32(
                templateSize,
                checked((uint)(output.Position - templateStart)));
        }
        output.PatchUInt32(size, checked((uint)(output.Position - start)));
        return offsets;
    }

    private static void WriteProviderAttributes(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        string providerName) {

        BeginElement(output, offsetPatch, "PRVA");
        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(1);
        output.WriteUInt32(0x10000001);
        int nameOffset = output.ReserveUInt32();
        output.PatchUInt32(nameOffset, checked((uint)output.Position));
        output.WriteUtf16(providerName, nullTerminate: true);
        output.Align(4);
        output.PatchUInt32(size, checked((uint)(output.Position - start)));
    }

    private static Dictionary<string, uint> WriteOpcodes(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        IReadOnlyList<OpcodeMetadata> opcodes) {

        BeginElement(output, offsetPatch, "OPCO");
        return WriteNamedTable(
            output,
            opcodes,
            static (buffer, item, namePatch) => {
                buffer.WriteUInt16(item.Task);
                buffer.WriteUInt16(item.Value);
                buffer.WriteUInt32(item.MessageId);
                namePatch.Value = buffer.ReserveUInt32();
            },
            static item => item.Key,
            static item => item.Name);
    }

    private static Dictionary<string, uint> WriteLevels(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        IReadOnlyList<LevelMetadata> levels) {

        BeginElement(output, offsetPatch, "LEVL");
        return WriteNamedTable(
            output,
            levels,
            static (buffer, item, namePatch) => {
                buffer.WriteUInt32(item.Value);
                buffer.WriteUInt32(item.MessageId);
                namePatch.Value = buffer.ReserveUInt32();
            },
            static item => item.Name,
            static item => item.Name);
    }

    private static Dictionary<string, uint> WriteTasks(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        BeginElement(output, offsetPatch, "TASK");
        return WriteNamedTable(
            output,
            definition.Tasks,
            (buffer, item, namePatch) => {
                buffer.WriteUInt32(item.Value);
                buffer.WriteUInt32(messages.TaskMessageId(item.Name));
                buffer.WriteGuid(item.EventGuid ?? Guid.Empty);
                namePatch.Value = buffer.ReserveUInt32();
            },
            static item => item.Name,
            static item => item.Name);
    }

    private static void WriteKeywords(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        IReadOnlyList<KeywordMetadata> keywords) {

        BeginElement(output, offsetPatch, "KEYW");
        _ = WriteNamedTable(
            output,
            keywords,
            static (buffer, item, namePatch) => {
                buffer.WriteUInt64(item.Mask);
                buffer.WriteUInt32(item.MessageId);
                namePatch.Value = buffer.ReserveUInt32();
            },
            static item => item.Name,
            static item => item.Name);
    }

    private static Dictionary<string, uint> WriteNamedTable<T>(
        EventProviderBinaryBuffer output,
        IReadOnlyList<T> items,
        Action<EventProviderBinaryBuffer, T, OffsetPatch> writeRecord,
        Func<T, string> key,
        Func<T, string> name) {

        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(checked((uint)items.Count));
        var names = new List<OffsetPatch>(items.Count);
        var offsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (T item in items) {
            offsets.Add(key(item), checked((uint)output.Position));
            var namePatch = new OffsetPatch();
            writeRecord(output, item, namePatch);
            names.Add(namePatch);
        }
        for (int index = 0; index < items.Count; index++) {
            output.PatchUInt32(
                names[index].Value,
                checked((uint)output.Position));
            output.WriteSizedUtf16(name(items[index]));
        }
        output.PatchUInt32(
            size,
            items.Count == 0
                ? 0
                : checked((uint)(output.Position - start)));
        return offsets;
    }

    private static void WriteEvents(
        EventProviderBinaryBuffer output,
        int offsetPatch,
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages,
        IReadOnlyDictionary<string, (byte Value, ulong Keyword)> channels,
        IReadOnlyDictionary<int, uint> templateOffsets,
        IReadOnlyDictionary<string, uint> levelOffsets,
        IReadOnlyDictionary<string, uint> taskOffsets,
        IReadOnlyDictionary<string, uint> opcodeOffsets,
        IReadOnlyList<KeywordMetadata> keywords) {

        BeginElement(output, offsetPatch, "EVNT");
        int start = output.Position - 4;
        int size = output.ReserveUInt32();
        output.WriteUInt32(checked((uint)definition.Events.Count));
        output.WriteUInt32(0);
        var keywordValues = keywords.ToDictionary(
            static item => item.Name,
            static item => item.Mask,
            StringComparer.Ordinal);
        var taskValues = definition.Tasks.ToDictionary(
            static item => item.Name,
            static item => item,
            StringComparer.Ordinal);
        var levelValues = definition.Levels.ToDictionary(
            static item => item.Name,
            static item => item.Value,
            StringComparer.Ordinal);

        for (int index = 0; index < definition.Events.Count; index++) {
            EventProviderEventDefinition eventDefinition =
                definition.Events[index];
            (byte channelValue, ulong channelKeyword) =
                channels[eventDefinition.Channel];
            byte level = eventDefinition.Level.StartsWith(
                "win:",
                StringComparison.Ordinal)
                ? EventProviderStandardMetadata.Level(eventDefinition.Level)
                : levelValues[eventDefinition.Level];
            ushort task = 0;
            EventProviderTaskDefinition? taskDefinition = null;
            if (!string.IsNullOrWhiteSpace(eventDefinition.Task)) {
                taskDefinition = taskValues[eventDefinition.Task];
                task = taskDefinition.Value;
            }
            byte opcode = ResolveOpcodeValue(
                definition,
                eventDefinition,
                taskDefinition);
            ulong keyword = channelKeyword;
            foreach (string keywordName in eventDefinition.Keywords) {
                keyword |= keywordValues[keywordName];
            }

            output.WriteUInt16(checked((ushort)eventDefinition.Id));
            output.WriteByte(eventDefinition.Version);
            output.WriteByte(channelValue);
            output.WriteByte(level);
            output.WriteByte(opcode);
            output.WriteUInt16(task);
            output.WriteUInt64(keyword);
            output.WriteUInt32(messages.EventMessageId(index));
            output.WriteUInt32(templateOffsets[index]);
            output.WriteUInt32(
                ResolveOpcodeOffset(
                    eventDefinition,
                    taskDefinition,
                    opcodeOffsets));
            output.WriteUInt32(levelOffsets[eventDefinition.Level]);
            output.WriteUInt32(
                string.IsNullOrWhiteSpace(eventDefinition.Task)
                    ? 0
                    : taskOffsets[eventDefinition.Task]);
            output.WriteUInt32(0);
            output.WriteUInt32(0);
            output.WriteUInt32(
                taskDefinition?.EventGuid.HasValue == true
                    ? 0x88U
                    : 0x80U);
        }
        output.PatchUInt32(size, checked((uint)(output.Position - start)));
    }

    private static List<LevelMetadata> CreateLevels(
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        var levels = definition.Levels.Select(level =>
                new LevelMetadata(
                    level.Name,
                    level.Value,
                    messages.LevelMessageId(level.Name)))
            .ToList();
        foreach (string name in definition.Events
                     .Select(static item => item.Level)
                     .Where(static value => value.StartsWith(
                         "win:",
                         StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal)) {
            levels.Add(new LevelMetadata(
                name,
                EventProviderStandardMetadata.Level(name),
                EventProviderStandardMetadata.LevelMessageId(name)));
        }
        return levels;
    }

    private static List<OpcodeMetadata> CreateOpcodes(
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        var opcodes = new List<OpcodeMetadata>();
        foreach (EventProviderTaskDefinition task in definition.Tasks) {
            foreach (EventProviderOpcodeDefinition opcode in task.Opcodes) {
                opcodes.Add(new OpcodeMetadata(
                    TaskOpcodeKey(task.Name, opcode.Name),
                    opcode.Name,
                    task.Value,
                    opcode.Value,
                    messages.TaskOpcodeMessageId(task.Name, opcode.Name)));
            }
        }
        foreach (EventProviderOpcodeDefinition opcode in definition.Opcodes) {
            opcodes.Add(new OpcodeMetadata(
                OpcodeKey(opcode.Name),
                opcode.Name,
                0,
                opcode.Value,
                messages.OpcodeMessageId(opcode.Name)));
        }
        foreach (string name in definition.Events
                     .Select(static item => item.Opcode)
                     .Where(static value => value.StartsWith(
                         "win:",
                         StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal)) {
            opcodes.Add(new OpcodeMetadata(
                OpcodeKey(name),
                name,
                0,
                EventProviderStandardMetadata.Opcode(name),
                EventProviderStandardMetadata.OpcodeMessageId(name)));
        }
        return opcodes;
    }

    private static List<KeywordMetadata> CreateKeywords(
        EventProviderDefinition definition,
        EventProviderMessageCatalog messages) {

        var keywords = definition.Keywords.Select(keyword =>
                new KeywordMetadata(
                    keyword.Name,
                    keyword.Mask,
                    messages.KeywordMessageId(keyword.Name)))
            .ToList();
        foreach (string name in definition.Events
                     .SelectMany(static item => item.Keywords)
                     .Where(static value => value.StartsWith(
                         "win:",
                         StringComparison.Ordinal))
                     .Distinct(StringComparer.Ordinal)) {
            keywords.Add(new KeywordMetadata(
                name,
                EventProviderStandardMetadata.Keyword(name),
                EventProviderStandardMetadata.KeywordMessageId(name)));
        }
        return keywords;
    }

    private static byte ResolveOpcodeValue(
        EventProviderDefinition definition,
        EventProviderEventDefinition eventDefinition,
        EventProviderTaskDefinition? task) {

        if (string.IsNullOrWhiteSpace(eventDefinition.Opcode)) {
            return 0;
        }
        if (eventDefinition.Opcode.StartsWith(
                "win:",
                StringComparison.Ordinal)) {
            return EventProviderStandardMetadata.Opcode(
                eventDefinition.Opcode);
        }
        EventProviderOpcodeDefinition? taskOpcode = task?.Opcodes
            .FirstOrDefault(opcode => string.Equals(
                opcode.Name,
                eventDefinition.Opcode,
                StringComparison.Ordinal));
        return taskOpcode?.Value ?? definition.Opcodes.First(opcode =>
            string.Equals(
                opcode.Name,
                eventDefinition.Opcode,
                StringComparison.Ordinal)).Value;
    }

    private static uint ResolveOpcodeOffset(
        EventProviderEventDefinition eventDefinition,
        EventProviderTaskDefinition? task,
        IReadOnlyDictionary<string, uint> offsets) {

        if (string.IsNullOrWhiteSpace(eventDefinition.Opcode)) {
            return 0;
        }
        if (task != null && task.Opcodes.Any(opcode => string.Equals(
                opcode.Name,
                eventDefinition.Opcode,
                StringComparison.Ordinal))) {
            return offsets[TaskOpcodeKey(
                task.Name,
                eventDefinition.Opcode)];
        }
        return offsets[OpcodeKey(eventDefinition.Opcode)];
    }

    private static Dimension ResolveDimension(
        EventProviderFieldDefinition field,
        IReadOnlyList<EventProviderFieldDefinition> fields,
        int fieldIndex) {

        uint flags = 0;
        ushort count = 0;
        ushort length = 0;
        if (!string.IsNullOrWhiteSpace(field.Length)) {
            if (ushort.TryParse(field.Length, out ushort fixedLength)) {
                flags |= 2;
                length = fixedLength;
            } else {
                flags |= 4;
                length = FindEarlierField(fields, fieldIndex, field.Length);
            }
        }
        if (!string.IsNullOrWhiteSpace(field.Count)) {
            if (ushort.TryParse(field.Count, out ushort fixedCount)) {
                flags |= 8;
                count = fixedCount;
            } else {
                flags |= 16;
                count = FindEarlierField(fields, fieldIndex, field.Count);
            }
        }
        return new Dimension(
            flags,
            ((uint)length << 16) | count);
    }

    private static ushort FindEarlierField(
        IReadOnlyList<EventProviderFieldDefinition> fields,
        int fieldIndex,
        string name) {

        for (int index = 0; index < fieldIndex; index++) {
            if (string.Equals(
                    fields[index].Name,
                    name,
                    StringComparison.Ordinal)) {
                return checked((ushort)index);
            }
        }
        throw new InvalidDataException(
            $"Dimension field '{name}' is not an earlier payload field.");
    }

    private static Guid CreateTemplateId(
        EventProviderDefinition definition,
        EventProviderEventDefinition eventDefinition) {

        var identity = new StringBuilder();
        identity.Append(definition.Id.ToString("D"));
        identity.Append('|');
        identity.Append(eventDefinition.Id);
        identity.Append('|');
        identity.Append(eventDefinition.Version);
        foreach (EventProviderFieldDefinition field in eventDefinition.Fields) {
            identity.Append('|');
            identity.Append(field.Name);
            identity.Append(':');
            identity.Append((int)field.Type);
            identity.Append(':');
            identity.Append(EventProviderManifestNames.OutputTypeName(field));
            identity.Append(':');
            identity.Append(field.Map);
            identity.Append(':');
            identity.Append(field.Length);
            identity.Append(':');
            identity.Append(field.Count);
        }
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(identity.ToString()));
        byte[] guid = new byte[16];
        Array.Copy(hash, guid, guid.Length);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }

    private static string OpcodeKey(string name) => "opcode:" + name;
    private static string TaskOpcodeKey(string task, string name) =>
        "task-opcode:" + task + ":" + name;

    private sealed class OffsetPatch {
        internal int Value { get; set; }
    }

    private sealed class LevelMetadata {
        internal LevelMetadata(string name, byte value, uint messageId) {
            Name = name;
            Value = value;
            MessageId = messageId;
        }

        internal string Name { get; }
        internal byte Value { get; }
        internal uint MessageId { get; }
    }

    private sealed class OpcodeMetadata {
        internal OpcodeMetadata(
            string key,
            string name,
            ushort task,
            byte value,
            uint messageId) {

            Key = key;
            Name = name;
            Task = task;
            Value = value;
            MessageId = messageId;
        }

        internal string Key { get; }
        internal string Name { get; }
        internal ushort Task { get; }
        internal byte Value { get; }
        internal uint MessageId { get; }
    }

    private sealed class KeywordMetadata {
        internal KeywordMetadata(string name, ulong mask, uint messageId) {
            Name = name;
            Mask = mask;
            MessageId = messageId;
        }

        internal string Name { get; }
        internal ulong Mask { get; }
        internal uint MessageId { get; }
    }

    private readonly struct Dimension {
        internal Dimension(uint flags, uint value) {
            Flags = flags;
            Value = value;
        }

        internal uint Flags { get; }
        internal uint Value { get; }
    }
}
