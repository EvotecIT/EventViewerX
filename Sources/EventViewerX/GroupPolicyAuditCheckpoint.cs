namespace EventViewerX;

/// <summary>Resumable position for one queried event-log container.</summary>
public sealed class GroupPolicyAuditCheckpoint {
    /// <summary>Computer or offline file that was queried.</summary>
    public string QueryTarget { get; set; } = string.Empty;

    /// <summary>Container channel or offline file that held the event.</summary>
    public string ContainerLogName { get; set; } = string.Empty;

    /// <summary>Portable native bookmark used to resume after this event.</summary>
    public string BookmarkXml { get; set; } = string.Empty;

    /// <summary>Record identifier exposed by the event payload, when present.</summary>
    public long? RecordId { get; set; }

    /// <summary>Creation time of the event at this checkpoint.</summary>
    public DateTime TimeCreatedUtc { get; set; }

    /// <summary>Query ordering used when this checkpoint was captured.</summary>
    public bool Oldest { get; set; } = true;

    /// <summary>Stable key for resolving the checkpoint in a subsequent query.</summary>
    public string SourceKey => CreateSourceKey(QueryTarget, ContainerLogName);

    /// <summary>Creates a stable source key from a query target and container.</summary>
    public static string CreateSourceKey(string? queryTarget, string containerLogName) {
        if (string.IsNullOrWhiteSpace(containerLogName)) {
            throw new ArgumentException("Container log name is required.", nameof(containerLogName));
        }
        string target = EventLogTarget.IsLocalMachine(queryTarget)
            ? Environment.MachineName
            : queryTarget!.Trim();
        string container = containerLogName.Trim();
        return target.ToUpperInvariant() + "|" + container.ToUpperInvariant();
    }

    internal GroupPolicyAuditCheckpoint Copy() => new() {
        QueryTarget = QueryTarget,
        ContainerLogName = ContainerLogName,
        BookmarkXml = BookmarkXml,
        RecordId = RecordId,
        TimeCreatedUtc = TimeCreatedUtc,
        Oldest = Oldest
    };
}
