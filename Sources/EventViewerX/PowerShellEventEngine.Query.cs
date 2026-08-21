using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace EventViewerX;

public static partial class PowerShellEventEngine {
    /// <summary>
    /// Streams PowerShell operational events through the owned native engine with only structured payload data.
    /// </summary>
    private static IEnumerable<EventObject> QueryPowerShellScriptEvents(
        string logName,
        IReadOnlyCollection<string> eventIds,
        string? machineName,
        string? eventLogPath,
        DateTime? dateFrom,
        DateTime? dateTo,
        long nativeReadLimit,
        CancellationToken cancellationToken) {

        string xpath = EventFilterCompiler.BuildXPath(
            new EventFilter {
                EventIds = eventIds
                    .Select(eventId => int.Parse(
                        eventId,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture))
                    .ToArray(),
                StartTime = dateFrom,
                EndTime = dateTo
            });

        if (!string.IsNullOrWhiteSpace(eventLogPath)) {
            var fileQuery = new EventLogFileQuery(eventLogPath!) {
                XPath = xpath,
                Oldest = false,
                ReadMode = EventReadMode.StructuredData,
                MaxEvents = nativeReadLimit
            };
            foreach (EventObject eventObject in EventLogEngine.ReadFile(fileQuery, cancellationToken)) {
                yield return eventObject;
            }
            yield break;
        }

        var channelQuery = new EventLogChannelQuery(logName) {
            XPath = xpath,
            MachineName = machineName,
            Oldest = false,
            ReadMode = EventReadMode.StructuredData,
            MaxEvents = nativeReadLimit,
            RemoteConnectionTimeoutMilliseconds = Settings.SessionTimeoutMs,
            RemoteReadTimeoutMilliseconds =
                Settings.QuerySessionTimeoutMs,
            RpcEndpointPort = Settings.RpcProbePort
        };
        foreach (EventObject eventObject in EventLogEngine.ReadChannel(channelQuery, cancellationToken)) {
            yield return eventObject;
        }
    }
}
