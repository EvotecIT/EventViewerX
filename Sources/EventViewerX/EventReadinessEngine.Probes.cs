using System.Diagnostics;
using System.Net;

namespace EventViewerX;

/// <summary>Bounded direct and managed-safe collector probe composition.</summary>
public static partial class EventReadinessEngine {
    private static EventLogProbeResult ProbeSourceSafely(
        IEventReadinessEvidenceProvider evidenceProvider,
        EventSourceDefinition source,
        bool collector,
        string targetLog,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) {

        IReadOnlyList<EventFilter> partitions = EventFilterPartitioner.Partition(
            new EventFilter { EventIds = source.EventIds });
        var stopwatch = Stopwatch.StartNew();
        int totalScanned = 0;
        EventLogProbeResult? lastSuccessfulProbe = null;
        for (int index = 0; index < partitions.Count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = timeout - stopwatch.Elapsed;
            int remainingScan = maxEventsToScan - totalScanned;
            if (remaining <= TimeSpan.Zero) {
                return CreateFailedProbe(
                    targetLog,
                    machineName,
                    EventLogProbeStatus.Timeout,
                    $"The {partitions.Count}-partition readiness probe exhausted its {timeout.TotalMilliseconds:F0} ms budget after {index} partition(s).",
                    stopwatch.Elapsed);
            }
            if (remainingScan <= 0) {
                return CreateFailedProbe(
                    targetLog,
                    machineName,
                    EventLogProbeStatus.LimitReached,
                    $"The partitioned readiness probe exhausted its {maxEventsToScan} event scan budget.",
                    stopwatch.Elapsed);
            }
            string xpath = EventFilterCompiler.BuildXPath(partitions[index]);
            if (collector) {
                xpath = EventTypeEngine.AddOriginalChannelPredicate(xpath, source.LogName);
            }
            EventLogProbeResult probe = ProbeSafely(
                evidenceProvider,
                targetLog,
                xpath,
                machineName,
                remaining,
                remainingScan,
                credential,
                authentication,
                cancellationToken);
            totalScanned += probe.EventsScanned;
            if (probe.Status == EventLogProbeStatus.Ok) {
                return ReframePartitionedProbe(
                    probe,
                    totalScanned,
                    stopwatch.Elapsed,
                    partitions.Count,
                    index + 1);
            }
            if (probe.Status is not (EventLogProbeStatus.NoEvent or
                EventLogProbeStatus.NoUsableTimestamp or
                EventLogProbeStatus.LimitReached)) {
                return ReframePartitionedProbe(
                    probe,
                    totalScanned,
                    stopwatch.Elapsed,
                    partitions.Count,
                    index + 1);
            }
            lastSuccessfulProbe = probe;
        }
        EventLogProbeResult fallback = lastSuccessfulProbe ?? CreateFailedProbe(
            targetLog,
            machineName,
            EventLogProbeStatus.NoEvent,
            "No event matched the native query.",
            stopwatch.Elapsed);
        return ReframePartitionedProbe(
            fallback,
            totalScanned,
            stopwatch.Elapsed,
            partitions.Count,
            partitions.Count);
    }

    private static EventLogProbeResult ReframePartitionedProbe(
        EventLogProbeResult probe,
        int eventsScanned,
        TimeSpan duration,
        int partitionCount,
        int attemptedPartitions) => new(
            probe.LogName,
            probe.Machine,
            probe.EventTimeUtc,
            probe.Status,
            partitionCount == 1
                ? probe.Message
                : $"{probe.Message ?? probe.Status.ToString()} Partition coverage: {attemptedPartitions}/{partitionCount}.",
            eventsScanned,
            probe.RecordCount,
            duration,
            probe.NativeQueryVerified);

    private static EventLogProbeResult ProbeSafely(
        IEventReadinessEvidenceProvider evidenceProvider,
        string logName,
        string xpath,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) {

        var stopwatch = Stopwatch.StartNew();
        try {
            return evidenceProvider.Probe(
                logName,
                xpath,
                machineName,
                timeout,
                maxEventsToScan,
                credential,
                authentication,
                cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (UnauthorizedAccessException exception) {
            return CreateFailedProbe(
                logName,
                machineName,
                EventLogProbeStatus.AccessDenied,
                exception.Message,
                stopwatch.Elapsed);
        } catch (Exception exception) {
            return CreateFailedProbe(
                logName,
                machineName,
                EventLogProbeStatus.Error,
                exception.Message,
                stopwatch.Elapsed);
        }
    }

    private static EventLogProbeResult CreateFailedProbe(
        string logName,
        string? machineName,
        EventLogProbeStatus status,
        string message,
        TimeSpan duration) => new(
            logName,
            machineName ?? EventLogTarget.LocalMachineName,
            null,
            status,
            message,
            0,
            null,
            duration,
            nativeQueryVerified: false);

}
