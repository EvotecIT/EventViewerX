namespace EventViewerX;

/// <summary>A captured diagnostic record with source coordinates. Text evidence never fabricates Windows event IDs.</summary>
public sealed class EventDiagnosticRecord {
    /// <summary>Stable source-generation and byte-range identity.</summary>
    public string Identity { get; set; } = string.Empty;
    /// <summary>Caller-supplied source identity, independent of the reader's machine.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Collector-declared device identity. Empty forbids cross-file application correlation.</summary>
    public string Device { get; set; } = string.Empty;
    /// <summary>File generation identity.</summary>
    public string Generation { get; set; } = string.Empty;
    /// <summary>Inclusive starting byte offset.</summary>
    public long ByteStart { get; set; }
    /// <summary>Exclusive ending byte offset.</summary>
    public long ByteEnd { get; set; }
    /// <summary>One-based first physical line.</summary>
    public long LineStart { get; set; }
    /// <summary>One-based last physical line.</summary>
    public long LineEnd { get; set; }
    /// <summary>Exact decoded frame, including CMTrace metadata.</summary>
    public string RawText { get; set; } = string.Empty;
    /// <summary>Decoded message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>CMTrace, PlainText, Malformed, or Oversized.</summary>
    public string Format { get; set; } = string.Empty;
    /// <summary>Original date/time text; no host-local conversion is performed.</summary>
    public string RawTime { get; set; } = string.Empty;
    /// <summary>Known instant, or null when the writer's offset is unknown.</summary>
    public DateTimeOffset? Timestamp { get; set; }
    /// <summary>EmbeddedOffset, SuppliedOffset, UnknownOffset, InvalidTime, or NotProvided.</summary>
    public string TimeQuality { get; set; } = "NotProvided";
    /// <summary>Writer-declared component.</summary>
    public string Component { get; set; } = string.Empty;
    /// <summary>Writer-declared thread identifier, retained as text.</summary>
    public string Thread { get; set; } = string.Empty;
    /// <summary>Writer-declared context.</summary>
    public string Context { get; set; } = string.Empty;
    /// <summary>Collector-declared execution context, kept separate from writer metadata.</summary>
    public string CaptureContext { get; set; } = "Unknown";
    /// <summary>Writer-declared severity type; this alone is not an application outcome.</summary>
    public int? Severity { get; set; }
    /// <summary>Parsing or bound diagnostic.</summary>
    public string? Diagnostic { get; set; }
}

/// <summary>Bounded incremental text-log read configuration. Defaults are suitable for offline captures.</summary>
public sealed class EventDiagnosticReadOptions {
    /// <summary>Source identity supplied by the collector. Defaults to the full input path.</summary>
    public string? Source { get; set; }
    /// <summary>Collector-declared device identity; never inferred from the reader host.</summary>
    public string Device { get; set; } = string.Empty;
    /// <summary>Writer UTC offset when the record does not contain one. Never inferred from the collector.</summary>
    public TimeSpan? UtcOffset { get; set; }
    /// <summary>Maximum bytes read in one call.</summary>
    public int MaximumBatchBytes { get; set; } = 8 * 1024 * 1024;
    /// <summary>Maximum records returned in one call.</summary>
    public int MaximumRecords { get; set; } = 10_000;
    /// <summary>Maximum frame bytes retained. Oversized frames are skipped through their boundary.</summary>
    public int MaximumRecordBytes { get; set; } = 256 * 1024;
    /// <summary>Allows a complete final plain-text line without a newline. False for files still being written.</summary>
    public bool FinalFile { get; set; } = true;
}

/// <summary>Serializable restart position. Fingerprints cover the prefix and resume boundary, not every historical byte.</summary>
public sealed class EventDiagnosticCheckpoint {
    /// <summary>Checkpoint format version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Full input path.</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>Physical file identity.</summary>
    public string FileIdentity { get; set; } = string.Empty;
    /// <summary>Current evidence generation.</summary>
    public string Generation { get; set; } = string.Empty;
    /// <summary>Next unread byte.</summary>
    public long Offset { get; set; }
    /// <summary>Next physical line number.</summary>
    public long NextLine { get; set; } = 1;
    /// <summary>Prefix fingerprint at the last checkpoint.</summary>
    public string PrefixHash { get; set; } = string.Empty;
    /// <summary>Number of prefix bytes fingerprinted.</summary>
    public int PrefixLength { get; set; }
    /// <summary>Fingerprint of up to 4 KiB immediately preceding the next byte.</summary>
    public string BoundaryHash { get; set; } = string.Empty;
    /// <summary>Encoding used for the generation.</summary>
    public string Encoding { get; set; } = string.Empty;
}

/// <summary>One bounded read, including the durable restart position and coverage limits.</summary>
public sealed class EventDiagnosticReadResult {
    /// <summary>Complete records in source order.</summary>
    public EventDiagnosticRecord[] Records { get; set; } = Array.Empty<EventDiagnosticRecord>();
    /// <summary>Position after the last complete frame.</summary>
    public EventDiagnosticCheckpoint Checkpoint { get; set; } = new();
    /// <summary>Rotation, replacement, truncation, or fingerprint change caused a new generation.</summary>
    public bool GenerationChanged { get; set; }
    /// <summary>Unconsumed bytes remain, including an incomplete trailing frame.</summary>
    public bool HasMore { get; set; }
    /// <summary>True only when all bytes formed valid complete records without bounds or decoding losses.</summary>
    public bool IsComplete { get; set; }
    /// <summary>Coverage explanation, including bounded fingerprint limitations.</summary>
    public string[] Diagnostics { get; set; } = Array.Empty<string>();
}