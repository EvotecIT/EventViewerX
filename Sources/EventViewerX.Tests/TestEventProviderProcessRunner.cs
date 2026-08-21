using System.Diagnostics;
using EventViewerX.Providers;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventProviderProcessRunner {
    [Fact]
    public void TimedOutProviderToolIsTerminatedBeforeRunReturns() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        string directory = Path.Combine(
            Path.GetTempPath(),
            "EventViewerX.ProcessRunner." +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        int processId = 0;
        try {
            Assert.Throws<TimeoutException>(() =>
                EventProviderProcessRunner.Run(
                    executable,
                    new[] {
                        "-NoLogo",
                        "-NoProfile",
                        "-Command",
                        "Start-Sleep -Seconds 30"
                    },
                    directory,
                    TimeSpan.FromSeconds(2),
                    process =>
                        processId = process.Id));

            Assert.NotEqual(0, processId);
            Assert.Throws<ArgumentException>(() =>
                Process.GetProcessById(processId));
        } finally {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
