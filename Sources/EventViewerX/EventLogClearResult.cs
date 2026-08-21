namespace EventViewerX;

/// <summary>Result of a completed native channel clear operation.</summary>
public sealed class EventLogClearResult {
    internal EventLogClearResult(
        string logName,
        string? machineName,
        string? backupPath) {

        LogName = logName;
        MachineName = machineName;
        BackupPath = backupPath;
    }

    /// <summary>Cleared channel.</summary>
    public string LogName { get; }
    /// <summary>Remote computer, or null for local.</summary>
    public string? MachineName { get; }
    /// <summary>Backup EVTX path supplied to Windows, when requested.</summary>
    public string? BackupPath { get; }
}
