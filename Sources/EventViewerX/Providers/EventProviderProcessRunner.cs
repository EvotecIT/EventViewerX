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
        Action<Process>? processStarted = null,
        CancellationToken cancellationToken = default) {

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
        BoundedProcessResult processResult = BoundedProcessRunner.RunResult(
            startInfo,
            timeout,
            cancellationToken,
            processStarted);
        return new EventProviderProcessResult {
            ExitCode = processResult.ExitCode,
            Output = processResult.Output,
            Error = processResult.Error
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
