using System.Diagnostics;
using System.Text;

namespace EventViewerX.Evtx;

/// <summary>
/// Cross-platform EVTX reader that streams JSONL from the caller-supplied Rust <c>evtx_dump</c> executable.
/// This adapter does not download, install, or update the executable.
/// </summary>
public sealed class EvtxDumpSavedEventReader : ISavedEventReader {
    private readonly string _executablePath;
    private readonly TimeSpan _maximumRuntime;
    private readonly TimeSpan _maximumInactivity;

    /// <summary>Creates a bounded reader for an explicit executable path or a command resolvable through PATH.</summary>
    /// <param name="executablePath">Exact executable path or command resolvable through PATH.</param>
    /// <param name="maximumRuntime">Maximum total parser lifetime. The default is 30 minutes.</param>
    /// <param name="maximumInactivity">Maximum time without one JSONL output line. The default is two minutes.</param>
    public EvtxDumpSavedEventReader(
        string executablePath = "evtx_dump",
        TimeSpan? maximumRuntime = null,
        TimeSpan? maximumInactivity = null) {

        if (string.IsNullOrWhiteSpace(executablePath)) {
            throw new ArgumentException("The evtx_dump executable path cannot be empty.", nameof(executablePath));
        }
        _executablePath = executablePath.Trim();
        _maximumRuntime = maximumRuntime ?? TimeSpan.FromMinutes(30);
        _maximumInactivity = maximumInactivity ?? TimeSpan.FromMinutes(2);
        if (_maximumRuntime <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(maximumRuntime));
        }
        if (_maximumInactivity <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(maximumInactivity));
        }
    }

    /// <inheritdoc />
    public IEnumerable<SavedEventRecord> Read(
        EventLogFileQuery query,
        Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EvtxContainerShapeInspector.Report(query.Path, diagnosticHandler);
        var matcher = new EvtxXPathMatcher(query.XPath);
        if (query.Oldest) {
            foreach (SavedEventRecord record in ReadForward(query.Path, matcher, diagnosticHandler, cancellationToken)) {
                yield return record;
            }
            yield break;
        }

        diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
            Code = "EVXEVTX101",
            Severity = SavedEventReadDiagnosticSeverity.Information,
            Message = query.MaxEvents > 0
                ? $"Newest-first EVTX enumeration retains at most the newest {query.MaxEvents} matching record(s)."
                : "Newest-first EVTX enumeration buffers all matching records. Use Oldest=true or set MaxEvents for bounded-memory streaming."
        });
        foreach (SavedEventRecord record in NewestFirstSavedEventBuffer.Read(
                     ReadForward(query.Path, matcher, diagnosticHandler, cancellationToken),
                     query.MaxEvents,
                     cancellationToken)) {
            yield return record;
        }
    }

    private IEnumerable<SavedEventRecord> ReadForward(
        string path,
        EvtxXPathMatcher matcher,
        Action<SavedEventReadDiagnostic>? diagnosticHandler,
        CancellationToken cancellationToken) {

        var startInfo = new ProcessStartInfo {
            FileName = _executablePath,
            Arguments = $"-t 1 -o jsonl {QuoteArgument(Path.GetFullPath(path))}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = startInfo };
        bool started;
        try {
            started = process.Start();
        } catch (Exception exception) {
            throw new InvalidOperationException(
                $"Unable to start EVTX parser '{_executablePath}'. Install evtx_dump or provide its exact path.",
                exception);
        }
        if (!started) {
            throw new InvalidOperationException(
                $"Unable to start EVTX parser '{_executablePath}'. Install evtx_dump or provide its exact path.");
        }
        Task<string> errorTask = Task.Run(() => ReadBoundedError(process.StandardError), CancellationToken.None);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => TryKillTree(process));
        Stopwatch runtime = Stopwatch.StartNew();
        Stopwatch inactivity = Stopwatch.StartNew();
        bool completed = false;
        int inputRecords = 0;
        int normalizedRecords = 0;
        int rejectedRecords = 0;
        string? firstRejection = null;
        try {
            string? line;
            while ((line = ReadLineBounded(
                        process.StandardOutput,
                        process,
                        runtime,
                        inactivity,
                        cancellationToken)) != null) {

                inactivity.Restart();
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                inputRecords++;
                SavedEventRecord record;
                try {
                    record = EvtxDumpJsonProjector.Create(line);
                } catch (Exception exception) {
                    rejectedRecords++;
                    firstRejection ??= exception.Message;
                    continue;
                }
                normalizedRecords++;
                if (matcher.IsMatch(record.RawXml)) {
                    yield return record;
                }
            }
            WaitForExitBounded(process, runtime, cancellationToken);
            completed = true;
            string error = ReadErrorBounded(errorTask, process);
            if (!string.IsNullOrWhiteSpace(error)) {
                diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                    Code = process.ExitCode == 0 ? "EVXEVTX302" : "EVXEVTX303",
                    Severity = process.ExitCode == 0
                        ? SavedEventReadDiagnosticSeverity.Warning
                        : SavedEventReadDiagnosticSeverity.Error,
                    Message = error.Trim(),
                    Recovered = process.ExitCode == 0
                });
            }
            if (rejectedRecords > 0) {
                diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                    Code = "EVXEVTX304",
                    Severity = SavedEventReadDiagnosticSeverity.Warning,
                    Message = $"Rejected {rejectedRecords} malformed evtx_dump JSONL record(s) while retaining valid records. First failure: {firstRejection}",
                    Recovered = normalizedRecords > 0
                });
            }
            if (process.ExitCode != 0) {
                throw new InvalidDataException(
                    $"evtx_dump exited with code {process.ExitCode}. See saved-event diagnostics for parser output.");
            }
            if (inputRecords > 0 && normalizedRecords == 0) {
                throw new InvalidDataException(
                    "evtx_dump produced records, but EventViewerX could not normalize any of them. " +
                    "The executable output may be incompatible with this EventViewerX version.");
            }
        } finally {
            if (!completed) {
                TryKillTree(process);
            }
        }
    }

    private string? ReadLineBounded(
        StreamReader reader,
        Process process,
        Stopwatch runtime,
        Stopwatch inactivity,
        CancellationToken cancellationToken) {

        Task<string?> read = reader.ReadLineAsync();
        while (!read.Wait(100)) {
            ThrowIfProcessBoundExceeded(process, runtime, inactivity, cancellationToken);
        }
        return read.GetAwaiter().GetResult();
    }

    private void WaitForExitBounded(
        Process process,
        Stopwatch runtime,
        CancellationToken cancellationToken) {

        Stopwatch inactivity = Stopwatch.StartNew();
        while (!process.WaitForExit(100)) {
            ThrowIfProcessBoundExceeded(process, runtime, inactivity, cancellationToken);
        }
    }

    private void ThrowIfProcessBoundExceeded(
        Process process,
        Stopwatch runtime,
        Stopwatch inactivity,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        if (runtime.Elapsed <= _maximumRuntime && inactivity.Elapsed <= _maximumInactivity) {
            return;
        }
        TryKillTree(process);
        string boundary = runtime.Elapsed > _maximumRuntime
            ? $"maximum runtime {_maximumRuntime}"
            : $"maximum inactivity {_maximumInactivity}";
        throw new TimeoutException($"EVTX parser '{_executablePath}' exceeded its {boundary}.");
    }

    private static string ReadErrorBounded(Task<string> errorTask, Process process) {
        if (!errorTask.Wait(TimeSpan.FromSeconds(5))) {
            TryKillTree(process);
            throw new TimeoutException("EVTX parser diagnostic output did not close within five seconds.");
        }
        return errorTask.GetAwaiter().GetResult();
    }

    private static string ReadBoundedError(StreamReader reader) {
        const int maximumCharacters = 65536;
        var result = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) {
            int available = maximumCharacters - result.Length;
            if (available > 0) {
                result.Append(buffer, 0, Math.Min(read, available));
            }
        }
        if (result.Length == maximumCharacters) {
            result.Append(" [diagnostic output truncated by EventViewerX]");
        }
        return result.ToString();
    }

    private static void TryKillTree(Process process) {
        try {
            if (!process.HasExited) {
                System.Reflection.MethodInfo? killTree = typeof(Process).GetMethod(
                    nameof(Process.Kill),
                    new[] { typeof(bool) });
                if (killTree != null) {
                    killTree.Invoke(process, new object[] { true });
                } else {
                    process.Kill();
                }
            }
        } catch {
            // The process may exit between HasExited and Kill.
        }
    }

    private static string QuoteArgument(string value) {
        if (value.Length > 0 && value.All(static character =>
                !char.IsWhiteSpace(character) && character != '"')) {
            return value;
        }
        var result = new StringBuilder(value.Length + 2);
        result.Append('"');
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
