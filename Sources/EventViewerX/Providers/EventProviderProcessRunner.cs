namespace EventViewerX.Providers;

internal sealed class EventProviderProcessResult {
    internal int ExitCode { get; set; }
    internal string Output { get; set; } = string.Empty;
    internal string Error { get; set; } = string.Empty;
}

internal static class EventProviderProcessRunner {
    internal static EventProviderProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        Action<Process>? processStarted = null) {

        string argumentText = string.Join(
            " ",
            arguments.Select(Quote));
        var startInfo = new ProcessStartInfo {
            FileName = fileName,
            Arguments = argumentText,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = new Process {
            StartInfo = startInfo
        };
        if (!process.Start()) {
            throw new InvalidOperationException(
                $"Failed to start '{fileName}'.");
        }
        Task<string> output =
            process.StandardOutput.ReadToEndAsync();
        Task<string> error =
            process.StandardError.ReadToEndAsync();
        try {
            processStarted?.Invoke(process);
        } catch {
            try {
                process.Kill();
            } catch (InvalidOperationException) {
            } finally {
                process.WaitForExit();
                Task.WaitAll(output, error);
            }
            throw;
        }
        if (!process.WaitForExit(
                checked((int)Math.Min(
                    int.MaxValue,
                    timeout.TotalMilliseconds)))) {
            try {
                process.Kill();
            } catch (InvalidOperationException) {
            } finally {
                process.WaitForExit();
                Task.WaitAll(output, error);
            }
            throw new TimeoutException(
                $"Provider build tool '{fileName}' did not finish within {timeout}.");
        }
        Task.WaitAll(output, error);
        return new EventProviderProcessResult {
            ExitCode = process.ExitCode,
            Output = output.Result,
            Error = error.Result
        };
    }

    internal static void EnsureSuccess(
        EventProviderProcessResult result,
        string toolName) {

        if (result.ExitCode == 0) {
            return;
        }
        throw new InvalidOperationException(
            $"{toolName} exited with code {result.ExitCode}." +
            Environment.NewLine +
            result.Output +
            Environment.NewLine +
            result.Error);
    }

    private static string Quote(string value) {
        if (value.Length == 0) {
            return "\"\"";
        }
        if (value.IndexOfAny(new[] {
                ' ',
                '\t',
                '"'
            }) < 0) {
            return value;
        }
        var result = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char character in value) {
            if (character == '\\') {
                backslashes++;
                continue;
            }
            if (character == '"') {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }
            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(character);
        }
        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
