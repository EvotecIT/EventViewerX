using Xunit;

namespace EventViewerX.Tests;

public sealed class TestFilePublication {
    [Fact]
    public void PartialNativeReplacementRestoresTheOriginalFileAndItsStreams() {
        string root = CreateRoot();
        string destination = Path.Combine(root, "state.json");
        string backup = Path.Combine(root, "state.recovery");
        try {
            File.WriteAllText(destination, "original state");
            if (OperatingSystem.IsWindows()) {
                File.WriteAllText(destination + ":evidence", "original stream");
            }
            // ReplaceFile error 1177 can leave the original under its backup name.
            File.Move(destination, backup);
            FilePublication.RestoreFailedReplacement(destination, backup, new IOException("Native replacement error 1177"));
            Assert.Equal("original state", File.ReadAllText(destination));
            Assert.False(File.Exists(backup));
            if (OperatingSystem.IsWindows()) {
                Assert.Equal("original stream", File.ReadAllText(destination + ":evidence"));
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UncertainDestinationRetainsTheNativeRecoveryFileAndReportsItsPath() {
        string root = CreateRoot();
        string destination = Path.Combine(root, "state.json");
        string backup = Path.Combine(root, "state.recovery");
        try {
            File.WriteAllText(destination, "another writer's state");
            File.WriteAllText(backup, "original state");
            var failure = Assert.Throws<FilePublicationRecoveryException>(() =>
                FilePublication.RestoreFailedReplacement(destination, backup, new IOException("Native replacement failed")));
            Assert.Contains(backup, failure.Message);
            Assert.Equal("original state", File.ReadAllText(backup));
            Assert.Equal("another writer's state", File.ReadAllText(destination));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SuccessfulOverwriteRetainsDestinationStreamsAndRemovesRecoveryCopy() {
        string root = CreateRoot();
        string destination = Path.Combine(root, "state.json");
        string temporary = Path.Combine(root, "staged.tmp");
        try {
            File.WriteAllText(destination, "old state");
            File.WriteAllText(temporary, "new state");
            if (OperatingSystem.IsWindows()) {
                File.WriteAllText(destination + ":evidence", "retained stream");
            }
            FilePublication.Promote(temporary, destination, overwrite: true);
            Assert.Equal("new state", File.ReadAllText(destination));
            Assert.Equal(new[] { "state.json" }, Directory.EnumerateFiles(root).Select(Path.GetFileName));
            if (OperatingSystem.IsWindows()) {
                Assert.Equal("retained stream", File.ReadAllText(destination + ":evidence"));
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot() {
        string root = Path.Combine(Path.GetTempPath(), "EventViewerX-Publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
