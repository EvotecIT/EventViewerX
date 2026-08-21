using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using EventViewerX;
using EventViewerX.Reporting;

namespace EventLogParsing.BenchmarkHost;

internal sealed class EventAccumulator {
    private const long OrderModulus = 1_000_000_007;
    private const long OrderMultiplier = 1_000_003;

    public long Count { get; private set; }

    public long IdSum { get; private set; }

    public long RecordIdSum { get; private set; }

    public long TimeTicksXor { get; private set; }

    public long OrderSignature { get; private set; }

    public long? FirstRecordId { get; private set; }

    public long? LastRecordId { get; private set; }

    public long MetadataTouch { get; private set; }

    public long MessageCharacters { get; private set; }

    public long XmlCharacters { get; private set; }

    public long PropertyCount { get; private set; }

    public long StructuredFieldCount { get; private set; }

    public long MessageFieldCount { get; private set; }

    public long AttachmentBytes { get; private set; }

    public void Add(EventObject eventObject, EventReadMode readMode) {
        ArgumentNullException.ThrowIfNull(eventObject);

        AddMetadata(
            eventObject.Id,
            eventObject.RecordId,
            eventObject.TimeCreated,
            eventObject.ProviderName,
            eventObject.MachineName,
            eventObject.LogName,
            eventObject.Level,
            eventObject.Keywords,
            eventObject.Task,
            eventObject.Opcode,
            eventObject.ProcessId,
            eventObject.ThreadId);

        if (readMode is EventReadMode.Message or EventReadMode.Full or EventReadMode.StructuredDataAndMessage) {
            MessageCharacters += eventObject.Message.Length;
            if (readMode == EventReadMode.Full) {
                MessageFieldCount += eventObject.MessageData.Count;
            }
        }

        if (readMode is EventReadMode.StructuredData or EventReadMode.Full or EventReadMode.StructuredDataAndMessage) {
            XmlCharacters += eventObject.XMLData.Length;
            PropertyCount += eventObject.Properties.Count;
            StructuredFieldCount += eventObject.Data.Count;
            foreach (byte[] attachment in eventObject.Attachments) {
                AttachmentBytes += attachment.LongLength;
            }
        }
    }

    public void Add(EventReportRow row) {
        ArgumentNullException.ThrowIfNull(row);
        AddMetadata(
            row.EventId,
            row.RecordId,
            row.TimeCreated,
            row.Provider,
            row.SourceComputer,
            row.SourceLog,
            null,
            null,
            null,
            null,
            null,
            null);
        MessageCharacters += row.Message.Length;
        StructuredFieldCount += row.Values.Count;
    }

    public void Add(EventRecord record, EventReadMode readMode) {
        ArgumentNullException.ThrowIfNull(record);

        AddMetadata(
            record.Id,
            record.RecordId,
            record.TimeCreated ?? DateTime.MinValue,
            record.ProviderName ?? string.Empty,
            record.MachineName ?? string.Empty,
            record.LogName ?? string.Empty,
            record.Level,
            record.Keywords,
            record.Task,
            record.Opcode,
            record.ProcessId,
            record.ThreadId);

        if (readMode is EventReadMode.Message or EventReadMode.Full or EventReadMode.StructuredDataAndMessage) {
            string message = SafeRead(record.FormatDescription);
            MessageCharacters += message.Length;
            MetadataTouch += SafeRead(() => record.LevelDisplayName).Length;
            MetadataTouch += SafeRead(() => record.TaskDisplayName).Length;
            MetadataTouch += SafeRead(() => record.OpcodeDisplayName).Length;
            MetadataTouch += SafeReadKeywordCount(record);
        }

        if (readMode is EventReadMode.StructuredData or EventReadMode.Full or EventReadMode.StructuredDataAndMessage) {
            PropertyCount += SafeReadPropertyCount(record);
            string xml = SafeRead(record.ToXml);
            XmlCharacters += xml.Length;
        }
    }

    public void AddSelected(IList<object> values) {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 18) {
            throw new ArgumentException("The property selector did not return the expected metadata fields.", nameof(values));
        }

        AddMetadata(
            ToInt32(values[0]) ?? 0,
            ToInt64(values[1]),
            ToDateTime(values[2]),
            values[3]?.ToString() ?? string.Empty,
            values[4]?.ToString() ?? string.Empty,
            values[5]?.ToString() ?? string.Empty,
            ToByte(values[6]),
            ToInt64Bits(values[7]),
            ToInt32(values[8]),
            ToInt16(values[9]),
            ToInt32(values[10]),
            ToInt32(values[11]));
        MetadataTouch += values[12]?.ToString()?.Length ?? 0;
        MetadataTouch += values[13] != null ? 1 : 0;
        MetadataTouch += values[14] != null ? 1 : 0;
        MetadataTouch += values[15] != null ? 1 : 0;
        MetadataTouch += values[16]?.ToString()?.Length ?? 0;
        MetadataTouch += values[17] != null ? 1 : 0;
    }

    private void AddMetadata(
        int id,
        long? recordId,
        DateTime timeCreated,
        string providerName,
        string machineName,
        string logName,
        byte? level,
        long? keywords,
        int? task,
        short? opcode,
        int? processId,
        int? threadId) {

        Count++;
        IdSum += id;
        if (recordId.HasValue) {
            RecordIdSum += recordId.Value;
            FirstRecordId ??= recordId.Value;
            LastRecordId = recordId.Value;
        }
        TimeTicksXor ^= timeCreated.Ticks;
        OrderSignature = (
            (OrderSignature * OrderMultiplier)
            + (recordId.GetValueOrDefault() % OrderModulus)
            + ((id % OrderModulus) * 31L)
            + ((timeCreated.Ticks % OrderModulus) * 17L)
        ) % OrderModulus;
        MetadataTouch += providerName.Length;
        MetadataTouch += machineName.Length;
        MetadataTouch += logName.Length;
        MetadataTouch += level.HasValue ? 1 : 0;
        MetadataTouch += keywords.HasValue ? 1 : 0;
        MetadataTouch += task.HasValue ? 1 : 0;
        MetadataTouch += opcode.HasValue ? 1 : 0;
        MetadataTouch += processId.HasValue ? 1 : 0;
        MetadataTouch += threadId.HasValue ? 1 : 0;
    }

    private static string SafeRead(Func<string?> read) {
        try {
            return read() ?? string.Empty;
        } catch (EventLogException) {
            return string.Empty;
        }
    }

    private static int SafeReadKeywordCount(EventRecord record) {
        try {
            return record.KeywordsDisplayNames?.Count() ?? 0;
        } catch (EventLogException) {
            return 0;
        }
    }

    private static int SafeReadPropertyCount(EventRecord record) {
        try {
            return record.Properties?.Count ?? 0;
        } catch (EventLogException) {
            return 0;
        }
    }

    private static byte? ToByte(object? value) {
        return value == null ? null : Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    private static short? ToInt16(object? value) {
        return value == null ? null : Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }

    private static int? ToInt32(object? value) {
        return value == null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static long? ToInt64(object? value) {
        return value == null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static long? ToInt64Bits(object? value) {
        if (value == null) {
            return null;
        }
        return value is ulong unsigned
            ? unchecked((long)unsigned)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static DateTime ToDateTime(object? value) {
        if (value is DateTime dateTime) {
            return dateTime;
        }
        return value == null
            ? DateTime.MinValue
            : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }
}
