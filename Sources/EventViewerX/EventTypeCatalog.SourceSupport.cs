namespace EventViewerX;

public static partial class EventTypeCatalog {
    private static readonly IReadOnlyDictionary<EventType, string> UnavailableSources =
        new Dictionary<EventType, string> {
            [EventType.DhcpLeaseCreated] = "DHCP lease code 10 belongs to the DHCP text audit log, not the registered Windows event channel. A DHCP audit-file source is required.",
            [EventType.SqlDatabaseCreated] = "SQL Server event 17137 describes database startup and 1802 describes CREATE DATABASE failure. Neither establishes successful database creation; a verified SQL Audit source is required.",
            [EventType.NetworkPromiscuousMode] = "NDIS event 10400 describes an interface reset and 10401 a timestamping notification. Neither establishes promiscuous mode; a verified adapter-mode telemetry source is required.",
            [EventType.HyperVCheckpointCreated] = "Event 4096 in the previously registered VMMS channel has no verified checkpoint-creation contract. Checkpoint restore event 18596 is not a creation substitute.",
            [EventType.AADConnectRunProfile] = "Directory Synchronization event 906 is general error logging, not a verified run-profile completion event. Use verified synchronization events or a generic Application-log query."
        };

    internal static string? GetUnavailableSourceReason(EventType type) {
        string[] reasons = Expand(new[] { type })
            .Where(UnavailableSources.ContainsKey)
            .Select(leaf => $"{leaf}: {UnavailableSources[leaf]}")
            .ToArray();
        return reasons.Length == 0 ? null : string.Join(" ", reasons);
    }

    private static void EnsureSourcesAvailable(IReadOnlyList<EventType> expanded) {
        string[] reasons = expanded.Where(UnavailableSources.ContainsKey)
            .Select(type => $"{type}: {UnavailableSources[type]}")
            .ToArray();
        if (reasons.Length > 0) {
            throw new NotSupportedException(string.Join(" ", reasons) +
                " Select supported leaf definitions or inspect the original telemetry with a generic query.");
        }
    }
}