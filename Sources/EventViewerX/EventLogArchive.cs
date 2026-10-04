using EventViewerX.Native;
using System.Globalization;

namespace EventViewerX;

/// <summary>Native metadata operations for offline Windows Event Log archives.</summary>
public static class EventLogArchive {
    /// <summary>Reads log-level metadata without enumerating event records.</summary>
    public static EventLogFileInformation GetInformation(string path) {
        return WindowsEventArchive.GetFileInformation(path);
    }

    /// <summary>
    /// Archives provider metadata and localized messages beside an EVTX file
    /// previously created by the Windows Event Log export API. Keep its LocaleMetaData
    /// directory alongside the log when copying the archive to another computer.
    /// </summary>
    public static void ArchiveResources(
        string path,
        CultureInfo? culture = null,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException(
                "Event log path cannot be null or empty.",
                nameof(path));
        }
        WindowsEventArchive.ArchiveFileResources(
            path,
            culture?.LCID ?? 0,
            cancellationToken);
    }
}
