using System;
using System.Diagnostics.Eventing.Reader;
using Xunit;

namespace EventViewerX.Tests;

public class TestDisplayEventLogs {
    [Fact]
    public void CreateEventLogDetailsWithNullInfo() {
        if (!OperatingSystem.IsWindows()) return;
        using var session = new EventLogSession();
        using var config = new EventLogConfiguration("Application", session);
        var logger = new InternalLogger();
        var details = new EventLogDetails(logger, Environment.MachineName, config, null);
        Assert.Equal("Application", details.LogName);
        Assert.False(details.HasDiagnostics);
        Assert.Empty(details.Diagnostics);
    }
}
