using System.Globalization;
using System.Security.Principal;
using Microsoft.Win32;

namespace EventViewerX;

/// <summary>Windows Event Collector runtime status and host readiness operations.</summary>
public static partial class CollectorSubscriptionManager {
    /// <summary>Reads the local runtime state and per-source errors for an existing subscription.</summary>
    public static CollectorSubscriptionRuntimeStatus GetCollectorSubscriptionRuntimeStatus(
        string name,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Subscription name cannot be empty.", nameof(name));
        }
        string output = RunWecUtil(new[] { "gr", name.Trim() }, cancellationToken);
        return ParseRuntimeStatus(output, name.Trim());
    }

    /// <summary>Assesses local WEC, WinRM, listener, and ForwardedEvents prerequisites without changing state.</summary>
    public static CollectorReadinessStatus GetCollectorReadiness(
        CancellationToken cancellationToken = default) {

        var issues = new List<string>();
        bool administrator = IsAdministrator();
        if (!administrator) {
            issues.Add("Administrative rights are required to initialize or change collector subscriptions.");
        }

        bool installed = false;
        bool running = false;
        string startMode = string.Empty;
        EventReadinessDiagnosticKind collectorDiagnostic = EventReadinessDiagnosticKind.None;
        try {
            (installed, running) = ReadServiceState("Wecsvc", cancellationToken);
            startMode = installed ? ReadServiceStartMode("Wecsvc") : string.Empty;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            collectorDiagnostic = ClassifyReadinessException(exception);
            issues.Add("Windows Event Collector service state could not be inspected: " + exception.Message);
        }
        bool winRmInstalled = false;
        bool winRmRunning = false;
        EventReadinessDiagnosticKind winRmDiagnostic = EventReadinessDiagnosticKind.None;
        try {
            (winRmInstalled, winRmRunning) = ReadServiceState("WinRM", cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            winRmDiagnostic = ClassifyReadinessException(exception);
            issues.Add("WinRM service state could not be inspected: " + exception.Message);
        }
        bool listener = false;
        EventReadinessDiagnosticKind listenerDiagnostic = EventReadinessDiagnosticKind.None;
        try {
            listener = HasWinRmListener(cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            listenerDiagnostic = ClassifyReadinessException(exception);
            issues.Add("WinRM listener state could not be inspected: " + exception.Message);
        }
        (bool forwardedExists,
            bool forwardedEnabled,
            EventReadinessDiagnosticKind forwardedDiagnostic,
            string forwardedError) = InspectForwardedEvents(
                static () => {
                    using var configuration =
                        new System.Diagnostics.Eventing.Reader.EventLogConfiguration("ForwardedEvents");
                    return (true, configuration.IsEnabled);
                });
        if (forwardedError.Length > 0) {
            issues.Add($"ForwardedEvents readiness could not be read: {forwardedError}");
        }
        issues.AddRange(GetConfirmedReadinessIssues(
            installed,
            running,
            collectorDiagnostic,
            winRmInstalled,
            winRmRunning,
            winRmDiagnostic,
            listener,
            listenerDiagnostic,
            forwardedExists,
            forwardedEnabled,
            forwardedDiagnostic));

        return new CollectorReadinessStatus {
            MachineName = Environment.MachineName,
            IsAdministrator = administrator,
            CollectorServiceInstalled = installed,
            CollectorServiceRunning = running,
            CollectorServiceDiagnosticKind = collectorDiagnostic,
            CollectorServiceStartMode = startMode,
            WinRmServiceRunning = winRmRunning,
            WinRmDiagnosticKind = winRmDiagnostic,
            WinRmListenerAvailable = listener,
            WinRmListenerDiagnosticKind = listenerDiagnostic,
            ForwardedEventsExists = forwardedExists,
            ForwardedEventsEnabled = forwardedEnabled,
            ForwardedEventsDiagnosticKind = forwardedDiagnostic,
            Issues = issues
        };
    }

    internal static IReadOnlyList<string> GetConfirmedReadinessIssues(
        bool collectorInstalled,
        bool collectorRunning,
        EventReadinessDiagnosticKind collectorDiagnostic,
        bool winRmInstalled,
        bool winRmRunning,
        EventReadinessDiagnosticKind winRmDiagnostic,
        bool listenerAvailable,
        EventReadinessDiagnosticKind listenerDiagnostic,
        bool forwardedEventsExists,
        bool forwardedEventsEnabled,
        EventReadinessDiagnosticKind forwardedEventsDiagnostic) {

        var issues = new List<string>();
        if (collectorDiagnostic == EventReadinessDiagnosticKind.None) {
            if (!collectorInstalled) {
                issues.Add("Windows Event Collector service (Wecsvc) is not installed.");
            } else if (!collectorRunning) {
                issues.Add("Windows Event Collector service (Wecsvc) is not running.");
            }
        }
        if (winRmDiagnostic == EventReadinessDiagnosticKind.None &&
            (!winRmInstalled || !winRmRunning)) {
            issues.Add("Windows Remote Management (WinRM) is not running.");
        }
        if (listenerDiagnostic == EventReadinessDiagnosticKind.None && !listenerAvailable) {
            issues.Add("No enabled WinRM HTTP or HTTPS listener is available.");
        }
        if (forwardedEventsDiagnostic == EventReadinessDiagnosticKind.None) {
            if (!forwardedEventsExists) {
                issues.Add("ForwardedEvents channel is not registered.");
            } else if (!forwardedEventsEnabled) {
                issues.Add("ForwardedEvents channel is disabled.");
            }
        }
        return issues;
    }

    internal static (
        bool Exists,
        bool Enabled,
        EventReadinessDiagnosticKind Diagnostic,
        string Error) InspectForwardedEvents(
            Func<(bool Exists, bool Enabled)> inspector) {

        try {
            (bool exists, bool enabled) = inspector();
            return (exists, enabled, EventReadinessDiagnosticKind.None, string.Empty);
        } catch (System.Diagnostics.Eventing.Reader.EventLogNotFoundException) {
            return (false, false, EventReadinessDiagnosticKind.None, string.Empty);
        } catch (Exception exception) {
            return (false, false, ClassifyReadinessException(exception), exception.Message);
        }
    }

    /// <summary>Runs the inbox WinRM and WEC quick configuration, then returns verified readiness.</summary>
    public static CollectorReadinessStatus InitializeCollector(
        bool configureWinRm = true,
        CancellationToken cancellationToken = default) {

        if (!IsAdministrator()) {
            throw new UnauthorizedAccessException("Administrative rights are required to initialize Windows Event Collector.");
        }
        if (configureWinRm) {
            RunWinRm(new[] { "quickconfig", "-quiet" }, cancellationToken);
        }
        RunWecUtil(new[] { "qc", "/q" }, cancellationToken);
        CollectorReadinessStatus readiness = GetCollectorReadiness(cancellationToken);
        if (!readiness.IsReady) {
            throw new InvalidOperationException(
                "Windows collector initialization completed but readiness checks still failed: " +
                string.Join(" ", readiness.Issues));
        }
        return readiness;
    }

    internal static CollectorSubscriptionRuntimeStatus ParseRuntimeStatus(string output, string fallbackName) {
        var result = new CollectorSubscriptionRuntimeStatus {
            SubscriptionName = fallbackName,
            RawStatus = output ?? string.Empty
        };
        var sources = new List<CollectorSubscriptionSourceRuntimeStatus>();
        CollectorSubscriptionSourceRuntimeStatus? currentSource = null;
        bool inSources = false;
        foreach (string rawLine in (output ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)) {
            string line = rawLine.Trim();
            if (line.Length == 0) {
                continue;
            }
            if (line.Equals("EventSources:", StringComparison.OrdinalIgnoreCase)) {
                inSources = true;
                continue;
            }
            int separator = line.IndexOf(':');
            if (inSources && separator < 0) {
                currentSource = new CollectorSubscriptionSourceRuntimeStatus { Address = line };
                sources.Add(currentSource);
                continue;
            }
            if (separator < 0) {
                continue;
            }
            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            if (currentSource == null) {
                ApplyOverallValue(result, key, value);
            } else {
                ApplySourceValue(currentSource, key, value);
            }
        }
        result.Sources = sources;
        return result;
    }

    private static void ApplyOverallValue(CollectorSubscriptionRuntimeStatus result, string key, string value) {
        switch (key.ToLowerInvariant()) {
            case "subscription": result.SubscriptionName = value; break;
            case "runtimestatus": result.Status = value; break;
            case "events processed": result.EventsProcessed = ParseLong(value); break;
            case "lasterror": result.LastErrorCode = ParseErrorCode(value); break;
            case "errormessage": result.ErrorMessage = value; break;
        }
    }

    private static void ApplySourceValue(CollectorSubscriptionSourceRuntimeStatus result, string key, string value) {
        switch (key.ToLowerInvariant()) {
            case "runtimestatus": result.Status = value; break;
            case "events processed": result.EventsProcessed = ParseLong(value); break;
            case "lasterror": result.LastErrorCode = ParseErrorCode(value); break;
            case "errormessage": result.ErrorMessage = value; break;
            case "lastheartbeattime":
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset heartbeat)) {
                    result.LastHeartbeatTime = heartbeat;
                }
                break;
        }
    }

    private static uint? ParseErrorCode(string value) {
        string normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value.Substring(2)
            : value;
        return uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed)
            ? parsed
            : null;
    }

    private static long? ParseLong(string value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;

    private static bool IsAdministrator() {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static (bool Installed, bool Running) ReadServiceState(
        string serviceName,
        CancellationToken cancellationToken) {

        try {
            string output = RunSystemTool("sc.exe", new[] { "query", serviceName }, cancellationToken);
            return (true, output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0);
        } catch (InvalidOperationException exception) when (
            exception.Message.IndexOf("1060", StringComparison.OrdinalIgnoreCase) >= 0) {
            return (false, false);
        }
    }

    private static string ReadServiceStartMode(string serviceName) {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);
        object? value = key?.GetValue("Start");
        return value is int start ? start switch {
            2 => "Automatic",
            3 => "Manual",
            4 => "Disabled",
            _ => start.ToString(CultureInfo.InvariantCulture)
        } : string.Empty;
    }

    private static bool HasWinRmListener(CancellationToken cancellationToken) {
        string output = RunWinRm(
            new[] { "enumerate", "winrm/config/listener" },
            cancellationToken);
        return output.IndexOf("Enabled = true", StringComparison.OrdinalIgnoreCase) >= 0 &&
               (output.IndexOf("Transport = HTTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                output.IndexOf("Transport = HTTPS", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static EventReadinessDiagnosticKind ClassifyReadinessException(Exception exception) {
        for (Exception? current = exception; current != null; current = current.InnerException) {
            if (current is UnauthorizedAccessException || current is System.Security.SecurityException ||
                current.Message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0) {
                return EventReadinessDiagnosticKind.AccessDenied;
            }
            if (current is TimeoutException || current is OperationCanceledException) {
                return EventReadinessDiagnosticKind.Timeout;
            }
        }
        return EventReadinessDiagnosticKind.Error;
    }

    private static string RunWinRm(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {

        string script = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "winrm.vbs");
        return RunSystemTool(
            "cscript.exe",
            new[] { "//nologo", script }.Concat(arguments).ToArray(),
            cancellationToken);
    }

    private static string RunSystemTool(
        string executableName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {

        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            executableName);
        if (!File.Exists(executable)) {
            throw new FileNotFoundException($"Windows system utility '{executableName}' was not found.", executable);
        }
        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments) {
#if NET472
            startInfo.Arguments += startInfo.Arguments.Length == 0
                ? QuoteProcessArgument(argument)
                : " " + QuoteProcessArgument(argument);
#else
            startInfo.ArgumentList.Add(argument);
#endif
        }
        return BoundedProcessRunner.Run(startInfo, WecUtilTimeout, cancellationToken);
    }
}
