namespace EventViewerX.Storage;

/// <summary>SQLite integrity, schema, size, and row-count evidence for one EventStore.</summary>
public sealed class EventStoreIntegrityResult {
    internal EventStoreIntegrityResult(
        bool isHealthy,
        IReadOnlyList<string> diagnostics,
        int schemaVersion,
        int eventIdentityVersion,
        int findingSchemaVersion,
        long eventCount,
        long findingCount,
        long databaseBytes) {

        IsHealthy = isHealthy;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        SchemaVersion = schemaVersion;
        EventIdentityVersion = eventIdentityVersion;
        FindingSchemaVersion = findingSchemaVersion;
        EventCount = eventCount;
        FindingCount = findingCount;
        DatabaseBytes = databaseBytes;
    }

    /// <summary>Whether SQLite and every supported EventViewerX schema contract passed validation.</summary>
    public bool IsHealthy { get; }
    /// <summary>Integrity and compatibility diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
    /// <summary>Base EventStore schema version.</summary>
    public int SchemaVersion { get; }
    /// <summary>Event identity contract version.</summary>
    public int EventIdentityVersion { get; }
    /// <summary>Durable finding contract version.</summary>
    public int FindingSchemaVersion { get; }
    /// <summary>Stored source-event rows.</summary>
    public long EventCount { get; }
    /// <summary>Stored detection-finding rows.</summary>
    public long FindingCount { get; }
    /// <summary>Current primary database file size.</summary>
    public long DatabaseBytes { get; }
}
