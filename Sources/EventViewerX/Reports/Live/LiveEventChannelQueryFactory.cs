namespace EventViewerX.Reports.Live;

internal static class LiveEventChannelQueryFactory {
    internal static EventLogChannelQuery Create(
        string logName,
        string? machineName,
        string xpath,
        long maxEvents,
        bool oldest,
        EventReadMode readMode,
        int? timeoutMilliseconds) {

        int timeout =
            timeoutMilliseconds ?? 5000;
        return new EventLogChannelQuery(
            logName) {
            XPath = xpath,
            MachineName = machineName,
            MaxEvents = maxEvents,
            Oldest = oldest,
            RemoteConnectionTimeoutMilliseconds =
                timeout,
            RemoteReadTimeoutMilliseconds =
                timeout,
            ReadMode = readMode
        };
    }
}
