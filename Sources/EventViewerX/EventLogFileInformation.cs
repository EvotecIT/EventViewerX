namespace EventViewerX;

/// <summary>Native metadata for an offline Windows Event Log file.</summary>
public sealed class EventLogFileInformation {
    internal EventLogFileInformation(
        string path,
        DateTime creationTimeUtc,
        DateTime lastAccessTimeUtc,
        DateTime lastWriteTimeUtc,
        long fileSize,
        uint attributes,
        long recordCount,
        long oldestRecordNumber,
        bool isFull) {

        Path = path;
        CreationTimeUtc = creationTimeUtc;
        LastAccessTimeUtc = lastAccessTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
        FileSize = fileSize;
        Attributes = attributes;
        RecordCount = recordCount;
        OldestRecordNumber = oldestRecordNumber;
        IsFull = isFull;
    }

    /// <summary>Absolute source path.</summary>
    public string Path { get; }
    /// <summary>Log creation timestamp.</summary>
    public DateTime CreationTimeUtc { get; }
    /// <summary>Last access timestamp.</summary>
    public DateTime LastAccessTimeUtc { get; }
    /// <summary>Last write timestamp.</summary>
    public DateTime LastWriteTimeUtc { get; }
    /// <summary>Native log file size in bytes.</summary>
    public long FileSize { get; }
    /// <summary>Native file attribute bitmask.</summary>
    public uint Attributes { get; }
    /// <summary>Number of records in the log.</summary>
    public long RecordCount { get; }
    /// <summary>Oldest retained record number.</summary>
    public long OldestRecordNumber { get; }
    /// <summary>Whether Windows reports the log as full.</summary>
    public bool IsFull { get; }
}
