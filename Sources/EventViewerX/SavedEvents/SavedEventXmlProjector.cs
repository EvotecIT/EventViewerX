using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace EventViewerX;

/// <summary>Projects parser-produced event XML into the parser-neutral saved-event contract.</summary>
public static class SavedEventXmlProjector {
    /// <summary>Creates a normalized saved event from rendered EVTX XML.</summary>
    /// <param name="xml">One complete Event XML document.</param>
    /// <param name="fallbackRecordId">Record identifier used when XML omits EventRecordID.</param>
    /// <param name="fallbackTimeCreatedUtc">UTC timestamp used when XML omits TimeCreated.</param>
    public static SavedEventRecord Create(
        string xml,
        long? fallbackRecordId = null,
        DateTime? fallbackTimeCreatedUtc = null) {

        if (string.IsNullOrWhiteSpace(xml)) {
            throw new ArgumentException("Event XML cannot be null or empty.", nameof(xml));
        }
        XDocument document;
        try {
            using var textReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(textReader, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            document = XDocument.Load(reader, LoadOptions.None);
        } catch (XmlException exception) {
            throw new InvalidDataException("The parser produced invalid event XML.", exception);
        }
        XElement system = document.Descendants().FirstOrDefault(Is("System")) ??
            throw new InvalidDataException("Event XML does not contain a System element.");
        XElement? provider = system.Elements().FirstOrDefault(Is("Provider"));
        XElement? correlation = system.Elements().FirstOrDefault(Is("Correlation"));
        XElement? execution = system.Elements().FirstOrDefault(Is("Execution"));
        XElement? timeCreated = system.Elements().FirstOrDefault(Is("TimeCreated"));
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddEventData(document, data);
        AddUserData(document, data);

        return new SavedEventRecord {
            ProviderName = Attribute(provider, "Name"),
            ProviderId = GuidValue(Attribute(provider, "Guid")),
            EventId = RequiredInt(ElementValue(system, "EventID"), "EventID"),
            RecordId = LongValue(ElementValue(system, "EventRecordID")) ?? fallbackRecordId,
            Channel = ElementValue(system, "Channel"),
            Computer = ElementValue(system, "Computer"),
            TimeCreatedUtc = DateTimeValue(Attribute(timeCreated, "SystemTime")) ??
                fallbackTimeCreatedUtc?.ToUniversalTime() ??
                throw new InvalidDataException("Event XML does not contain a valid TimeCreated/SystemTime value."),
            Version = ByteValue(ElementValue(system, "Version")),
            Level = ByteValue(ElementValue(system, "Level")),
            Task = IntValue(ElementValue(system, "Task")),
            Opcode = ShortValue(ElementValue(system, "Opcode")),
            Keywords = KeywordsValue(ElementValue(system, "Keywords")),
            ProcessId = IntValue(Attribute(execution, "ProcessID")),
            ThreadId = IntValue(Attribute(execution, "ThreadID")),
            ActivityId = GuidValue(Attribute(correlation, "ActivityID")),
            RelatedActivityId = GuidValue(Attribute(correlation, "RelatedActivityID")),
            RawXml = xml,
            Data = data,
            MessageRenderStatus = EventMessageRenderStatus.MessageResourceUnavailable
        };
    }

    private static Func<XElement, bool> Is(string localName) =>
        element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal);

    private static string ElementValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(Is(localName))?.Value ?? string.Empty;

    private static string Attribute(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(attribute =>
            string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))?.Value ?? string.Empty;

    private static void AddEventData(XDocument document, Dictionary<string, string> data) {
        XElement? eventData = document.Descendants().FirstOrDefault(Is("EventData"));
        if (eventData == null) {
            return;
        }
        int unnamedIndex = 0;
        foreach (XElement element in eventData.Elements()) {
            string name = Attribute(element, "Name");
            if (string.IsNullOrWhiteSpace(name)) {
                if (string.IsNullOrEmpty(element.Value)) {
                    continue;
                }
                name = $"NoNameA{unnamedIndex++}";
            }
            data[name] = element.Value;
        }
    }

    private static void AddUserData(XDocument document, Dictionary<string, string> data) {
        XElement? userData = document.Descendants().FirstOrDefault(Is("UserData"));
        if (userData == null) {
            return;
        }
        foreach (XElement element in userData.Descendants().Where(static item => !item.Elements().Any())) {
            data[element.Name.LocalName] = element.Value;
        }
    }

    private static int RequiredInt(string value, string fieldName) =>
        IntValue(value) ?? throw new InvalidDataException($"Event XML does not contain a valid {fieldName} value.");

    private static int? IntValue(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : null;

    private static long? LongValue(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : null;

    private static short? ShortValue(string value) =>
        short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short result) ? result : null;

    private static byte? ByteValue(string value) =>
        byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result) ? result : null;

    private static Guid? GuidValue(string value) =>
        Guid.TryParse(value, out Guid result) ? result : null;

    private static DateTime? DateTimeValue(string value) =>
        DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime result)
            ? result
            : null;

    private static long? KeywordsValue(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            ulong.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex)) {
            return unchecked((long)hex);
        }
        return LongValue(value);
    }
}
