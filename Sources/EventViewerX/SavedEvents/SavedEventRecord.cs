using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Parser-neutral saved event preserving identity, three core source fields, raw payload, and message fidelity.
/// Provider-formatted messages may be unavailable when provider resources or Windows rendering APIs are absent.
/// </summary>
public sealed class SavedEventRecord {
    /// <summary>Provider that emitted the event.</summary>
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>Native event identifier.</summary>
    public int EventId { get; set; }
    /// <summary>Record identifier within the original channel.</summary>
    public long? RecordId { get; set; }
    /// <summary>Original source channel.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>Computer that emitted the event.</summary>
    public string Computer { get; set; } = string.Empty;
    /// <summary>UTC source-event timestamp.</summary>
    public DateTime TimeCreatedUtc { get; set; }
    /// <summary>Optional provider GUID.</summary>
    public Guid? ProviderId { get; set; }
    /// <summary>Optional event version.</summary>
    public byte? Version { get; set; }
    /// <summary>Optional event level.</summary>
    public byte? Level { get; set; }
    /// <summary>Optional event task.</summary>
    public int? Task { get; set; }
    /// <summary>Optional event opcode.</summary>
    public short? Opcode { get; set; }
    /// <summary>Optional event keywords.</summary>
    public long? Keywords { get; set; }
    /// <summary>Optional process identifier.</summary>
    public int? ProcessId { get; set; }
    /// <summary>Optional thread identifier.</summary>
    public int? ThreadId { get; set; }
    /// <summary>Optional activity identifier.</summary>
    public Guid? ActivityId { get; set; }
    /// <summary>Optional related activity identifier.</summary>
    public Guid? RelatedActivityId { get; set; }
    /// <summary>Raw event XML when the parser can preserve it.</summary>
    public string RawXml { get; set; } = string.Empty;
    /// <summary>Named event payload values.</summary>
    public IReadOnlyDictionary<string, string> Data { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Provider-formatted message when available.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Culture used to render Message, when known.</summary>
    public string MessageCulture { get; set; } = string.Empty;
    /// <summary>Explicit message-rendering fidelity.</summary>
    public EventMessageRenderStatus MessageRenderStatus { get; set; } =
        EventMessageRenderStatus.MessageResourceUnavailable;
    /// <summary>Platform or provider error code for failed rendering.</summary>
    public int MessageRenderErrorCode { get; set; }
    /// <summary>Whether the parser recovered this record from a damaged region.</summary>
    public bool Recovered { get; set; }
    /// <summary>Optional byte offset of this record in the source container.</summary>
    public long? FileOffset { get; set; }

    internal EventObject ToEventObject(string sourcePath, EventReadMode readMode) {
        Validate();
        var metadata = new NativeEventMetadata(
            ProviderName,
            ProviderId,
            EventId,
            qualifiers: null,
            Level,
            Task,
            Opcode,
            Keywords,
            TimeCreatedUtc.ToUniversalTime(),
            RecordId,
            ActivityId,
            RelatedActivityId,
            ProcessId,
            ThreadId,
            Channel,
            Computer,
            userId: null,
            Version);
        var message = new NativeEventMessage(
            metadata,
            Message,
            levelDisplayName: string.Empty,
            taskDisplayName: string.Empty,
            opcodeDisplayName: string.Empty,
            keywordDisplayNames: Array.Empty<string>(),
            bookmark: null,
            MessageCulture,
            MessageRenderStatus,
            MessageRenderErrorCode);
        var structured = new NativeEventStructured(
            metadata,
            RawXml,
            Array.Empty<EventPropertyValue>(),
            bookmark: null);
        EventObject result = readMode switch {
            EventReadMode.Metadata => new EventObject(metadata, sourcePath, sourcePath),
            EventReadMode.Message => new EventObject(message, sourcePath, sourcePath),
            EventReadMode.StructuredData => new EventObject(structured, sourcePath, sourcePath),
            EventReadMode.RawXml => new EventObject(metadata, RawXml, null, sourcePath, sourcePath),
            EventReadMode.StructuredDataAndMessage => new EventObject(
                new NativeEventFull(message, structured), sourcePath, sourcePath, readMode),
            EventReadMode.Full => new EventObject(new NativeEventFull(message, structured), sourcePath, sourcePath),
            _ => throw new ArgumentOutOfRangeException(nameof(readMode))
        };
        foreach (KeyValuePair<string, string> item in Data) {
            result.Data[item.Key] = item.Value;
        }
        result.QuerySourceKind = EventLogQuerySourceKind.File;
        return result;
    }

    private void Validate() {
        ProviderName = ProviderName?.Trim() ?? string.Empty;
        Channel = Channel?.Trim() ?? string.Empty;
        Computer = Computer?.Trim() ?? string.Empty;
        RawXml ??= string.Empty;
        Message ??= string.Empty;
        MessageCulture ??= string.Empty;
        if (EventId < 0) {
            throw new InvalidDataException("Saved event ID cannot be negative.");
        }
        if (TimeCreatedUtc == default) {
            throw new InvalidDataException("Saved event timestamp is required.");
        }
        if (!Enum.IsDefined(typeof(EventMessageRenderStatus), MessageRenderStatus)) {
            throw new InvalidDataException("Saved event message-render status is not supported.");
        }
        if (Data == null || Data.Any(static item => string.IsNullOrWhiteSpace(item.Key))) {
            throw new InvalidDataException("Saved event data keys cannot be null or empty.");
        }
    }
}
