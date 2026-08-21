namespace EventViewerX;

/// <summary>Result of a completed direct event export.</summary>
public sealed class EventExportResult {
    internal EventExportResult(
        string path,
        EventExportFormat format,
        long eventCount,
        long bytes,
        string? sha256) {

        Path = path;
        Format = format;
        EventCount = eventCount;
        Bytes = bytes;
        Sha256 = sha256;
    }

    /// <summary>Absolute output path.</summary>
    public string Path { get; }

    /// <summary>Output format.</summary>
    public EventExportFormat Format { get; }

    /// <summary>Number of exported records.</summary>
    public long EventCount { get; }

    /// <summary>Final file size.</summary>
    public long Bytes { get; }

    /// <summary>
    /// Uppercase SHA-256 of the completed file, or null when hashing was skipped.
    /// </summary>
    public string? Sha256 { get; }
}
