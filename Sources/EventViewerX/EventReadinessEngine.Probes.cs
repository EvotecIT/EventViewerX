using System.Diagnostics;
using System.Net;

namespace EventViewerX;

/// <summary>Bounded direct and managed-safe collector probe composition.</summary>
public static partial class EventReadinessEngine {
    private static EventLogProbeResult ProbeSourceSafely(
        IEventReadinessEvidenceProvider evidenceProvider,
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string targetLog,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) {

        var stopwatch = Stopwatch.StartNew();
        try {
            return evidenceProvider.ProbeTypedDirectSource(
                types,
                source,
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
                targetLog,
                machineName,
                EventLogProbeStatus.AccessDenied,
                exception.Message,
                stopwatch.Elapsed);
        } catch (Exception exception) {
            return CreateFailedProbe(
                targetLog,
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
