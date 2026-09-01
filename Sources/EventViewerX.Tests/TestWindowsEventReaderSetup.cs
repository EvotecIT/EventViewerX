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

    [Fact]
    public void ExtendedFileLeaseStagesArchivedLocaleMetadataAndCleansIt() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string ordinaryRoot = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-Lease-" + Guid.NewGuid().ToString("N"));
        string extendedRoot = @"\\?\" + ordinaryRoot;
        string sourcePath = Path.Combine(extendedRoot, "archive.evtx");
        string sourceResources = Path.Combine(
            extendedRoot,
            "LocaleMetaData",
            "en-US");
        string nativeDirectory = string.Empty;
        try {
            Directory.CreateDirectory(sourceResources);
            File.WriteAllText(sourcePath, "event data");
            File.WriteAllText(
                Path.Combine(sourceResources, "provider.dll"),
                "message resources");
            var query = new NativeEventQuery(
                IntPtr.Zero,
                sourcePath,
                "*",
                WindowsEventNativeMethods.QueryFlags.FilePath,
                sourcePath,
                sourcePath);

            using (WindowsEventFileQueryLease lease =
                   WindowsEventFileQueryLease
                       .AcquireWithMessageResources(query)) {
                string nativePath = Assert.IsType<string>(lease.Query.Path);
                nativeDirectory = Path.GetDirectoryName(nativePath)!;
                Assert.NotEqual(sourcePath, nativePath);
                Assert.Equal(
                    "message resources",
                    File.ReadAllText(Path.Combine(
                        nativeDirectory,
                        "LocaleMetaData",
                        "en-US",
                        "provider.dll")));
            }

            Assert.False(Directory.Exists(nativeDirectory));
        } finally {
            if (Directory.Exists(extendedRoot)) {
                Directory.Delete(extendedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MetadataFileLeaseDoesNotStageArchivedLocaleMetadata() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string ordinaryRoot = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-MetadataLease-" + Guid.NewGuid().ToString("N"));
        string extendedRoot = @"\\?\" + ordinaryRoot;
        string sourcePath = Path.Combine(extendedRoot, "archive.evtx");
        string sourceResources = Path.Combine(
            extendedRoot,
            "LocaleMetaData",
            "en-US");
        try {
            Directory.CreateDirectory(sourceResources);
            File.WriteAllText(sourcePath, "event data");
            File.WriteAllText(
                Path.Combine(sourceResources, "provider.dll"),
                "message resources");
            var query = new NativeEventQuery(
                IntPtr.Zero,
                sourcePath,
                "*",
                WindowsEventNativeMethods.QueryFlags.FilePath,
                sourcePath,
                sourcePath);

            using WindowsEventFileQueryLease lease =
                WindowsEventFileQueryLease.Acquire(query);
            string nativePath = Assert.IsType<string>(lease.Query.Path);
            Assert.False(Directory.Exists(Path.Combine(
                Path.GetDirectoryName(nativePath)!,
                "LocaleMetaData")));
        } finally {
            if (Directory.Exists(extendedRoot)) {
                Directory.Delete(extendedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void CancelledExtendedFileLeaseStopsBeforeStaging() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string ordinaryRoot = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-CancelledLease-" + Guid.NewGuid().ToString("N"));
        string extendedRoot = @"\\?\" + ordinaryRoot;
        string sourcePath = Path.Combine(extendedRoot, "archive.evtx");
        try {
            Directory.CreateDirectory(extendedRoot);
            File.WriteAllText(sourcePath, "event data");
            var query = new NativeEventQuery(
                IntPtr.Zero,
                sourcePath,
                "*",
                WindowsEventNativeMethods.QueryFlags.FilePath,
                sourcePath,
                sourcePath);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                WindowsEventFileQueryLease.Acquire(
                    query,
                    cancellation.Token));
        } finally {
            if (Directory.Exists(extendedRoot)) {
                Directory.Delete(extendedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtendedFileLeaseCleansReadOnlyLinksWithoutChangingSourceAttributes() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        string ordinaryRoot = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX-ReadOnlyLease-" + Guid.NewGuid().ToString("N"));
        string extendedRoot = @"\\?\" + ordinaryRoot;
        string sourcePath = Path.Combine(extendedRoot, "archive.evtx");
        string nativeDirectory = string.Empty;
        try {
            Directory.CreateDirectory(extendedRoot);
            File.WriteAllText(sourcePath, "event data");
            File.SetAttributes(sourcePath, FileAttributes.ReadOnly);
            var query = new NativeEventQuery(
                IntPtr.Zero,
                sourcePath,
                "*",
                WindowsEventNativeMethods.QueryFlags.FilePath,
                sourcePath,
                sourcePath);

            using (WindowsEventFileQueryLease lease =
                   WindowsEventFileQueryLease.Acquire(query)) {
                nativeDirectory = Path.GetDirectoryName(lease.Query.Path!)!;
            }

            Assert.False(Directory.Exists(nativeDirectory));
            Assert.True(
                (File.GetAttributes(sourcePath) & FileAttributes.ReadOnly) != 0);
        } finally {
            if (File.Exists(sourcePath)) {
                File.SetAttributes(sourcePath, FileAttributes.Normal);
            }
            if (Directory.Exists(extendedRoot)) {
                Directory.Delete(extendedRoot, recursive: true);
            }
        }
    }
}
