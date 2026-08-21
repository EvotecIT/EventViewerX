using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestWindowsEventReaderSetup {
    [Fact]
    public void CancellationWinsBeforeTheNativeQueryIsOpened() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        using var cancellation =
            new CancellationTokenSource();
        cancellation.Cancel();
        var query = new NativeEventQuery(
            IntPtr.Zero,
            "EventViewerX-Missing-Cancelled-Query",
            "<invalid",
            WindowsEventNativeMethods.QueryFlags.ChannelPath,
            "cancelled query");

        using IEnumerator<EventObject> events =
            WindowsEventReader.Read(
                    query,
                    EventReadMode.Metadata,
                    Environment.MachineName,
                    "EventViewerX-Missing-Cancelled-Query",
                    cancellation.Token)
                .GetEnumerator();

        Assert.Throws<OperationCanceledException>(
            () => events.MoveNext());
    }

    [Fact]
    public void QueryOpenedIsReportedOnlyAfterTheNativeCursorExists() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string path = Path.GetFullPath(Path.Combine(
            "..",
            "..",
            "..",
            "..",
            "..",
            "Tests",
            "Logs",
            "NamedFilterExamples.evtx"));
        var query = new NativeEventQuery(
            IntPtr.Zero,
            path,
            "*",
            WindowsEventNativeMethods.QueryFlags.FilePath |
            WindowsEventNativeMethods.QueryFlags.ForwardDirection,
            path);
        bool queryOpened = false;

        using IEnumerator<EventObject> events =
            WindowsEventReader.Read(
                    query,
                    EventReadMode.Metadata,
                    Environment.MachineName,
                    path,
                    CancellationToken.None,
                    () => queryOpened = true)
                .GetEnumerator();

        Assert.False(queryOpened);
        Assert.True(events.MoveNext());
        Assert.True(queryOpened);
    }
}
