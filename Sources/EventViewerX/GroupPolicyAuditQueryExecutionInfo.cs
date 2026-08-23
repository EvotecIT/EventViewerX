using System.Collections.Concurrent;

namespace EventViewerX;

/// <summary>Reports Group Policy audit query progress, failures, and resumable source checkpoints.</summary>
public sealed class GroupPolicyAuditQueryExecutionInfo {
    private readonly ConcurrentDictionary<string, GroupPolicyAuditCheckpoint> _checkpoints =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of candidate Directory Service Changes events examined.</summary>
    public long EventsScanned { get; internal set; }

    /// <summary>Number of matching Group Policy audit events emitted.</summary>
    public long EventsEmitted { get; internal set; }

    /// <summary>Whether another candidate existed after the configured scan cap.</summary>
    public bool ScanLimitReached { get; internal set; }

    /// <summary>Whether another matching result existed after the configured result cap.</summary>
    public bool ResultLimitReached { get; internal set; }

    /// <summary>Whether either configured bound stopped the query before natural completion.</summary>
    public bool IsTruncated => ScanLimitReached || ResultLimitReached;

    /// <summary>Expected remote-target failures isolated while healthy targets continued.</summary>
    public IReadOnlyList<EventLogQueryTargetFailure> TargetFailures { get; internal set; } =
        Array.Empty<EventLogQueryTargetFailure>();

    /// <summary>Latest resumable checkpoint reached for each queried container.</summary>
    public IReadOnlyList<GroupPolicyAuditCheckpoint> Checkpoints => _checkpoints.Values
        .OrderBy(static checkpoint => checkpoint.QueryTarget, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static checkpoint => checkpoint.ContainerLogName, StringComparer.OrdinalIgnoreCase)
        .Select(static checkpoint => checkpoint.Copy())
        .ToArray();

    internal void Reset() {
        EventsScanned = 0;
        EventsEmitted = 0;
        ScanLimitReached = false;
        ResultLimitReached = false;
        TargetFailures = Array.Empty<EventLogQueryTargetFailure>();
        _checkpoints.Clear();
    }

    internal void RecordCheckpoint(EventObject source, bool oldest) {
        if (string.IsNullOrWhiteSpace(source.BookmarkXml)) {
            return;
        }
        string queryTarget = string.IsNullOrWhiteSpace(source.CollectorComputer)
            ? Environment.MachineName
            : source.CollectorComputer;
        string container = string.IsNullOrWhiteSpace(source.ContainerLogName)
            ? source.GatheredLogName
            : source.ContainerLogName;
        var checkpoint = new GroupPolicyAuditCheckpoint {
            QueryTarget = queryTarget,
            ContainerLogName = container,
            BookmarkXml = source.BookmarkXml!,
            RecordId = source.RecordId,
            TimeCreatedUtc = source.TimeCreated.Kind == DateTimeKind.Utc
                ? source.TimeCreated
                : source.TimeCreated.ToUniversalTime(),
            Oldest = oldest
        };
        _checkpoints[checkpoint.SourceKey] = checkpoint;
    }

    internal void RecordCheckpoints(IEnumerable<GroupPolicyAuditCheckpoint> checkpoints) {
        foreach (GroupPolicyAuditCheckpoint checkpoint in checkpoints) {
            _checkpoints[checkpoint.SourceKey] = checkpoint.Copy();
        }
    }
}
