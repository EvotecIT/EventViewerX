using System.Diagnostics;
using Xunit;

namespace EventViewerX.Tests;

public class TestLimitLog {
    [Fact]
    public void LimitExistingLog() {
        if (!OperatingSystem.IsWindows()) return;
        if (!TestEnv.IsAdmin()) return;
        using IDisposable isolation =
            TestClassicEventLogIsolation.Acquire();
        string logName = "EVX" + Guid.NewGuid().ToString("N") + "LimitLog";
        if (ClassicEventLogManager.LogExists(logName)) {
            ClassicEventLogManager.RemoveLog(logName);
        }
        try {
            ClassicEventLogManager.EnsureLog(
                new ClassicEventLogConfiguration {
                    LogName = logName,
                    SourceName = logName,
                    MaximumKilobytes = 1024
                });
            ClassicEventLogEnsureResult limited =
                ClassicEventLogManager.EnsureLog(
                    new ClassicEventLogConfiguration {
                        LogName = logName,
                        SourceName = logName,
                        MaximumKilobytes = 2048,
                        OverflowAction =
                            OverflowAction.OverwriteOlder,
                        RetentionDays = 2
                    });
            Assert.True(limited.UpdatedConfiguration);
            using EventLog log = new(logName);
            Assert.Equal(2048, log.MaximumKilobytes);
            Assert.Equal(OverflowAction.OverwriteOlder, log.OverflowAction);
            Assert.Equal(2, log.MinimumRetentionDays);
        }
        finally {
            if (ClassicEventLogManager.LogExists(logName)) {
                ClassicEventLogManager.RemoveLog(logName);
            }
        }
    }

    [Fact]
    public void LimitLogOverwriteAsNeeded() {
        if (!OperatingSystem.IsWindows()) return;
        if (!TestEnv.IsAdmin()) return;
        using IDisposable isolation =
            TestClassicEventLogIsolation.Acquire();
        string logName = "EVX" + Guid.NewGuid().ToString("N") + "LimitLog";
        if (ClassicEventLogManager.LogExists(logName)) {
            ClassicEventLogManager.RemoveLog(logName);
        }
        try {
            ClassicEventLogManager.EnsureLog(
                new ClassicEventLogConfiguration {
                    LogName = logName,
                    SourceName = logName,
                    MaximumKilobytes = 1024
                });
            OverflowAction originalOverflowAction;
            int originalRetentionDays;
            using (EventLog initial = new(logName)) {
                originalOverflowAction =
                    initial.OverflowAction;
                originalRetentionDays =
                    initial.MinimumRetentionDays;
            }
            ClassicEventLogEnsureResult limited =
                ClassicEventLogManager.EnsureLog(
                    new ClassicEventLogConfiguration {
                        LogName = logName,
                        SourceName = logName,
                        MaximumKilobytes = 4096
                    });
            Assert.True(limited.UpdatedConfiguration);
            using EventLog log = new(logName);
            Assert.Equal(4096, log.MaximumKilobytes);
            Assert.Equal(
                originalOverflowAction,
                log.OverflowAction);
            Assert.Equal(
                originalRetentionDays,
                log.MinimumRetentionDays);
        }
        finally {
            if (ClassicEventLogManager.LogExists(logName)) {
                ClassicEventLogManager.RemoveLog(logName);
            }
        }
    }
}
