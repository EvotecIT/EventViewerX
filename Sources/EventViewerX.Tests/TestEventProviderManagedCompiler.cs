using EventViewerX.Providers;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderManagedCompiler {
    [Fact]
    public void WritesDeterministicResourceOnlyPortableExecutable() {
        EventProviderDefinition definition = CreateLocalizedDefinition();
        string root = CreateTemporaryDirectory();
        string firstPath = Path.Combine(root, "first.dll");
        string secondPath = Path.Combine(root, "second.dll");
        try {
            EventProviderManagedCompiler.Compile(definition, firstPath);
            EventProviderManagedCompiler.Compile(definition, secondPath);

            Assert.Equal(
                File.ReadAllBytes(firstPath),
                File.ReadAllBytes(secondPath));
            using FileStream input = File.OpenRead(firstPath);
            using var pe = new PEReader(input);

            Assert.Equal(0, pe.PEHeaders.PEHeader!.AddressOfEntryPoint);
            Assert.Equal(0, pe.PEHeaders.PEHeader.ImportTableDirectory.Size);
            Assert.Equal(0, pe.PEHeaders.PEHeader.CorHeaderTableDirectory.Size);
            Assert.Single(pe.PEHeaders.SectionHeaders);
            Assert.Equal(
                ".rsrc",
                pe.PEHeaders.SectionHeaders[0].Name);
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EmbedsLocalizedMessagesAndTheCompiledEventTemplate() {
        EventProviderDefinition definition = CreateLocalizedDefinition();
        string root = CreateTemporaryDirectory();
        string outputPath = Path.Combine(root, "localized.dll");
        try {
            EventProviderManagedCompiler.Compile(
                definition,
                outputPath);

            byte[] english = ReadResource(
                outputPath,
                ResourceIdentifier.Id(11),
                1,
                0x0409);
            byte[] german = ReadResource(
                outputPath,
                ResourceIdentifier.Id(11),
                1,
                0x0407);
            byte[] template = ReadResource(
                outputPath,
                ResourceIdentifier.Name("WEVT_TEMPLATE"),
                1,
                0x0409);

            Assert.Contains(
                "Managed compiler event %1 with value %2.",
                ReadMessageTable(english));
            Assert.Contains(
                "Verwaltetes Compilerereignis %1 mit Wert %2.",
                ReadMessageTable(german));
            Assert.Equal("CRIM", Encoding.ASCII.GetString(template, 0, 4));
            Assert.Contains("WEVT", Encoding.ASCII.GetString(template));
            Assert.Contains("TTBL", Encoding.ASCII.GetString(template));
            Assert.Contains("EVNT", Encoding.ASCII.GetString(template));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UsesWinmetaMessageIdentifiersForStandardMetadata() {
        EventProviderDefinition definition = CreateLocalizedDefinition();
        definition.Events[0].Opcode = "win:Start";
        definition.Events[0].Keywords.Add("win:AuditSuccess");
        EventProviderMessageCatalog messages =
            EventProviderMessageCatalog.Create(definition);
        byte[] template = EventProviderCrimWriter.Write(
            definition,
            messages);

        int opcodes = FindSignature(template, "OPCO");
        int levels = FindSignature(template, "LEVL");
        int keywords = FindSignature(template, "KEYW");
        Assert.Equal(0x30000001U, ReadUInt32(template, opcodes + 16));
        Assert.Equal(0x50000004U, ReadUInt32(template, levels + 16));
        Assert.Equal(0x10000036U, ReadUInt32(template, keywords + 20));
        Assert.Equal(
            0x10000032U,
            EventProviderStandardMetadata.KeywordMessageId(
                "win:WDIContext"));
        Assert.Equal(
            0x10000038U,
            EventProviderStandardMetadata.KeywordMessageId(
                "win:EventlogClassic"));
        Assert.Equal(
            uint.MaxValue,
            EventProviderStandardMetadata.OpcodeMessageId(
                "win:ReservedOpcode241"));
        Assert.Equal(
            uint.MaxValue,
            EventProviderStandardMetadata.KeywordMessageId(
                "win:ReservedKeyword56"));
    }

    [Fact]
    public void CompilesMetadataMapsAndReferencedDimensions() {
        EventProviderDefinition definition = CreateRichDefinition();
        EventProviderMessageCatalog messages =
            EventProviderMessageCatalog.Create(definition);
        byte[] template = EventProviderCrimWriter.Write(
            definition,
            messages);

        Assert.Contains("CHAN", Encoding.ASCII.GetString(template));
        Assert.Contains("MAPS", Encoding.ASCII.GetString(template));
        Assert.Contains("VMAP", Encoding.ASCII.GetString(template));
        Assert.Contains("TTBL", Encoding.ASCII.GetString(template));
        Assert.Contains("PRVA", Encoding.ASCII.GetString(template));
        Assert.Contains("OPCO", Encoding.ASCII.GetString(template));
        Assert.Contains("LEVL", Encoding.ASCII.GetString(template));
        Assert.Contains("TASK", Encoding.ASCII.GetString(template));
        Assert.Contains("KEYW", Encoding.ASCII.GetString(template));
        Assert.Contains("EVNT", Encoding.ASCII.GetString(template));

        int temporary = FindSignature(template, "TEMP");
        int itemsOffset = checked((int)ReadUInt32(template, temporary + 16));
        Assert.Equal(0U, ReadUInt32(template, itemsOffset));
        Assert.Equal((byte)6, template[itemsOffset + 4]);

        int binary = itemsOffset + 20;
        Assert.Equal(4U, ReadUInt32(template, binary));
        Assert.Equal((byte)14, template[binary + 4]);
        Assert.Equal(0U, ReadUInt32(template, binary + 12) >> 16);

        int count = itemsOffset + 40;
        Assert.Equal(0U, ReadUInt32(template, count));
        Assert.Equal((byte)6, template[count + 4]);

        int values = itemsOffset + 60;
        Assert.Equal(16U, ReadUInt32(template, values));
        Assert.Equal((byte)8, template[values + 4]);
        Assert.Equal(2U, ReadUInt32(template, values + 12) & 0xffff);

        int status = itemsOffset + 80;
        Assert.Equal((byte)8, template[status + 4]);
        Assert.NotEqual(0U, ReadUInt32(template, status + 8));
    }

    [Fact]
    public void MapsEverySupportedInputTypeAndDeclaredOutputType() {
        EventProviderFieldType[] inputs = Enum
            .GetValues<EventProviderFieldType>()
            .Where(static value => value != EventProviderFieldType.Auto)
            .ToArray();
        Assert.Equal(21, inputs.Length);
        Assert.Equal(
            Enumerable.Range(1, 21).Select(static value => (byte)value),
            inputs.Select(EventProviderBinaryTypes.Input));

        foreach (EventProviderFieldOutputType outputType in
                 Enum.GetValues<EventProviderFieldOutputType>()) {
            EventProviderFieldDefinition field =
                CompatibleOutputField(outputType);
            byte output = EventProviderBinaryTypes.Output(field);
            Assert.InRange(output, (byte)1, (byte)38);
        }
    }

    [Fact]
    public void RejectsManagedCompilerBinaryLimitsDuringValidation() {
        EventProviderDefinition tooManyChannels =
            TestEventProviderPackages.CreateDefinition();
        tooManyChannels.Channels.Clear();
        for (int index = 0; index < 17; index++) {
            tooManyChannels.AddChannel(
                EventProviderChannelDefinition.Operational(
                    "Channel" + index,
                    tooManyChannels.Name + "/Channel" + index));
        }
        tooManyChannels.Events[0].Channel = "Channel0";

        EventProviderValidationResult channelResult =
            EventProviderDefinitionValidator.Validate(tooManyChannels);
        Assert.Contains(
            channelResult.Errors,
            static issue => issue.Code == "ChannelLimitExceeded");

        EventProviderDefinition oversizedDimension =
            TestEventProviderPackages.CreateDefinition();
        oversizedDimension.Events[0].Fields[0].Length = "65536";
        EventProviderValidationResult dimensionResult =
            EventProviderDefinitionValidator.Validate(oversizedDimension);
        Assert.Contains(
            dimensionResult.Errors,
            static issue => issue.Code == "FieldLengthOutOfRange");

        EventProviderDefinition incompatibleMap = CreateRichDefinition();
        incompatibleMap.Events[0].Fields[4].Type =
            EventProviderFieldType.UnicodeString;
        EventProviderValidationResult mapResult =
            EventProviderDefinitionValidator.Validate(incompatibleMap);
        Assert.Contains(
            mapResult.Errors,
            static issue => issue.Code == "FieldMapTypeIncompatible");

        EventProviderDefinition incompatibleLength =
            TestEventProviderPackages.CreateDefinition();
        incompatibleLength.Events[0].Fields[1].Length = "2";
        EventProviderValidationResult lengthResult =
            EventProviderDefinitionValidator.Validate(incompatibleLength);
        Assert.Contains(
            lengthResult.Errors,
            static issue => issue.Code == "FieldLengthTypeIncompatible");

        EventProviderDefinition incompatibleReference =
            TestEventProviderPackages.CreateDefinition();
        incompatibleReference.Events[0].Fields[0].Type =
            EventProviderFieldType.Int32;
        incompatibleReference.Events[0].Fields[1].Count = "ComputerName";
        EventProviderValidationResult referenceResult =
            EventProviderDefinitionValidator.Validate(incompatibleReference);
        Assert.Contains(
            referenceResult.Errors,
            static issue => issue.Code == "FieldCountReferenceNotNumeric");

        foreach (EventProviderMapKind kind in
                 Enum.GetValues<EventProviderMapKind>()) {
            EventProviderDefinition emptyMap =
                TestEventProviderPackages.CreateDefinition();
            emptyMap.Maps.Add(new EventProviderMapDefinition {
                Name = kind + "Map",
                Kind = kind
            });

            EventProviderValidationResult emptyMapResult =
                EventProviderDefinitionValidator.Validate(emptyMap);
            Assert.Contains(
                emptyMapResult.Errors,
                static issue => issue.Code == "MapEntryRequired");
        }
    }

    [Fact]
    public void GeneratesChannelLoggingAsSchemaElements() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        EventProviderChannelDefinition channel = definition.Channels[0];
        channel.Retention = true;
        channel.AutoBackup = false;
        channel.MaximumSizeBytes = 1024 * 1024;
        channel.Access = "O:BAG:SYD:(A;;0x3;;;SY)";

        XDocument manifest = XDocument.Parse(
            EventProviderManifestGenerator.Generate(
                definition,
                "provider.resources.dll"));
        XNamespace ns =
            "http://schemas.microsoft.com/win/2004/08/events";
        XElement channelElement = manifest.Descendants(ns + "channel").Single();
        XElement logging = channelElement.Element(ns + "logging")!;

        Assert.Equal(
            "O:BAG:SYD:(A;;0x3;;;SY)",
            channelElement.Attribute("access")!.Value);
        Assert.Null(channelElement.Attribute("retention"));
        Assert.Null(channelElement.Attribute("autoBackup"));
        Assert.Null(channelElement.Attribute("maxSize"));
        Assert.Equal(
            ["autoBackup", "retention", "maxSize"],
            logging.Elements()
                .Select(static element => element.Name.LocalName)
                .ToArray());
        Assert.Equal("true", logging.Element(ns + "retention")!.Value);
        Assert.Equal("false", logging.Element(ns + "autoBackup")!.Value);
        Assert.Equal("1048576", logging.Element(ns + "maxSize")!.Value);
    }

    private static EventProviderDefinition CreateLocalizedDefinition() {
        EventProviderDefinition definition =
            TestEventProviderPackages.CreateDefinition();
        definition.DisplayNames["de-DE"] = "EventViewerX Compiler Test";
        definition.Events[0].Messages["en-US"] =
            "Managed compiler event {ComputerName} with value {FindingCount}.";
        definition.Events[0].Messages["de-DE"] =
            "Verwaltetes Compilerereignis {ComputerName} mit Wert {FindingCount}.";
        return definition;
    }

    private static EventProviderDefinition CreateRichDefinition() {
        EventProviderDefinition definition = EventProviderDefinition.Create(
            "Evotec-EventViewerX-RichCompilerTest",
            Guid.Parse("c1cc0181-7363-4c70-872f-2170d33c1412"));
        definition.AddChannel(
            EventProviderChannelDefinition.Operational(
                "Operational",
                definition.Name + "/Operational"));
        definition.Levels.Add(new EventProviderLevelDefinition {
            Name = "Detailed",
            Value = 16
        });
        definition.Tasks.Add(new EventProviderTaskDefinition {
            Name = "Scan",
            Value = 1,
            EventGuid = Guid.Parse("0ad95f86-0b17-41bb-a99d-390825d31031"),
            Opcodes = {
                new EventProviderOpcodeDefinition {
                    Name = "Evaluate",
                    Value = 10
                }
            }
        });
        definition.Opcodes.Add(new EventProviderOpcodeDefinition {
            Name = "Publish",
            Value = 11
        });
        definition.Keywords.Add(new EventProviderKeywordDefinition {
            Name = "Compliance",
            Mask = 1
        });
        definition.Maps.Add(new EventProviderMapDefinition {
            Name = "StatusMap",
            Kind = EventProviderMapKind.Value,
            Entries = {
                new EventProviderMapEntryDefinition {
                    Value = 1,
                    Messages = { ["en-US"] = "Healthy" }
                }
            }
        });
        EventProviderEventDefinition eventDefinition =
            EventProviderEventDefinition.Create(
                "RichEvent",
                4100,
                "Operational");
        eventDefinition.Level = "Detailed";
        eventDefinition.Task = "Scan";
        eventDefinition.Opcode = "Evaluate";
        eventDefinition.Keywords.Add("Compliance");
        eventDefinition.AddField(
            EventProviderFieldDefinition.Create(
                "PayloadLength",
                EventProviderFieldType.UInt16));
        eventDefinition.AddField(new EventProviderFieldDefinition {
            Name = "Payload",
            Type = EventProviderFieldType.Binary,
            Length = "PayloadLength"
        });
        eventDefinition.AddField(
            EventProviderFieldDefinition.Create(
                "ValueCount",
                EventProviderFieldType.UInt16));
        eventDefinition.AddField(new EventProviderFieldDefinition {
            Name = "Values",
            Type = EventProviderFieldType.UInt32,
            Count = "ValueCount"
        });
        eventDefinition.AddField(new EventProviderFieldDefinition {
            Name = "Status",
            Type = EventProviderFieldType.UInt32,
            Map = "StatusMap"
        });
        eventDefinition.Messages["en-US"] =
            "Status {Status} with {ValueCount} values.";
        definition.AddEvent(eventDefinition);
        return definition;
    }

    private static EventProviderFieldDefinition CompatibleOutputField(
        EventProviderFieldOutputType outputType) {

        EventProviderFieldType input = outputType switch {
            EventProviderFieldOutputType.Xml or
            EventProviderFieldOutputType.Json =>
                EventProviderFieldType.UnicodeString,
            EventProviderFieldOutputType.Utf8 =>
                EventProviderFieldType.Binary,
            EventProviderFieldOutputType.DateTime or
            EventProviderFieldOutputType.CultureInsensitiveDateTime =>
                EventProviderFieldType.FileTime,
            EventProviderFieldOutputType.String =>
                EventProviderFieldType.UnicodeString,
            EventProviderFieldOutputType.IPv6 or
            EventProviderFieldOutputType.SocketAddress =>
                EventProviderFieldType.Binary,
            EventProviderFieldOutputType.CodePointer =>
                EventProviderFieldType.Pointer,
            _ => EventProviderFieldType.UInt32
        };
        return new EventProviderFieldDefinition {
            Name = "Value",
            Type = input,
            OutputType = outputType,
            Length = input == EventProviderFieldType.Binary ? "16" : string.Empty
        };
    }

    private static IReadOnlyList<string> ReadMessageTable(byte[] data) {
        int blockCount = checked((int)ReadUInt32(data, 0));
        var messages = new List<string>();
        for (int block = 0; block < blockCount; block++) {
            int descriptor = 4 + block * 12;
            uint lowId = ReadUInt32(data, descriptor);
            uint highId = ReadUInt32(data, descriptor + 4);
            int offset = checked((int)ReadUInt32(data, descriptor + 8));
            for (uint id = lowId; id <= highId; id++) {
                ushort length = ReadUInt16(data, offset);
                ushort flags = ReadUInt16(data, offset + 2);
                Assert.Equal((ushort)1, flags);
                string message = Encoding.Unicode
                    .GetString(data, offset + 4, length - 4)
                    .TrimEnd('\0', '\r', '\n');
                messages.Add(message);
                offset += length;
            }
        }
        return messages;
    }

    private static byte[] ReadResource(
        string path,
        ResourceIdentifier type,
        uint name,
        ushort language) {

        using FileStream input = File.OpenRead(path);
        using var pe = new PEReader(input);
        int resourceRva = pe.PEHeaders.PEHeader!
            .ResourceTableDirectory.RelativeVirtualAddress;
        SectionHeader section = pe.PEHeaders.SectionHeaders.Single(
            candidate => candidate.Name == ".rsrc");
        byte[] resources = pe.GetSectionData(section.VirtualAddress)
            .GetContent()
            .ToArray();
        int typeDirectory = FindDirectory(resources, 0, type);
        int nameDirectory = FindDirectory(
            resources,
            typeDirectory,
            ResourceIdentifier.Id(name));
        int dataEntry = FindDataEntry(
            resources,
            nameDirectory,
            ResourceIdentifier.Id(language));
        int dataRva = checked((int)ReadUInt32(resources, dataEntry));
        int size = checked((int)ReadUInt32(resources, dataEntry + 4));
        return resources.AsSpan(dataRva - resourceRva, size).ToArray();
    }

    private static int FindDirectory(
        byte[] resources,
        int directory,
        ResourceIdentifier identifier) {

        uint target = FindEntry(resources, directory, identifier);
        Assert.NotEqual(0U, target & 0x80000000U);
        return checked((int)(target & 0x7fffffffU));
    }

    private static int FindDataEntry(
        byte[] resources,
        int directory,
        ResourceIdentifier identifier) {

        uint target = FindEntry(resources, directory, identifier);
        Assert.Equal(0U, target & 0x80000000U);
        return checked((int)target);
    }

    private static uint FindEntry(
        byte[] resources,
        int directory,
        ResourceIdentifier identifier) {

        int named = ReadUInt16(resources, directory + 12);
        int ids = ReadUInt16(resources, directory + 14);
        for (int index = 0; index < named + ids; index++) {
            int entry = directory + 16 + index * 8;
            uint key = ReadUInt32(resources, entry);
            bool isName = (key & 0x80000000U) != 0;
            if (identifier.IsName == isName &&
                (isName
                    ? string.Equals(
                        ReadResourceName(resources, key),
                        identifier.Text,
                        StringComparison.Ordinal)
                    : (key & 0xffffU) == identifier.Value)) {
                return ReadUInt32(resources, entry + 4);
            }
        }
        throw new InvalidDataException(
            $"Resource entry '{identifier}' was not found.");
    }

    private static string ReadResourceName(byte[] resources, uint key) {
        int offset = checked((int)(key & 0x7fffffffU));
        int length = ReadUInt16(resources, offset);
        return Encoding.Unicode.GetString(
            resources,
            offset + 2,
            length * 2);
    }

    private static int FindSignature(byte[] data, string signature) {
        byte[] expected = Encoding.ASCII.GetBytes(signature);
        for (int offset = 0;
             offset <= data.Length - expected.Length;
             offset++) {
            if (data.AsSpan(offset, expected.Length).SequenceEqual(expected)) {
                return offset;
            }
        }
        throw new InvalidDataException(
            $"Signature '{signature}' was not found.");
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private readonly record struct ResourceIdentifier(
        bool IsName,
        string Text,
        uint Value) {

        internal static ResourceIdentifier Name(string value) =>
            new(true, value, 0);

        internal static ResourceIdentifier Id(uint value) =>
            new(false, string.Empty, value);

        public override string ToString() => IsName ? Text : Value.ToString();
    }
}
