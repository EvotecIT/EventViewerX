using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

[Collection("NativeOperationLifetime")]
public sealed class TestWindowsEventArchiveCancellation {
    [Fact]
    public void ArchiveResourcesPreservesSourceOpenAccessFailure() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            bool archiveCalled = false;

            Assert.Throws<UnauthorizedAccessException>(() =>
                WindowsEventArchive.ArchiveFileResources(
                    root,
                    0,
                    CancellationToken.None,
                    (_, _) => archiveCalled = true));

            Assert.False(archiveCalled);
        } finally {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void CancellationBeforeNativeWorkerAdmissionRemovesTheTemporaryCopy() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(
            root,
            "source.evtx");
        byte[] original = { 1, 2, 3, 4 };
        File.WriteAllBytes(
            path,
            original);
        using var cancellation = new CancellationTokenSource();
        bool archiveCalled = false;
        try {
            Assert.ThrowsAny<OperationCanceledException>(() =>
                WindowsEventArchive.ArchiveFileResources(
                    path,
                    0,
                    cancellation.Token,
                    (_, _) => archiveCalled = true,
                    (sourcePath, temporaryPath, _) => {
                        File.Copy(
                            sourcePath,
                            temporaryPath);
                        cancellation.Cancel();
                    }));

            Assert.False(archiveCalled);
            Assert.Equal(
                original,
                File.ReadAllBytes(path));
            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*.archive.evtx"));
        } finally {
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task CancellationLeavesOriginalUntouchedUntilNativeWorkerFinishes() {
        string root = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(
            root,
            "source.evtx");
        byte[] original = { 1, 2, 3, 4 };
        File.WriteAllBytes(
            path,
            original);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        try {
            Task operation = Task.Run(() =>
                WindowsEventArchive.ArchiveFileResources(
                    path,
                    0,
                    cancellation.Token,
                    (temporaryPath, _) => {
                        entered.Set();
                        release.Wait();
                        File.WriteAllBytes(
                            temporaryPath,
                            new byte[] { 9, 8, 7 });
                    }));
            Assert.True(
                entered.Wait(TimeSpan.FromSeconds(5)));

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => operation);
            Assert.Equal(
                original,
                File.ReadAllBytes(path));

            release.Set();
            Assert.True(
                SpinWait.SpinUntil(
                    () => Directory
                        .EnumerateFiles(
                            root,
                            "*.archive.evtx")
                        .Count() == 0,
                    TimeSpan.FromSeconds(5)));
            Assert.Equal(
                original,
                File.ReadAllBytes(path));
        } finally {
            release.Set();
            if (Directory.Exists(root)) {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
