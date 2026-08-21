using System.ComponentModel;
using System.Diagnostics;

namespace EventViewerX;

internal static class BoundedProcessRunner {
    internal static string Run(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        if (startInfo == null) {
            throw new ArgumentNullException(nameof(startInfo));
        }
        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Process timeout must be greater than zero.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                $"Failed to start '{startInfo.FileName}'.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenRegistration registration =
            cancellationToken.Register(
                static state => TryKill((Process)state!),
                process);
        Stopwatch elapsed = Stopwatch.StartNew();
        try {
            while (!process.WaitForExit(100)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (elapsed.Elapsed < timeout) {
                    continue;
                }
                TryKill(process);
                TryWaitForExit(process);
                throw new TimeoutException(
                    $"Process '{startInfo.FileName}' did not exit within {timeout.TotalSeconds:0.###} seconds.");
            }
            process.WaitForExit();
            cancellationToken.ThrowIfCancellationRequested();
            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0) {
                throw new Win32Exception(
                    process.ExitCode,
                    $"Process '{startInfo.FileName}' failed with exit code {process.ExitCode}: " +
                    (string.IsNullOrWhiteSpace(error) ? output : error).Trim());
            }
            return output;
        } catch {
            TryKill(process);
            TryWaitForExit(process);
            throw;
        }
    }

    private static void TryKill(Process process) {
        try {
            if (!process.HasExited) {
                process.Kill();
            }
        } catch (InvalidOperationException) {
        } catch (SystemException) {
        }
    }

    private static void TryWaitForExit(Process process) {
        try {
            process.WaitForExit(5000);
        } catch (InvalidOperationException) {
        } catch (SystemException) {
        }
    }
}