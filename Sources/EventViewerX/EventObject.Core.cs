using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Principal;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Managed snapshot of a Windows event record.
/// </summary>
/// <remarks>
/// The constructor takes ownership of the supplied <see cref="EventRecord"/> and disposes it after
/// copying the requested data. This keeps large and long-running queries from retaining native event handles.
/// </remarks>
public partial class EventObject {
    private Dictionary<string, string>? _data;
    private readonly string _message;
    private Dictionary<string, string>? _messageData;
    private string[]? _messageLines;
    private string? _messageSubject;
    private List<string>? _nicIdentifiers;
    private IReadOnlyList<byte[]>? _attachments;
    private object? _payloadSync = new();
    private bool _payloadParsingEnabled;
    private bool _includeAttachments;
    private volatile bool _payloadParsed;

    /// <summary>Time and date when the event was created.</summary>
    public DateTime TimeCreated { get; }

    /// <summary>Event identifier.</summary>
    public int Id { get; }

    /// <summary>Record identifier.</summary>
    public long? RecordId { get; }

    /// <summary>Log name reported by the event.</summary>
    public string LogName { get; }

    /// <summary>
    /// Original channel recorded in the event payload. For forwarded events this remains the source channel,
    /// such as Security, while <see cref="ContainerLogName"/> identifies ForwardedEvents.
    /// </summary>
    public string OriginalLogName => LogName;

    /// <summary>Log name that contained the event.</summary>
    public string ContainerLog { get; set; } = string.Empty;

    /// <summary>Channel or file container from which the event was read.</summary>
    public string ContainerLogName => ContainerLog;

    /// <summary>Computer that created the event.</summary>
    public string ComputerName => MachineName;

    /// <summary>Computer that originally created the event.</summary>
    public string SourceComputer => MachineName;

    /// <summary>Display-friendly event level.</summary>
    public string LevelDisplayName { get; }

    /// <summary>Provider that generated the event.</summary>
    public string ProviderName { get; }

    /// <summary>Additional event qualifiers, when present.</summary>
    public string? Qualifiers { get; }

    /// <summary>Event opcode.</summary>
    public short? Opcode { get; }

    /// <summary>Provider identifier.</summary>
    public Guid? ProviderId { get; }

    /// <summary>Related activity identifier.</summary>
    public Guid? RelatedActivityId { get; }

    /// <summary>Activity identifier.</summary>
    public Guid? ActivityId { get; }

    /// <summary>Security identifier associated with the event.</summary>
    public SecurityIdentifier? UserId { get; }

    /// <summary>
    /// Bookmark that can be used to resume a query. Metadata-only snapshots omit bookmarks to preserve
    /// the low-allocation path; set IncludeBookmark on the query when a native bookmark is required.
    /// </summary>
    public EventBookmark? Bookmark { get; }

    /// <summary>
    /// Portable XML representation of <see cref="Bookmark"/> that can be passed to a subsequent query.
    /// </summary>
    /// <remarks>
    /// Unlike the framework bookmark object, this property is available consistently on .NET Framework,
    /// modern .NET, Windows PowerShell 5.1, and PowerShell 7.
    /// </remarks>
    public string? BookmarkXml => Bookmark == null ? null : EventBookmarkXml.Get(Bookmark);

    /// <summary>Provider-formatted event message, when requested by <see cref="ReadMode"/>.</summary>
    public string Message => _message;

    /// <summary>Culture used to format <see cref="Message"/> and provider display names.</summary>
    public string MessageCulture { get; } = string.Empty;

    /// <summary>Outcome of provider message rendering for this snapshot.</summary>
    public EventMessageRenderStatus MessageRenderStatus { get; }

    /// <summary>Windows or runtime error code when message rendering did not succeed; otherwise zero.</summary>
    public int MessageRenderErrorCode { get; }

    /// <summary>Message split into CRLF/LF-delimited lines.</summary>
    /// <remarks>The split is created only when a caller requests the lines or parsed message fields.</remarks>
    public IReadOnlyList<string> MessageLines {
        get => _messageLines ??= SplitMessageLines(_message);
        private set => _messageLines = value as string[] ?? value?.ToArray() ?? Array.Empty<string>();
    }

    /// <summary>Display name of the task.</summary>
    public string TaskDisplayName { get; }

    /// <summary>Display name of the opcode.</summary>
    public string OpcodeDisplayName { get; }

    /// <summary>Keyword display names associated with the event.</summary>
    public IEnumerable<string> KeywordsDisplayNames { get; }

    /// <summary>Keyword flags associated with the event.</summary>
    public long? Keywords { get; }

    /// <summary>Numeric event level.</summary>
    public byte? Level { get; }

    /// <summary>Event version.</summary>
    public byte? Version { get; }

    /// <summary>Task identifier.</summary>
    public int? Task { get; }

    /// <summary>Process identifier.</summary>
    public int? ProcessId { get; }

    /// <summary>Thread identifier.</summary>
    public int? ThreadId { get; }

    /// <summary>Computer that created the event.</summary>
    public string MachineName { get; }

    /// <summary>Event property values copied from the native record.</summary>
    public IReadOnlyList<EventPropertyValue> Properties { get; }

    /// <summary>
    /// Query identifiers whose Select expressions matched this event in a
    /// structured QueryList.
    /// </summary>
    /// <remarks>
    /// Windows documents <c>EvtEventQueryIDs</c> as unsupported. Owned native
    /// projections therefore return an empty collection instead of inventing
    /// matches; snapshots created from a managed <see cref="EventLogRecord"/>
    /// preserve whatever identifiers that API supplies.
    /// </remarks>
    public IReadOnlyList<int> MatchedQueryIds { get; }

    /// <summary>Structured event data parsed from XML.</summary>
    public Dictionary<string, string> Data {
        get {
            EnsurePayloadParsed();
            return _data!;
        }
        private set => _data = value;
    }

    /// <summary>NIC-related identifiers extracted from structured event data.</summary>
    public List<string> NicIdentifiers {
        get {
            EnsurePayloadParsed();
            return _nicIdentifiers!;
        }
        private set => _nicIdentifiers = value;
    }

    /// <summary>Key/value pairs parsed from the formatted message.</summary>
    public Dictionary<string, string> MessageData {
        get => _messageData ??= ParseMessage(_message);
        private set => _messageData = value;
    }

    /// <summary>First non-empty line of the formatted message.</summary>
    /// <remarks>The subject is derived lazily so message-only scans do not split every formatted message.</remarks>
    public string MessageSubject {
        get => _messageSubject ??= GetMessageSubject(_message);
        set => _messageSubject = value ?? string.Empty;
    }

    /// <summary>Binary attachments extracted from structured event data.</summary>
    /// <remarks>
    /// XML parsing and binary decoding occur only when structured properties are
    /// first requested. Enumerating rich snapshots without touching payload
    /// helpers therefore remains bounded and avoids unnecessary parsing work.
    /// </remarks>
    public IReadOnlyList<byte[]> Attachments {
        get {
            EnsurePayloadParsed();
            return _attachments!;
        }
        private set => _attachments = value;
    }

    /// <summary>Raw event XML, when requested by <see cref="ReadMode"/>.</summary>
    public string XMLData { get; set; } = string.Empty;

    /// <summary>Machine name or file path from which the event was queried.</summary>
    public string QueriedMachine { get; set; } = string.Empty;

    /// <summary>
    /// Collector or direct query target from which the event was retrieved. This is distinct from
    /// <see cref="SourceComputer"/> for Windows Event Collector subscriptions.
    /// </summary>
    public string CollectorComputer => QueriedMachine;

    /// <summary>Computer name or file path from which the event was gathered.</summary>
    public string GatheredFrom { get; set; } = string.Empty;

    /// <summary>Log name from which the event was gathered.</summary>
    public string GatheredLogName { get; set; } = string.Empty;

    /// <summary>Amount of provider data materialized for this snapshot.</summary>
    public EventReadMode ReadMode { get; }

    /// <summary>Whether the query read this event from a live channel or an offline event-log file.</summary>
    public EventLogQuerySourceKind QuerySourceKind { get; internal set; }

    /// <summary>
    /// Creates an event snapshot and releases the supplied native event record.
    /// </summary>
    /// <param name="eventRecord">Event record whose ownership is transferred to this constructor.</param>
    /// <param name="queriedMachine">Computer name or file path from which the event was read.</param>
    /// <param name="readMode">Amount of provider data to materialize.</param>
    /// <param name="includeBookmark">Whether to materialize a resumable bookmark.</param>
    public EventObject(
        EventRecord eventRecord,
        string queriedMachine,
        EventReadMode readMode = EventReadMode.Full,
        bool includeBookmark = false) {

        if (eventRecord == null) {
            throw new ArgumentNullException(nameof(eventRecord));
        }

        ReadMode = readMode;
        QueriedMachine = queriedMachine ?? string.Empty;
        _message = string.Empty;
        _payloadParsingEnabled =
            readMode == EventReadMode.StructuredData ||
            readMode == EventReadMode.RawXml ||
            readMode == EventReadMode.Full ||
            readMode == EventReadMode.StructuredDataAndMessage;
        _includeAttachments = readMode == EventReadMode.Full;

        try {
            TimeCreated = eventRecord.TimeCreated ?? DateTime.MinValue;
            Id = eventRecord.Id;
            RecordId = eventRecord.RecordId;
            LogName = eventRecord.LogName ?? string.Empty;
            MachineName = eventRecord.MachineName ?? string.Empty;
            ProviderName = eventRecord.ProviderName ?? string.Empty;
            Qualifiers = eventRecord.Qualifiers?.ToString();
            Opcode = eventRecord.Opcode;
            ProviderId = eventRecord.ProviderId;
            RelatedActivityId = eventRecord.RelatedActivityId;
            ActivityId = eventRecord.ActivityId;
            UserId = eventRecord.UserId;
            Bookmark = includeBookmark
                ? eventRecord.Bookmark
                : null;
            Keywords = eventRecord.Keywords;
            Level = eventRecord.Level;
            Version = eventRecord.Version;
            Task = eventRecord.Task;
            ProcessId = eventRecord.ProcessId;
            ThreadId = eventRecord.ThreadId;
            Properties = readMode == EventReadMode.StructuredData ||
                         readMode == EventReadMode.Full ||
                         readMode == EventReadMode.StructuredDataAndMessage
                ? SnapshotProperties(eventRecord)
                : Array.Empty<EventPropertyValue>();
            MatchedQueryIds = eventRecord is EventLogRecord matchedRecord
                ? matchedRecord.MatchedQueryIds?.ToArray() ??
                  Array.Empty<int>()
                : Array.Empty<int>();
            bool includeProviderDisplayNames =
                readMode == EventReadMode.Message ||
                readMode == EventReadMode.Full ||
                readMode == EventReadMode.StructuredDataAndMessage;
            TaskDisplayName = includeProviderDisplayNames
                ? SafeReadDisplayName(() => eventRecord.TaskDisplayName)
                : string.Empty;
            OpcodeDisplayName = includeProviderDisplayNames
                ? SafeReadDisplayName(() => eventRecord.OpcodeDisplayName)
                : string.Empty;
            KeywordsDisplayNames = includeProviderDisplayNames
                ? SafeReadKeywordDisplayNames(eventRecord)
                : Array.Empty<string>();

            string levelDisplayName = includeProviderDisplayNames
                ? SafeReadDisplayName(() => eventRecord.LevelDisplayName)
                : string.Empty;
            LevelDisplayName = string.IsNullOrEmpty(levelDisplayName)
                ? LevelToDisplayName(Level)
                : levelDisplayName;

            ContainerLog = eventRecord is EventLogRecord eventLogRecord
                ? eventLogRecord.ContainerLog ?? string.Empty
                : LogName;
            GatheredFrom = string.IsNullOrEmpty(QueriedMachine) ? Environment.MachineName : QueriedMachine;
            GatheredLogName = ContainerLog;

            if (readMode == EventReadMode.Message ||
                readMode == EventReadMode.Full ||
                readMode == EventReadMode.StructuredDataAndMessage) {
                _message = SafeFormatDescription(
                    eventRecord,
                    out EventMessageRenderStatus renderStatus,
                    out int renderErrorCode);
                MessageCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                MessageRenderStatus = renderStatus;
                MessageRenderErrorCode = renderErrorCode;
            }

            if (readMode == EventReadMode.StructuredData ||
                readMode == EventReadMode.RawXml ||
                readMode == EventReadMode.Full ||
                readMode == EventReadMode.StructuredDataAndMessage) {
                XMLData = SafeToXml(eventRecord);
            }
        } finally {
            eventRecord.Dispose();
        }
    }

    private static IReadOnlyList<EventPropertyValue> SnapshotProperties(EventRecord eventRecord) {
        try {
            return eventRecord.Properties?
                .Select(static property => new EventPropertyValue(property.Value))
                .ToArray() ?? Array.Empty<EventPropertyValue>();
        } catch (EventLogException ex) {
            Settings._logger.WriteVerbose("Failed to snapshot event properties. ({0})", ex.Message);
            return Array.Empty<EventPropertyValue>();
        }
    }

    private static string SafeReadDisplayName(Func<string?> readValue) {
        try {
            return readValue() ?? string.Empty;
        } catch (EventLogException) {
            return string.Empty;
        }
    }

    private static IEnumerable<string> SafeReadKeywordDisplayNames(EventRecord eventRecord) {
        try {
            return eventRecord.KeywordsDisplayNames?.ToArray() ?? Array.Empty<string>();
        } catch (EventLogException) {
            return Array.Empty<string>();
        }
    }

    private static string SafeFormatDescription(
        EventRecord eventRecord,
        out EventMessageRenderStatus renderStatus,
        out int renderErrorCode) {

        try {
            string message = eventRecord.FormatDescription() ?? string.Empty;
            renderStatus = EventMessageRenderStatus.Rendered;
            renderErrorCode = 0;
            return message;
        } catch (EventLogNotFoundException ex) {
            Settings._logger.WriteWarning("Failed to format event description due to missing provider metadata. ({0})", ex.Message);
            renderStatus = EventMessageRenderStatus.ProviderMetadataUnavailable;
            renderErrorCode = ex.HResult;
            return string.Empty;
        } catch (EventLogException ex) {
            Settings._logger.WriteWarning("Failed to format event description. ({0})", ex.Message);
            renderStatus = EventMessageRenderStatus.Failed;
            renderErrorCode = ex.HResult;
            return string.Empty;
        } catch (Exception ex) {
            Settings._logger.WriteWarning("Unexpected error while formatting event description. ({0})", ex.Message);
            renderStatus = EventMessageRenderStatus.Failed;
            renderErrorCode = ex.HResult;
            return string.Empty;
        }
    }

    private static string SafeToXml(EventRecord eventRecord) {
        try {
            return eventRecord.ToXml() ?? string.Empty;
        } catch (EventLogException ex) {
            Settings._logger.WriteWarning("Failed to read event XML payload. ({0})", ex.Message);
            return string.Empty;
        } catch (Exception ex) {
            Settings._logger.WriteWarning("Unexpected error while reading event XML payload. ({0})", ex.Message);
            return string.Empty;
        }
    }

    private static string LevelToDisplayName(byte? level) {
        return level switch {
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => level?.ToString() ?? string.Empty
        };
    }
}
