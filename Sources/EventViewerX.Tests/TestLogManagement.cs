using System.Diagnostics;
using Xunit;

namespace EventViewerX.Tests;

public class TestLogManagement {
    [Fact]
    public void ClassicLogOverflowActions_TryNormalize_UsesCanonicalNames() {
        var success = ClassicLogOverflowActions.TryNormalize("Overwrite_Older", out var normalized, out var error);

        Assert.True(success);
        Assert.Equal("overwrite_older", normalized);
        Assert.Null(error);
    }

    [Fact]
    public void ClassicLogOverflowActions_TryNormalize_RejectsUnknownValues() {
        var success = ClassicLogOverflowActions.TryNormalize("archive_forever", out var normalized, out var error);

        Assert.False(success);
        Assert.Null(normalized);
        Assert.Equal("overflow_action must be one of: overwrite_as_needed, overwrite_older, do_not_overwrite.", error);
    }

    [Fact]
    public void CreateAndRemoveLog() {
        if (!OperatingSystem.IsWindows()) return;
        if (!TestEnv.IsAdmin()) return;

        using IDisposable isolation =
            TestClassicEventLogIsolation.Acquire();
        string logName = "EVX" + Guid.NewGuid().ToString("N") + "CustomLog";
        if (ClassicEventLogManager.LogExists(logName)) {
            ClassicEventLogManager.RemoveLog(logName);
        }

        try {
            ClassicEventLogEnsureResult result =
                ClassicEventLogManager.EnsureLog(
                    new ClassicEventLogConfiguration {
                        LogName = logName,
                        SourceName = logName,
                        MaximumKilobytes = 256,
                        OverflowAction =
                            OverflowAction.OverwriteAsNeeded
                    });
            Assert.True(result.CreatedLog);
            Assert.True(result.CreatedSource);
            Assert.True(result.After.LogExists);
            Assert.True(result.After.SourceExists);

            bool removed =
                ClassicEventLogManager.RemoveLog(logName);
            Assert.True(removed);
            Assert.False(
                ClassicEventLogManager.LogExists(logName));
        }
        finally {
            if (ClassicEventLogManager.LogExists(logName)) {
                ClassicEventLogManager.RemoveLog(logName);
            }
        }
    }

    [Fact]
    public void LogRemovalRetriesTransientWindowsFailures() {
        int attempts = 0;
        int waits = 0;

        ClassicEventLogManager.ExecuteLogRemovalWithRetry(
            () => {
                attempts++;
                if (attempts < 3) {
                    throw new InvalidOperationException(
                        "The Event Log service has not refreshed yet.");
                }
            },
            static () => true,
            () => waits++);

        Assert.Equal(3, attempts);
        Assert.Equal(2, waits);
    }

    [Fact]
    public void LogRemovalAcceptsCompletedTransientFailure() {
        int waits = 0;

        ClassicEventLogManager.ExecuteLogRemovalWithRetry(
            static () => throw new InvalidOperationException(
                "The delete completed before the API returned."),
            static () => false,
            () => waits++);

        Assert.Equal(0, waits);
    }

    [Fact]
    public void LogRemovalDoesNotRetryPermissionFailures() {
        int existenceChecks = 0;
        int waits = 0;

        Assert.Throws<UnauthorizedAccessException>(() =>
            ClassicEventLogManager.ExecuteLogRemovalWithRetry(
                static () => throw new UnauthorizedAccessException(),
                () => {
                    existenceChecks++;
                    return true;
                },
                () => waits++));

        Assert.Equal(0, existenceChecks);
        Assert.Equal(0, waits);
    }

    [Fact]
    public void LogRemovalStopsAfterBoundedTransientFailures() {
        int attempts = 0;
        int waits = 0;

        Assert.Throws<InvalidOperationException>(() =>
            ClassicEventLogManager.ExecuteLogRemovalWithRetry(
                () => {
                    attempts++;
                    throw new InvalidOperationException(
                        "The Event Log service did not become ready.");
                },
                static () => true,
                () => waits++));

        Assert.Equal(20, attempts);
        Assert.Equal(19, waits);
    }

    [Fact]
    public void RetentionDaysRequireOverwriteOlder() {
        Assert.Throws<ArgumentException>(() =>
            ClassicEventLogManager.EnsureLog(
                new ClassicEventLogConfiguration {
                    LogName = "Example",
                    SourceName = "Example",
                    OverflowAction =
                        OverflowAction.OverwriteAsNeeded,
                    RetentionDays = 7
                }));
    }
}
