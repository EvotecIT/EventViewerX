namespace EventViewerX.Storage;

/// <summary>Validated portable EventStore backup artifact.</summary>
public sealed class EventStoreBackupResult {
    internal EventStoreBackupResult(string path, long bytes, string sha256, DateTime createdAtUtc) {
        Path = path;
        Bytes = bytes;
        Sha256 = sha256;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Absolute backup path.</summary>
    public string Path { get; }
    /// <summary>Backup size.</summary>
    public long Bytes { get; }
    /// <summary>Uppercase SHA-256 checksum.</summary>
    public string Sha256 { get; }
    /// <summary>UTC backup completion time.</summary>
    public DateTime CreatedAtUtc { get; }
}
