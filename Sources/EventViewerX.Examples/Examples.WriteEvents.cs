using System.Diagnostics;

namespace EventViewerX.Examples;

internal partial class Examples {
    public static void WriteBasic() {
        ClassicEventLogManager.Write(
            new ClassicEventWriteRequest {
                SourceName = "MySource",
                LogName = "Application",
                MachineName = "AD1",
                Message = "This is a test message",
                EntryType = EventLogEntryType.Information,
                EventId = 101,
                ReplacementStrings = new[] {
                    "Replacement string 1",
                    "Replacement string 2"
                }
            });
    }

    public static void RegisterAndWriteToCustomLog() {
        ClassicEventLogManager.EnsureLog(
            new ClassicEventLogConfiguration {
                LogName = "MyCustomLog",
                SourceName = "MyApplication"
            });
        ClassicEventLogManager.Write(
            new ClassicEventWriteRequest {
                SourceName = "MyApplication",
                LogName = "MyCustomLog",
                Message = "This is a test message",
                EntryType = EventLogEntryType.Information,
                EventId = 101
            });
    }

    public static void WriteWithRawData() {
        ClassicEventLogManager.Write(
            new ClassicEventWriteRequest {
                SourceName = "MyApplication",
                LogName = "MyCustomLog",
                Message = "Authentication started.",
                EntryType = EventLogEntryType.Information,
                Category = 2,
                EventId = 1001,
                RawData = new byte[] { 1, 2, 3, 4 }
            });
    }
}
