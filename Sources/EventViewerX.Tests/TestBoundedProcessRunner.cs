using System.Diagnostics;
using EventViewerX;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestBoundedProcessRunner {
    [Fact]
    public void ReturnsRedirectedOutputForSuccessfulProcess() {
        ProcessStartInfo startInfo = CreateCommand("echo EventViewerX");

        string output = BoundedProcessRunner.Run(
            startInfo,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Contains("EventViewerX", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminatesProcessAfterTimeout() {
        ProcessStartInfo startInfo = CreateCommand(
            "ping -n 6 127.0.0.1 >nul");
        Stopwatch elapsed = Stopwatch.StartNew();

        Assert.Throws<TimeoutException>(() =>
            BoundedProcessRunner.Run(
                startInfo,
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None));

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TerminatesProcessWhenCancelled() {
        ProcessStartInfo startInfo = CreateCommand(
            "ping -n 6 127.0.0.1 >nul");
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(150));

        Assert.ThrowsAny<OperationCanceledException>(() =>
            BoundedProcessRunner.Run(
                startInfo,
                TimeSpan.FromSeconds(5),
                cancellation.Token));
    }

    private static ProcessStartInfo CreateCommand(string command) {
        return new ProcessStartInfo {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ??
                       Path.Combine(
                           Environment.GetFolderPath(Environment.SpecialFolder.System),
                           "cmd.exe"),
            Arguments = "/d /c \"" + command + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }
}