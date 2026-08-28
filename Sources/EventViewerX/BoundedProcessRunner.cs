using System.ComponentModel;
using System.Diagnostics;

namespace EventViewerX;

internal sealed class BoundedProcessResult {
    internal int ExitCode { get; set; }
    internal string Output { get; set; } = string.Empty;
    internal string Error { get; set; } = string.Empty;
}

internal static class BoundedProcessRunner {
    internal static string Run(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        BoundedProcessResult result = RunResult(
            startInfo,
            timeout,
            cancellationToken);
        if (result.ExitCode != 0) {
            throw new Win32Exception(
                result.ExitCode,
                $"Process '{startInfo.FileName}' failed with exit code {result.ExitCode}: " +
                (string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error).Trim());
        }
        return result.Output;
    }

    internal static BoundedProcessResult RunResult(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<Process>? processStarted = null) {

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
            processStarted?.Invoke(process);
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
            cancellationToken.ThrowIfCancellationRequested();
            (string output, string error) = ReadStreams(
                outputTask,
                errorTask,
                startInfo.FileName);
            return new BoundedProcessResult {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };
        } catch {
            TryKill(process);
            TryWaitForExit(process);
            TryDrainStreams(outputTask, errorTask);
            throw;
        }
    }

    private static void TryKill(Process process) {
        try {
            if (!process.HasExited) {
                var killTree = typeof(Process).GetMethod(
                    nameof(Process.Kill),
                    new[] { typeof(bool) });
                if (killTree != null) {
                    killTree.Invoke(process, new object[] { true });
                } else {
                    process.Kill();
                }
            }
        } catch (InvalidOperationException) {
        } catch (SystemException) {
        } catch (System.Reflection.TargetInvocationException) {
        }
    }

    private static void TryWaitForExit(Process process) {
        try {
            process.WaitForExit(5000);
        } catch (InvalidOperationException) {
        } catch (SystemException) {
        }
    }

    private static (string Output, string Error) ReadStreams(
        Task<string> outputTask,
        Task<string> errorTask,
        string fileName) {

        Task streams = Task.WhenAll(outputTask, errorTask);
        if (!streams.Wait(TimeSpan.FromSeconds(5))) {
            throw new TimeoutException(
                $"Process '{fileName}' exited but redirected output did not close within 5 seconds.");
        }
        return (
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    private static void TryDrainStreams(
        Task<string> outputTask,
        Task<string> errorTask) {

        try {
            Task.WhenAll(outputTask, errorTask).Wait(TimeSpan.FromSeconds(5));
        } catch (AggregateException) {
        }
    }
}
