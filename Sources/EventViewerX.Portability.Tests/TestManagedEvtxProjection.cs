using System.Reflection;
using System.Text;
using System.Xml.Linq;
using EventViewerX.Evtx;
using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestManagedEvtxProjection {
    [Fact]
    public void TemplateWriterEscapesStaticTextAndAttributesExactlyOnce() {
        using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx"));
        var log = new evtx.EventLog(stream);
        evtx.EventRecord record = log.GetEventRecords().First();
        evtx.IBinXml instance = record.Nodes.Single(static node => node.TagType == evtx.Tags.TagBuilder.BinaryTag.TemplateInstance);
        var template = (evtx.Tags.Template)instance.GetType().GetProperty("Template")!.GetValue(instance)!;
        evtx.Tags.OpenStartElementTag root = template.Nodes.OfType<evtx.Tags.OpenStartElementTag>().Single();
        var chunk = (evtx.ChunkInfo)typeof(evtx.Tags.OpenStartElementTag)
            .GetField("_chunk", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(root)!;
        uint nameOffset = chunk.StringTableEntries.Single(static pair => pair.Value.Value == "Name").Key;
        const string expected = "A&B<C>\"'\t\r\n";
        using var attributeBytes = new MemoryStream();
        using (var writer = new BinaryWriter(attributeBytes, Encoding.UTF8, leaveOpen: true)) {
            writer.Write(9);
            writer.Write((byte)6);
            writer.Write(nameOffset);
            writer.Write((byte)5);
            writer.Write((byte)1);
            writer.Write((short)expected.Length);
            writer.Write(Encoding.Unicode.GetBytes(expected));
        }
        attributeBytes.Position = 5;
        using var attributeReader = new BinaryReader(attributeBytes);
        root.Attributes.Add(new evtx.Tags.Attribute(record.RecordPosition, attributeReader, chunk));
        using var valueBytes = new MemoryStream();
        using (var writer = new BinaryWriter(valueBytes, Encoding.UTF8, leaveOpen: true)) {
            writer.Write((byte)1);
            writer.Write((short)expected.Length);
            writer.Write(Encoding.Unicode.GetBytes(expected));
        }
        valueBytes.Position = 0;
        using var valueReader = new BinaryReader(valueBytes);
        root.Nodes.Insert(root.Nodes.Count - 1, new evtx.Tags.Value(record.RecordPosition, valueReader, chunk));

        XElement actual = XDocument.Parse(new EvtxTemplateXmlRenderer().Render(record), LoadOptions.PreserveWhitespace).Root!;

        Assert.Equal(expected, (string?)actual.Attribute("Name"));
        Assert.Equal(expected, actual.Nodes().OfType<XText>().Single().Value);
    }

    [Theory]
    [InlineData("A&B<Cxxxx")]
    [InlineData("A\tBxxxxxx")]
    [InlineData("A\r\nBxxxxx")]
    [InlineData("&lt;xxxxx")]
    [InlineData("A😀Bxxxxx")]
    [InlineData("A\u200CBxxxxxx")]
    public void TemplateStringPayloadPreservesOriginalSubstitutionText(string expected) {
        string path = CreateStringFixture(expected);
        try {
            SavedEventRecord first = new EvtxSavedEventReader().Read(new EventLogFileQuery(path) {
                Oldest = true, MaxEvents = 1
            }).First();
            Assert.Equal(37, first.RecordId);
            Assert.Equal(expected, first.Data["param1"]);
            Assert.Equal(expected, XDocument.Parse(first.RawXml).Descendants()
                .Single(element => element.Name.LocalName == "Data" && (string?)element.Attribute("Name") == "param1").Value);
            SavedEventRecord filtered = new EvtxSavedEventReader().Read(new EventLogFileQuery(path) {
                Oldest = true, XPath = $"*[EventData[Data[@Name='param1']='{expected}']]"
            }).Single();
            Assert.Equal(first.RecordId, filtered.RecordId);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void XmlInvalidStringPayloadReportsFidelityFailureInsteadOfReplacingCharacters() {
        string path = CreateStringFixture("A\u0001Bxxxxxx");
        var diagnostics = new List<SavedEventReadDiagnostic>();
        try {
            Assert.Throws<InvalidDataException>(() => new EvtxSavedEventReader()
                .Read(new EventLogFileQuery(path) { Oldest = true }, diagnostics.Add).ToArray());
            SavedEventReadDiagnostic diagnostic = Assert.Single(diagnostics, static item => item.Code == "EVXEVTX201");
            Assert.Equal(4608, diagnostic.FileOffset);
            Assert.Equal(SavedEventReadDiagnosticSeverity.Error, diagnostic.Severity);
        } finally {
            File.Delete(path);
        }
    }

    private static string CreateStringFixture(string expected) {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx"));
        Assert.Equal("SmsRouter", Encoding.Unicode.GetString(bytes, 6500, 18));
        byte[] replacement = Encoding.Unicode.GetBytes(expected);
        Assert.Equal(18, replacement.Length);
        Buffer.BlockCopy(replacement, 0, bytes, 6500, replacement.Length);
        const int chunk = 4096;
        int freeSpace = checked((int)BitConverter.ToUInt32(bytes, chunk + 0x30));
        Buffer.BlockCopy(BitConverter.GetBytes(Force.Crc32.Crc32Algorithm.Compute(bytes,
            chunk + 512, freeSpace - 512)), 0, bytes, chunk + 0x34, 4);
        var header = new byte[120 + 384];
        Buffer.BlockCopy(bytes, chunk, header, 0, 120);
        Buffer.BlockCopy(bytes, chunk + 128, header, 120, 384);
        Buffer.BlockCopy(BitConverter.GetBytes(Force.Crc32.Crc32Algorithm.Compute(header)),
            0, bytes, chunk + 0x7C, 4);
        string path = Path.Combine(Path.GetTempPath(), $"EventViewerX-text-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void CompactNodeProjectionPreservesTheDependencyRendererMetadataAndPayload() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        using FileStream source = File.OpenRead(path);
        var dependency = new evtx.EventLog(source);
        SavedEventRecord[] reference = dependency.GetEventRecords()
            .Select(record => SavedEventXmlProjector.Create(record.ConvertPayloadToXml(),
                record.RecordNumber, record.Timestamp.UtcDateTime)).ToArray();
        SavedEventRecord[] actual = new EvtxSavedEventReader().Read(new EventLogFileQuery(path) { Oldest = true }).ToArray();
        Assert.Equal(184, actual.Length);
        Assert.Equal(reference.Length, actual.Length);
        PropertyInfo[] metadata = typeof(SavedEventRecord).GetProperties()
            .Where(property => property.Name != nameof(SavedEventRecord.RawXml) &&
                               property.Name != nameof(SavedEventRecord.Data) &&
                               property.Name != nameof(SavedEventRecord.FileOffset)).ToArray();
        for (int index = 0; index < reference.Length; index++) {
            foreach (PropertyInfo property in metadata) {
                Assert.Equal(property.GetValue(reference[index]), property.GetValue(actual[index]));
            }
            Assert.Equal(reference[index].Data.OrderBy(static pair => pair.Key, StringComparer.Ordinal),
                actual[index].Data.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
            XElement root = XDocument.Parse(actual[index].RawXml).Root!;
            Assert.Equal("Event", root.Name.LocalName);
            Assert.Equal("http://schemas.microsoft.com/win/2004/08/events/event", root.Name.NamespaceName);
            Assert.True(actual[index].FileOffset > 0);
        }
    }
}
