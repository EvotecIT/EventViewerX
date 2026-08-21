using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Security.Principal;
using EventViewerX.Native;

namespace EventViewerX;

public partial class EventObject {
    private static readonly string[] MetadataPropertyPaths = {
        "Event/System/EventID",
        "Event/System/EventID/@Qualifiers",
        "Event/System/EventRecordID",
        "Event/System/TimeCreated/@SystemTime",
        "Event/System/Provider/@Name",
        "Event/System/Provider/@Guid",
        "Event/System/Computer",
        "Event/System/Channel",
        "Event/System/Level",
        "Event/System/Keywords",
        "Event/System/Task",
        "Event/System/Opcode",
        "Event/System/Execution/@ProcessID",
        "Event/System/Execution/@ThreadID",
        "Event/System/Correlation/@ActivityID",
        "Event/System/Correlation/@RelatedActivityID",
        "Event/System/Security/@UserID",
        "Event/System/Version"
    };

    internal static EventLogPropertySelector CreateMetadataPropertySelector() {
        return new EventLogPropertySelector(MetadataPropertyPaths);
    }

    internal static EventObject CreateMetadata(
        EventRecord eventRecord,
        EventLogPropertySelector selector,
        string queriedMachine,
        string containerLog) {

        if (eventRecord == null) {
            throw new ArgumentNullException(nameof(eventRecord));
        }
        if (selector == null) {
            throw new ArgumentNullException(nameof(selector));
        }
        if (eventRecord is not EventLogRecord logRecord) {
            return new EventObject(eventRecord, queriedMachine, EventReadMode.Metadata);
        }

        IList<object> values;
        try {
            values = logRecord.GetPropertyValues(selector);
        } catch (EventLogException ex) {
            Settings._logger.WriteVerbose(
                "Falling back to direct metadata projection after EventLogPropertySelector failed. ({0})",
                ex.Message);
            return new EventObject(eventRecord, queriedMachine, EventReadMode.Metadata);
        }

        try {
            return new EventObject(values, bookmark: null, queriedMachine, containerLog);
        } finally {
            eventRecord.Dispose();
        }
    }

    private EventObject(
        IList<object> values,
        EventBookmark? bookmark,
        string queriedMachine,
        string containerLog) {

        if (values == null) {
            throw new ArgumentNullException(nameof(values));
        }
        if (values.Count < MetadataPropertyPaths.Length) {
            throw new ArgumentException("The metadata property selector returned an incomplete event.", nameof(values));
        }

        ReadMode = EventReadMode.Metadata;
        QueriedMachine = queriedMachine ?? string.Empty;
        _message = string.Empty;
        Id = ToInt32(values[0]) ?? 0;
        Qualifiers = values[1]?.ToString();
        RecordId = ToInt64(values[2]);
        TimeCreated = ToDateTime(values[3]);
        ProviderName = values[4]?.ToString() ?? string.Empty;
        ProviderId = ToGuid(values[5]);
        MachineName = values[6]?.ToString() ?? string.Empty;
        LogName = values[7]?.ToString() ?? string.Empty;
        Level = ToByte(values[8]);
        Keywords = ToInt64Bits(values[9]);
        Task = ToInt32(values[10]);
        Opcode = ToInt16(values[11]);
        ProcessId = ToInt32(values[12]);
        ThreadId = ToInt32(values[13]);
        ActivityId = ToGuid(values[14]);
        RelatedActivityId = ToGuid(values[15]);
        UserId = values[16] as SecurityIdentifier;
        Version = ToByte(values[17]);
        Bookmark = bookmark;
        Properties = Array.Empty<EventPropertyValue>();
        MatchedQueryIds = Array.Empty<int>();
        TaskDisplayName = string.Empty;
        OpcodeDisplayName = string.Empty;
        KeywordsDisplayNames = Array.Empty<string>();
        LevelDisplayName = LevelToDisplayName(Level);
        ContainerLog = string.IsNullOrEmpty(containerLog)
            ? LogName
            : containerLog;
        GatheredFrom = string.IsNullOrEmpty(QueriedMachine) ? Environment.MachineName : QueriedMachine;
        GatheredLogName = ContainerLog;
    }

    internal EventObject(
        NativeEventMetadata metadata,
        string queriedMachine,
        string containerLog)
        : this(
            metadata,
            bookmark: null,
            queriedMachine,
            containerLog) {
    }

    internal EventObject(
        NativeEventMetadata metadata,
        EventBookmark? bookmark,
        string queriedMachine,
        string containerLog)
        : this(
            metadata,
            EventReadMode.Metadata,
            queriedMachine,
            containerLog,
            string.Empty,
            string.Empty,
            EventMessageRenderStatus.NotRequested,
            0,
            bookmark,
            Array.Empty<EventPropertyValue>(),
            LevelToDisplayName(metadata.Level),
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            string.Empty,
            parsePayload: false,
            includeAttachments: false) {
    }

    internal EventObject(
        NativeEventMessage message,
        string queriedMachine,
        string containerLog)
        : this(
            message.Metadata,
            EventReadMode.Message,
            queriedMachine,
            containerLog,
            message.Message,
            message.CultureName,
            message.RenderStatus,
            message.RenderErrorCode,
            message.Bookmark,
            Array.Empty<EventPropertyValue>(),
            message.LevelDisplayName,
            message.TaskDisplayName,
            message.OpcodeDisplayName,
            message.KeywordDisplayNames,
            string.Empty,
            parsePayload: false,
            includeAttachments: false) {
    }

    internal EventObject(
        NativeEventStructured structured,
        string queriedMachine,
        string containerLog)
        : this(
            structured.Metadata,
            EventReadMode.StructuredData,
            queriedMachine,
            containerLog,
            string.Empty,
            string.Empty,
            EventMessageRenderStatus.NotRequested,
            0,
            structured.Bookmark,
            structured.Properties,
            LevelToDisplayName(structured.Metadata.Level),
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            structured.Xml,
            parsePayload: true,
            includeAttachments: false) {
    }

    internal EventObject(
        NativeEventMetadata metadata,
        string xml,
        EventBookmark? bookmark,
        string queriedMachine,
        string containerLog)
        : this(
            metadata,
            EventReadMode.RawXml,
            queriedMachine,
            containerLog,
            string.Empty,
            string.Empty,
            EventMessageRenderStatus.NotRequested,
            0,
            bookmark,
            Array.Empty<EventPropertyValue>(),
            LevelToDisplayName(metadata.Level),
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            xml,
            parsePayload: true,
            includeAttachments: false) {
    }

    internal EventObject(
        NativeEventFull full,
        string queriedMachine,
        string containerLog)
        : this(
            full,
            queriedMachine,
            containerLog,
            EventReadMode.Full) {
    }

    internal EventObject(
        NativeEventFull full,
        string queriedMachine,
        string containerLog,
        EventReadMode readMode)
        : this(
            full.Message.Metadata,
            readMode,
            queriedMachine,
            containerLog,
            full.Message.Message,
            full.Message.CultureName,
            full.Message.RenderStatus,
            full.Message.RenderErrorCode,
            full.Structured.Bookmark,
            full.Structured.Properties,
            full.Message.LevelDisplayName,
            full.Message.TaskDisplayName,
            full.Message.OpcodeDisplayName,
            full.Message.KeywordDisplayNames,
            full.Structured.Xml,
            parsePayload: true,
            includeAttachments: readMode == EventReadMode.Full) {

        if (readMode != EventReadMode.Full &&
            readMode != EventReadMode.StructuredDataAndMessage) {
            throw new ArgumentOutOfRangeException(
                nameof(readMode),
                readMode,
                "A full native projection requires Full or StructuredDataAndMessage mode.");
        }
    }

    private EventObject(
        NativeEventMetadata metadata,
        EventReadMode readMode,
        string queriedMachine,
        string containerLog,
        string message,
        string messageCulture,
        EventMessageRenderStatus messageRenderStatus,
        int messageRenderErrorCode,
        EventBookmark? bookmark,
        IReadOnlyList<EventPropertyValue> properties,
        string levelDisplayName,
        string taskDisplayName,
        string opcodeDisplayName,
        IReadOnlyList<string> keywordDisplayNames,
        string xml,
        bool parsePayload,
        bool includeAttachments) {

        ReadMode = readMode;
        QueriedMachine = queriedMachine ?? string.Empty;
        _payloadParsingEnabled = parsePayload;
        _includeAttachments = includeAttachments;
        _message = message;
        MessageCulture = messageCulture;
        MessageRenderStatus = messageRenderStatus;
        MessageRenderErrorCode = messageRenderErrorCode;
        Id = metadata.Id;
        Qualifiers = metadata.Qualifiers?.ToString(CultureInfo.InvariantCulture);
        RecordId = metadata.RecordId;
        TimeCreated = metadata.TimeCreated;
        ProviderName = metadata.ProviderName;
        ProviderId = metadata.ProviderId;
        MachineName = metadata.MachineName;
        LogName = metadata.LogName;
        Level = metadata.Level;
        Keywords = metadata.Keywords;
        Task = metadata.Task;
        Opcode = metadata.Opcode;
        ProcessId = metadata.ProcessId;
        ThreadId = metadata.ThreadId;
        ActivityId = metadata.ActivityId;
        RelatedActivityId = metadata.RelatedActivityId;
        UserId = metadata.UserId;
        Version = metadata.Version;
        Bookmark = bookmark;
        Properties = properties;
        MatchedQueryIds = Array.Empty<int>();
        TaskDisplayName = taskDisplayName;
        OpcodeDisplayName = opcodeDisplayName;
        KeywordsDisplayNames = keywordDisplayNames;
        LevelDisplayName = levelDisplayName;
        ContainerLog = string.IsNullOrEmpty(containerLog)
            ? LogName
            : containerLog;
        GatheredFrom = string.IsNullOrEmpty(QueriedMachine) ? Environment.MachineName : QueriedMachine;
        GatheredLogName = ContainerLog;
        XMLData = xml;

    }

    private static byte? ToByte(object? value) {
        return value == null ? null : Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    private static short? ToInt16(object? value) {
        return value == null ? null : Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }

    private static int? ToInt32(object? value) {
        if (value == null) {
            return null;
        }
        return value is uint unsigned
            ? unchecked((int)unsigned)
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static long? ToInt64(object? value) {
        if (value == null) {
            return null;
        }
        return value is ulong unsigned
            ? unchecked((long)unsigned)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static long? ToInt64Bits(object? value) {
        return ToInt64(value);
    }

    private static DateTime ToDateTime(object? value) {
        if (value is DateTime dateTime) {
            return dateTime;
        }
        return value == null
            ? DateTime.MinValue
            : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }

    private static Guid? ToGuid(object? value) {
        if (value is Guid guid) {
            return guid;
        }
        return value != null && Guid.TryParse(value.ToString(), out Guid parsed)
            ? parsed
            : null;
    }
}
