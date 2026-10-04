using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

[Collection("NativeOperationLifetime")]
public sealed class TestWindowsEventArchiveResources {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementExportRetiresOnlyResourcesFromTheOldLogGeneration(bool includeResources) {
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        string metadata = Path.Combine(root, "LocaleMetaData");
        Directory.CreateDirectory(metadata);
        string path = Path.Combine(root, "Export.evtx");
        File.WriteAllText(path, "old log");
        foreach (string name in new[] { "Export_1033.MTA", "Export.evtx_1041.MTA", "Neighbor_1033.MTA", "Export_extra_1033.MTA" }) {
            File.WriteAllText(Path.Combine(metadata, name), "old " + name);
        }
        var bundle = new WindowsEventArchiveBundle(path);
        try {
            File.WriteAllText(bundle.EventLogPath, "new log");
            if (includeResources) {
                string stagedMetadata = Path.Combine(bundle.DirectoryPath, "LocaleMetaData");
                Directory.CreateDirectory(stagedMetadata);
                File.WriteAllText(Path.Combine(stagedMetadata, "Export_1033.MTA"), "new English messages");
            }
            bundle.Publish(includeEventLog: true, overwrite: true, CancellationToken.None);
            Assert.Equal("new log", File.ReadAllText(path));
            Assert.False(File.Exists(Path.Combine(metadata, "Export.evtx_1041.MTA")));
            Assert.Equal(includeResources, File.Exists(Path.Combine(metadata, "Export_1033.MTA")));
            if (includeResources) {
                Assert.Equal("new English messages", File.ReadAllText(Path.Combine(metadata, "Export_1033.MTA")));
            }
            Assert.Equal("old Neighbor_1033.MTA", File.ReadAllText(Path.Combine(metadata, "Neighbor_1033.MTA")));
            Assert.Equal("old Export_extra_1033.MTA", File.ReadAllText(Path.Combine(metadata, "Export_extra_1033.MTA")));
        } finally {
            bundle.Cleanup();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedLogReplacementRestoresRetiredLocaleCompanions() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        string metadata = Path.Combine(root, "LocaleMetaData");
        Directory.CreateDirectory(metadata);
        string path = Path.Combine(root, "Export.evtx");
        string oldResource = Path.Combine(metadata, "Export_1041.MTA");
        File.WriteAllText(path, "old log");
        File.WriteAllText(oldResource, "old Japanese messages");
        var bundle = new WindowsEventArchiveBundle(path);
        try {
            File.WriteAllText(bundle.EventLogPath, "new log without resources");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Assert.Throws<IOException>(() => bundle.Publish(includeEventLog: true, overwrite: true, CancellationToken.None));
            }
            Assert.Equal("old log", File.ReadAllText(path));
            Assert.Equal("old Japanese messages", File.ReadAllText(oldResource));
        } finally {
            bundle.Cleanup();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResourceOnlyPublicationKeepsOtherLocalesForTheUnchangedLog() {
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        string metadata = Path.Combine(root, "LocaleMetaData");
        Directory.CreateDirectory(metadata);
        string path = Path.Combine(root, "Export.evtx");
        File.WriteAllText(path, "unchanged log");
        File.WriteAllText(Path.Combine(metadata, "Export_1041.MTA"), "Japanese messages");
        var bundle = new WindowsEventArchiveBundle(path);
        try {
            string stagedMetadata = Path.Combine(bundle.DirectoryPath, "LocaleMetaData");
            Directory.CreateDirectory(stagedMetadata);
            File.WriteAllText(Path.Combine(stagedMetadata, "Export_1033.MTA"), "English messages");
            bundle.Publish(includeEventLog: false, overwrite: true, CancellationToken.None);
            Assert.Equal("unchanged log", File.ReadAllText(path));
            Assert.Equal("Japanese messages", File.ReadAllText(Path.Combine(metadata, "Export_1041.MTA")));
            Assert.Equal("English messages", File.ReadAllText(Path.Combine(metadata, "Export_1033.MTA")));
        } finally {
            bundle.Cleanup();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CompetingPublicationWaitsForOwnershipOfTheCompleteFileSet() {
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "Export.evtx");
        var first = new WindowsEventArchiveBundle(path);
        var second = new WindowsEventArchiveBundle(path);
        foreach (var pair in new[] { (Bundle: first, Generation: "first"), (Bundle: second, Generation: "second") }) {
            File.WriteAllText(pair.Bundle.EventLogPath, pair.Generation + " log");
            string metadata = Path.Combine(pair.Bundle.DirectoryPath, "LocaleMetaData");
            Directory.CreateDirectory(metadata);
            File.WriteAllText(Path.Combine(metadata, "Export_1033.MTA"), pair.Generation + " messages");
        }
        using var entered = new ManualResetEventSlim();
        Task? competing = null;
        try {
            using (FilePublication.AcquireOwnership(path)) {
                competing = Task.Run(() => {
                    entered.Set();
                    second.Publish(includeEventLog: true, overwrite: true, CancellationToken.None);
                });
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
                Assert.False(competing.Wait(TimeSpan.FromMilliseconds(200)));
                first.Publish(includeEventLog: true, overwrite: true, CancellationToken.None);
                Assert.Equal("first log", File.ReadAllText(path));
                Assert.Equal("first messages", File.ReadAllText(Path.Combine(root, "LocaleMetaData", "Export_1033.MTA")));
            }
            await competing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("second log", File.ReadAllText(path));
            Assert.Equal("second messages", File.ReadAllText(Path.Combine(root, "LocaleMetaData", "Export_1033.MTA")));
        } finally {
            if (competing != null) {
                await competing.WaitAsync(TimeSpan.FromSeconds(5));
            }
            first.Cleanup();
            second.Cleanup();
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchivePublishesResourcesUnderTheOriginalLogName(bool extended) {
        if (extended && !OperatingSystem.IsWindows()) {
            return;
        }
        string ordinaryRoot = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        string root = extended ? @"\\?\" + ordinaryRoot : ordinaryRoot;
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "Security Export.evtx");
        byte[] original = { 1, 2, 3, 4 };
        File.WriteAllBytes(path, original);
        try {
            WindowsEventArchive.ArchiveFileResources(path, 1033, CancellationToken.None, (nativePath, _) => {
                string metadata = Path.Combine(Path.GetDirectoryName(nativePath)!, "LocaleMetaData");
                Directory.CreateDirectory(metadata);
                File.WriteAllText(Path.Combine(metadata, Path.GetFileNameWithoutExtension(nativePath) + "_1033.MTA"), "localized messages");
            });

            Assert.Equal(original, File.ReadAllBytes(path));
            string metadataRoot = Path.Combine(root, "LocaleMetaData");
            Assert.Equal("localized messages", File.ReadAllText(Path.Combine(metadataRoot, "Security Export_1033.MTA")));
            Assert.Single(Directory.EnumerateFiles(metadataRoot));
            Assert.Equal(new[] { "LocaleMetaData" }, Directory.EnumerateDirectories(root).Select(Path.GetFileName));
            Assert.Equal(new[] { "Security Export.evtx" }, Directory.EnumerateFiles(root).Select(Path.GetFileName));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PublicationFailureRestoresEarlierResourcesAndPreservesTheLog() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "LocaleMetaData"));
        string path = Path.Combine(root, "Security.evtx");
        string first = Path.Combine(root, "LocaleMetaData", "Security_1033.MTA");
        string second = Path.Combine(root, "LocaleMetaData", "Security_1041.MTA");
        File.WriteAllText(path, "original log");
        File.WriteAllText(first, "original English messages");
        File.WriteAllText(second, "original Japanese messages");
        var bundle = new WindowsEventArchiveBundle(path);
        try {
            File.WriteAllText(bundle.EventLogPath, "replacement log");
            string resources = Path.Combine(bundle.DirectoryPath, "LocaleMetaData");
            Directory.CreateDirectory(resources);
            File.WriteAllText(Path.Combine(resources, Path.GetFileName(first)), "new English messages");
            File.WriteAllText(Path.Combine(resources, Path.GetFileName(second)), "new Japanese messages");
            using (var locked = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Assert.Throws<IOException>(() => bundle.Publish(includeEventLog: true, overwrite: true, CancellationToken.None));
            }
            Assert.Equal("original log", File.ReadAllText(path));
            Assert.Equal("original English messages", File.ReadAllText(first));
            Assert.Equal("original Japanese messages", File.ReadAllText(second));
        } finally {
            bundle.Cleanup();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NativeExportPublishesMatchingResourcesAndPreservesOtherArchives() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "LocaleMetaData"));
        string destination = Path.Combine(root, "Export.evtx");
        string neighbor = Path.Combine(root, "LocaleMetaData", "Neighbor_1033.MTA");
        File.WriteAllText(neighbor, "neighbor messages");
        try {
            string fixture = Path.Combine(Directory.GetCurrentDirectory(), "Tests", "Logs", "NamedFilterExamples.evtx");
            if (!File.Exists(fixture)) {
                fixture = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx");
            }
            EventExportResult result = EventLogExporter.ExportEvtxCore(destination, overwrite: false,
                computeSha256: true, CancellationToken.None, nativePath => {
                    File.Copy(fixture, nativePath);
                    string metadata = Path.Combine(Path.GetDirectoryName(nativePath)!, "LocaleMetaData");
                    Directory.CreateDirectory(metadata);
                    File.WriteAllText(Path.Combine(metadata, Path.GetFileNameWithoutExtension(nativePath) + "_1033.MTA"), "export messages");
                });
            Assert.True(result.EventCount > 0);
            Assert.NotNull(result.Sha256);
            Assert.Equal("export messages", File.ReadAllText(Path.Combine(root, "LocaleMetaData", "Export_1033.MTA")));
            Assert.Equal("neighbor messages", File.ReadAllText(neighbor));
            Assert.Equal(new[] { "LocaleMetaData" }, Directory.EnumerateDirectories(root).Select(Path.GetFileName));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledArchiveRemovesLateResourcesWithoutChangingTheOriginal(bool failNativeWorker) {
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "Security.evtx");
        File.WriteAllText(path, "original log");
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        string stage = string.Empty;
        try {
            Task operation = Task.Run(() => WindowsEventArchive.ArchiveFileResources(path, 1033, cancellation.Token, (nativePath, _) => {
                stage = Path.GetDirectoryName(nativePath)!;
                entered.Set();
                release.Wait();
                string metadata = Path.Combine(stage, "LocaleMetaData");
                Directory.CreateDirectory(metadata);
                File.WriteAllText(Path.Combine(metadata, "Security_1033.MTA"), "late messages");
                if (failNativeWorker) {
                    throw new IOException("Late native failure");
                }
            }));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal("original log", File.ReadAllText(path));
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => !Directory.Exists(stage), TimeSpan.FromSeconds(5)));
            Assert.False(Directory.Exists(Path.Combine(root, "LocaleMetaData")));
            Assert.Equal("original log", File.ReadAllText(path));
        } finally {
            release.Set();
            Directory.Delete(root, recursive: true);
        }
    }
}
