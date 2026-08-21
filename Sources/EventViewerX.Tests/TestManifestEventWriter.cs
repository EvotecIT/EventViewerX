using System.Runtime.InteropServices;
using System.Text;
using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestManifestEventWriter {
    [Fact]
    public void OrdersNamedValuesAndRejectsMissingOrUnknownFields() {
        var definition = new ManifestEventDefinition {
            ProviderName = "Provider",
            ProviderId = Guid.NewGuid(),
            Id = 42,
            PayloadFields = new[] {
                new ManifestEventPayloadField {
                    Index = 0,
                    Name = "ComputerName",
                    InputType = "win:UnicodeString"
                },
                new ManifestEventPayloadField {
                    Index = 1,
                    Name = "FindingCount",
                    InputType = "win:UInt32"
                }
            }
        };

        IReadOnlyList<object?> ordered =
            ManifestEventWriter.OrderNamedPayload(
                definition,
                new Dictionary<string, object?> {
                    ["findingcount"] = 7U,
                    ["COMPUTERNAME"] = "EVOMAGIC"
                });

        Assert.Equal(
            new object?[] {
                "EVOMAGIC",
                7U
            },
            ordered);
        Assert.Throws<ArgumentException>(() =>
            ManifestEventWriter.OrderNamedPayload(
                definition,
                new Dictionary<string, object?> {
                    ["ComputerName"] = "EVOMAGIC"
                }));
        Assert.Throws<ArgumentException>(() =>
            ManifestEventWriter.OrderNamedPayload(
                definition,
                new Dictionary<string, object?> {
                    ["ComputerName"] = "EVOMAGIC",
                    ["FindingCount"] = 7U,
                    ["Unexpected"] = true
                }));
    }

    [Fact]
    public void ParsesOrderedManifestPayloadFields() {
        const string template =
            "<template xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">" +
            "<data name=\"Name\" inType=\"win:UnicodeString\"/>" +
            "<data name=\"Code\" inType=\"win:UInt32\" " +
            "outType=\"xs:unsignedInt\" map=\"CodeMap\" " +
            "length=\"NameLength\" count=\"2\"/>" +
            "</template>";

        IReadOnlyList<ManifestEventPayloadField> fields =
            ManifestEventWriter.ParsePayloadFields(template);

        Assert.Collection(
            fields,
            field => {
                Assert.Equal(0, field.Index);
                Assert.Equal("Name", field.Name);
                Assert.Equal("win:UnicodeString", field.InputType);
            },
            field => {
                Assert.Equal(1, field.Index);
                Assert.Equal("Code", field.Name);
                Assert.Equal("win:UInt32", field.InputType);
                Assert.Equal("xs:unsignedInt", field.OutputType);
                Assert.Equal("CodeMap", field.Map);
                Assert.Equal("NameLength", field.Length);
                Assert.Equal("2", field.Count);
            });
    }

    [Fact]
    public void ResolvesDescriptorAndCombinesKeywords() {
        EventProviderEventMetadataSnapshot selected = CreateEvent(
            42,
            version: 3,
            logName: "Operational",
            channelId: 16,
            level: 4,
            opcode: 2,
            task: 7,
            keywords: new[] { 1L, 8L });

        ManifestEventDefinition definition =
            ManifestEventWriter.ResolveDefinition(
                "Provider",
                Guid.Parse("117b45c1-cae2-4f1e-8d5d-76debf7f96dd"),
                new[] { "Admin", "Operational" },
                new[] { selected },
                42,
                null);

        Assert.Equal(42, definition.Id);
        Assert.Equal(3, definition.Version);
        Assert.Equal(16, definition.Channel);
        Assert.Equal(4, definition.Level);
        Assert.Equal(2, definition.Opcode);
        Assert.Equal(7, definition.Task);
        Assert.Equal(9, definition.Keywords);
        Assert.Equal("Operational", definition.LogName);
    }

    [Fact]
    public void RequiresVersionWhenEventIdIsAmbiguous() {
        EventProviderEventMetadataSnapshot[] events = {
            CreateEvent(42, version: 0),
            CreateEvent(42, version: 1)
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ManifestEventWriter.ResolveDefinition(
                "Provider",
                Guid.NewGuid(),
                Array.Empty<string>(),
                events,
                42,
                null));

        Assert.Contains("multiple versions", exception.Message);
    }

    [Fact]
    public void RejectsPayloadCountBeforeCallingNativeProvider() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("Name", "win:UnicodeString"));
        var provider = new RecordingProvider(0);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ManifestEventWriter.Write(
                definition,
                Array.Empty<object?>(),
                provider));

        Assert.Contains("expects 1", exception.Message);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void ReturnsNativeStatusAndResolvedDefinition() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("Name", "win:UnicodeString"));
        var provider = new RecordingProvider(123);

        ManifestEventWriteResult result = ManifestEventWriter.Write(
            definition,
            new object?[] { "value" },
            provider);

        Assert.Same(definition, result.Definition);
        Assert.Equal(1, result.PayloadCount);
        Assert.Equal(123U, result.NativeStatus);
        Assert.False(result.Success);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void EncodesManifestTypesUsingSchemaInsteadOfRuntimeGuessing() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("Text", "win:UnicodeString"),
            Field("Number", "win:UInt32"),
            Field("Enabled", "win:Boolean"),
            Field("Identifier", "win:Guid"),
            Field("Bytes", "win:Binary", length: "3"));
        Guid identifier =
            Guid.Parse("d4065c83-f2ed-4e8c-915a-99ab387a4929");
        object?[] payload = {
            123,
            "42",
            "true",
            identifier.ToString(),
            new byte[] { 1, 2, 3 }
        };

        using var buffer =
            new ManifestEventPayloadBuffer(definition, payload);

        Assert.Equal(
            Encoding.Unicode.GetBytes("123\0"),
            Read(buffer.Descriptors[0]));
        Assert.Equal(BitConverter.GetBytes(42U), Read(buffer.Descriptors[1]));
        Assert.Equal(BitConverter.GetBytes(1), Read(buffer.Descriptors[2]));
        Assert.Equal(identifier.ToByteArray(), Read(buffer.Descriptors[3]));
        Assert.Equal(new byte[] { 1, 2, 3 }, Read(buffer.Descriptors[4]));
    }

    [Fact]
    public void EncodesLengthDelimitedValuesAndCountedArrays() {
        const string template =
            "<template xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">" +
            "<data name=\"TextLength\" inType=\"win:UInt16\"/>" +
            "<data name=\"Text\" inType=\"win:UnicodeString\" length=\"TextLength\"/>" +
            "<data name=\"ValueCount\" inType=\"win:UInt16\"/>" +
            "<data name=\"Values\" inType=\"win:UInt32\" count=\"ValueCount\"/>" +
            "<data name=\"BinaryLength\" inType=\"win:UInt16\"/>" +
            "<data name=\"Bytes\" inType=\"win:Binary\" length=\"BinaryLength\"/>" +
            "</template>";
        ManifestEventDefinition definition = CreateDefinition(
            ManifestEventWriter.ParsePayloadFields(template)
                .ToArray());
        object?[] payload = {
            4,
            "ABC",
            2,
            new uint[] { 10, 20 },
            3,
            new byte[] { 1, 2, 3 }
        };

        using var buffer =
            new ManifestEventPayloadBuffer(
                definition,
                payload);

        Assert.Equal(
            Encoding.Unicode.GetBytes("ABC\0"),
            Read(buffer.Descriptors[1]));
        Assert.Equal(
            BitConverter.GetBytes(10U)
                .Concat(BitConverter.GetBytes(20U))
                .ToArray(),
            Read(buffer.Descriptors[3]));
        Assert.Equal(
            new byte[] { 1, 2, 3 },
            Read(buffer.Descriptors[5]));
    }

    [Fact]
    public void EncodesShortFixedLengthStringsWithNullPadding() {
        if (!OperatingSystem.IsWindows()) return;
        ManifestEventDefinition definition = CreateDefinition(
            Field(
                "UnicodeText",
                "win:UnicodeString",
                length: "5"),
            Field(
                "AnsiText",
                "win:AnsiString",
                length: "5"));

        using var buffer =
            new ManifestEventPayloadBuffer(
                definition,
                new object?[] {
                    "A",
                    "B"
                });

        Assert.Equal(
            Encoding.Unicode.GetBytes(
                "A\0\0\0\0"),
            Read(buffer.Descriptors[0]));
        Assert.Equal(
            new byte[] {
                (byte)'B',
                0,
                0,
                0,
                0
            },
            Read(buffer.Descriptors[1]));
    }

    [Theory]
    [InlineData("win:UnicodeString")]
    [InlineData("win:AnsiString")]
    public void RejectsOversizedReferencedStringLengthsBeforePadding(
        string inputType) {

        ManifestEventDefinition definition = CreateDefinition(
            Field("TextLength", "win:UInt32"),
            Field(
                "Text",
                inputType,
                length: "TextLength"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManifestEventPayloadBuffer(
                definition,
                new object?[] {
                    int.MaxValue,
                    "A"
                }));
    }

    [Fact]
    public void RejectsOversizedReferencedCountsBeforeEnumeratingValues() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("ValueCount", "win:UInt32"),
            Field(
                "Values",
                "win:UInt32",
                count: "ValueCount"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ManifestEventPayloadBuffer(
                definition,
                new object?[] {
                    int.MaxValue,
                    Array.Empty<uint>()
                }));
    }

    [Fact]
    public void EncodesZeroLengthAndCountedBinaryPayloadsWithoutPadding() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("EmptyText", "win:UnicodeString", length: "0"),
            Field("EmptyBytes", "win:Binary", length: "0"),
            Field(
                "BinaryValues",
                "win:Binary",
                length: "2",
                count: "2"));
        object?[] payload = {
            string.Empty,
            Array.Empty<byte>(),
            new[] {
                new byte[] { 1, 2 },
                new byte[] { 3, 4 }
            }
        };

        using var buffer =
            new ManifestEventPayloadBuffer(
                definition,
                payload);

        Assert.Equal(0U, buffer.Descriptors[0].Size);
        Assert.Equal(0UL, buffer.Descriptors[0].Pointer);
        Assert.Equal(0U, buffer.Descriptors[1].Size);
        Assert.Equal(0UL, buffer.Descriptors[1].Pointer);
        Assert.Equal(
            new byte[] { 1, 2, 3, 4 },
            Read(buffer.Descriptors[2]));
    }

    [Fact]
    public void RejectsBinaryPayloadWithoutManifestLength() {
        ManifestEventDefinition definition = CreateDefinition(
            Field("Bytes", "win:Binary"));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                new ManifestEventPayloadBuffer(
                    definition,
                    new object?[] {
                        new byte[] { 1 }
                    }));

        Assert.Contains(
            "does not declare a length",
            exception.Message);
    }

    [Fact]
    public void ReadsNumericChannelIdFromRegisteredProviderManifest() {
        byte channelId =
            WindowsEventProviderManifestMetadata.GetChannelId(
                "Microsoft-Windows-PowerShell",
                4100,
                1);

        Assert.Equal(16, channelId);
    }

    private static byte[] Read(
        WindowsManifestEventProvider.EventDataDescriptor descriptor) {

        byte[] bytes = new byte[descriptor.Size];
        Marshal.Copy(
            new IntPtr(unchecked((long)descriptor.Pointer)),
            bytes,
            0,
            bytes.Length);
        return bytes;
    }

    private static EventProviderEventMetadataSnapshot CreateEvent(
        long id,
        byte version,
        string logName = "",
        byte channelId = 0,
        int? level = null,
        int? opcode = null,
        int? task = null,
        IReadOnlyList<long>? keywords = null) {

        return new EventProviderEventMetadataSnapshot(
            id,
            version,
            logName,
            channelId,
            level,
            opcode,
            task,
            keywords ?? Array.Empty<long>(),
            "<template><data name=\"Value\" inType=\"win:UnicodeString\"/></template>",
            string.Empty);
    }

    private static ManifestEventDefinition CreateDefinition(
        params ManifestEventPayloadField[] fields) {

        return new ManifestEventDefinition {
            ProviderName = "Provider",
            ProviderId = Guid.NewGuid(),
            Id = 1,
            PayloadFields = fields
        };
    }

    private static ManifestEventPayloadField Field(
        string name,
        string inputType,
        string length = "",
        string count = "") {

        return new ManifestEventPayloadField {
            Name = name,
            InputType = inputType,
            Length = length,
            Count = count
        };
    }

    private sealed class RecordingProvider : IManifestEventProvider {
        private readonly uint _status;

        internal RecordingProvider(uint status) {
            _status = status;
        }

        internal int CallCount { get; private set; }

        public uint Write(
            ManifestEventDefinition definition,
            IReadOnlyList<object?> payload) {

            CallCount++;
            return _status;
        }
    }
}
