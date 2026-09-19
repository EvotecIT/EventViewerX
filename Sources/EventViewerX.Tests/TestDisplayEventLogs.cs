using System;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestDisplayEventLogs {
    [Fact]
    public void UnknownSizeRemainsUnknownWhileMeasuredZeroRemainsZero() {
        Assert.Null(EventLogDetails.ConvertSize(null, "B", "MB", 2));
        Assert.Equal(0, EventLogDetails.ConvertSize(0, "B", "MB", 2));
    }

    [Fact]
    public void CreateEventLogDetailsWithNullInfo() {
        if (!OperatingSystem.IsWindows()) return;
        using var session = new EventLogSession();
        using var config = new EventLogConfiguration("Application", session);
        var logger = new InternalLogger();
        var details = new EventLogDetails(logger, Environment.MachineName, config, null);
        Assert.Equal("Application", details.LogName);
        Assert.Null(details.FileSize);
        Assert.Null(details.FileSizeCurrentMB);
        Assert.Equal(config.MaximumSizeInBytes, details.FileSizeMaximum);
        Assert.NotNull(details.FileSizeMaximumMB);
        Assert.False(details.HasDiagnostics);
        Assert.Empty(details.Diagnostics);
    }

    [Fact]
    public void CreateEventLogDetailsWithRuntimeInfoReportsActualSize() {
        if (!OperatingSystem.IsWindows()) return;
        using var session = new EventLogSession();
        using var config = new EventLogConfiguration("Application", session);
        var info = session.GetLogInformation("Application", PathType.LogName);
        var details = new EventLogDetails(new InternalLogger(), Environment.MachineName, config, info);

        Assert.NotNull(details.FileSize);
        Assert.NotNull(details.FileSizeCurrentMB);
        Assert.Equal(Math.Round(details.FileSize!.Value / 1024.0 / 1024.0, 2), details.FileSizeCurrentMB);
    }
}
