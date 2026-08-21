using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace EventViewerX;

/// <summary>
/// Owns classic Windows Event Log registration, desired-state configuration, removal, and writes.
/// Querying modern Windows Event Log channels remains the responsibility of <see cref="EventLogEngine"/>.
/// </summary>
public static class ClassicEventLogManager {
    private const int LogRemovalAttemptCount = 20;
    private const int LogRemovalDelayMilliseconds = 100;

    /// <summary>Returns whether a classic log exists. Operational failures are not converted to false.</summary>
    public static bool LogExists(
        string logName,
        string? machineName = null) {

        ValidateName(logName, nameof(logName), "Log");
        return string.IsNullOrWhiteSpace(machineName)
            ? EventLog.Exists(logName)
            : EventLog.Exists(logName, machineName);
    }

    /// <summary>Returns whether a source exists and, when supplied, verifies its owning log.</summary>
    public static bool SourceExists(
        string sourceName,
        string? logName = null,
        string? machineName = null) {

        ValidateName(sourceName, nameof(sourceName), "Source");
        string? registeredLog =
            GetRegisteredSourceLog(
                sourceName,
                machineName);
        return registeredLog != null &&
               (string.IsNullOrWhiteSpace(logName) ||
                string.Equals(
                    registeredLog,
                    logName,
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns a detached snapshot of a classic log and source.</summary>
    public static ClassicLogState GetState(
        string logName,
        string sourceName,
        string? machineName = null) {

        ValidateName(logName, nameof(logName), "Log");
        ValidateName(sourceName, nameof(sourceName), "Source");
        bool logExists = LogExists(logName, machineName);
        bool sourceExists = SourceExists(
            sourceName,
            logName: null,
            machineName);
        string? registeredLog = sourceExists
            ? GetRegisteredSourceLog(
                sourceName,
                machineName)
            : null;
        var state = new ClassicLogState {
            LogName = logName,
            SourceName = sourceName,
            MachineName = machineName,
            LogExists = logExists,
            SourceExists = sourceExists,
            SourceRegisteredLogName = registeredLog
        };
        if (!logExists) {
            return state;
        }

        using EventLog log = OpenLog(logName, machineName);
        state.LogDisplayName = string.IsNullOrWhiteSpace(log.LogDisplayName)
            ? null
            : log.LogDisplayName;
        state.MaximumKilobytes = log.MaximumKilobytes > int.MaxValue
            ? int.MaxValue
            : (int)log.MaximumKilobytes;
        state.OverflowActionName =
            ClassicLogOverflowActions.Normalize(log.OverflowAction);
        state.MinimumRetentionDays = log.MinimumRetentionDays;
        return state;
    }

    /// <summary>Creates or updates a classic log and its source as one explicit desired-state operation.</summary>
    public static ClassicEventLogEnsureResult EnsureLog(
        ClassicEventLogConfiguration configuration) {

        ValidateConfiguration(configuration);
        string sourceName = string.IsNullOrWhiteSpace(
                configuration.SourceName)
            ? configuration.LogName
            : configuration.SourceName;
        ClassicLogState before = GetState(
            configuration.LogName,
            sourceName,
            configuration.MachineName);
        if (before.SourceExists &&
            !string.Equals(
                before.SourceRegisteredLogName,
                configuration.LogName,
                StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"Event source '{sourceName}' is already registered to log '{before.SourceRegisteredLogName}', not '{configuration.LogName}'. Windows event sources cannot be moved implicitly.");
        }

        bool createdSource = false;
        if (!before.SourceExists) {
            ClassicEventSourceConfiguration supplied =
                configuration.Source ??
                new ClassicEventSourceConfiguration();
            createdSource = EnsureSource(
                new ClassicEventSourceConfiguration {
                    SourceName = sourceName,
                    LogName = configuration.LogName,
                    MachineName = configuration.MachineName,
                    MessageResourceFile =
                        supplied.MessageResourceFile,
                    ParameterResourceFile =
                        supplied.ParameterResourceFile,
                    CategoryResourceFile =
                        supplied.CategoryResourceFile,
                    CategoryCount =
                        supplied.CategoryCount
                });
        }

        bool updated = ApplyConfiguration(configuration);
        ClassicLogState after = GetState(
            configuration.LogName,
            sourceName,
            configuration.MachineName);
        VerifyConfiguration(
            configuration,
            sourceName,
            after);
        return new ClassicEventLogEnsureResult {
            Before = before,
            After = after,
            CreatedLog = !before.LogExists && after.LogExists,
            CreatedSource = createdSource,
            UpdatedConfiguration = updated,
            Success = true
        };
    }

    /// <summary>Registers a source if it does not already exist on the requested log.</summary>
    public static bool EnsureSource(
        ClassicEventSourceConfiguration configuration) {

        ValidateSourceConfiguration(configuration);
        if (SourceExists(
                configuration.SourceName,
                configuration.LogName,
                configuration.MachineName)) {
            return false;
        }
        if (SourceExists(
                configuration.SourceName,
                logName: null,
                configuration.MachineName)) {
            string registeredLog =
                GetRegisteredSourceLog(
                    configuration.SourceName,
                    configuration.MachineName) ??
                string.Empty;
            throw new InvalidOperationException(
                $"Event source '{configuration.SourceName}' is already registered to log '{registeredLog}'.");
        }

        var data = new EventSourceCreationData(
            configuration.SourceName,
            configuration.LogName) {
            CategoryCount = configuration.CategoryCount
        };
        if (!string.IsNullOrWhiteSpace(configuration.MachineName)) {
            data.MachineName = configuration.MachineName;
        }
        if (!string.IsNullOrWhiteSpace(
                configuration.MessageResourceFile)) {
            data.MessageResourceFile =
                configuration.MessageResourceFile;
        }
        if (!string.IsNullOrWhiteSpace(
                configuration.ParameterResourceFile)) {
            data.ParameterResourceFile =
                configuration.ParameterResourceFile;
        }
        if (!string.IsNullOrWhiteSpace(
                configuration.CategoryResourceFile)) {
            data.CategoryResourceFile =
                configuration.CategoryResourceFile;
        }
        try {
            EventLog.CreateEventSource(data);
        } catch (Win32Exception exception)
            when (exception.NativeErrorCode == 183 &&
                  SourceExists(
                      configuration.SourceName,
                      configuration.LogName,
                      configuration.MachineName)) {
            return false;
        }
        if (!SourceExists(
                configuration.SourceName,
                configuration.LogName,
                configuration.MachineName)) {
            throw new InvalidOperationException(
                $"Windows did not retain event source '{configuration.SourceName}' on log '{configuration.LogName}'.");
        }
        return true;
    }

    /// <summary>Deletes a classic log. Returns false only when it is absent.</summary>
    public static bool RemoveLog(
        string logName,
        string? machineName = null) {

        if (!LogExists(logName, machineName)) {
            return false;
        }
        ExecuteLogRemovalWithRetry(
            () => {
                if (string.IsNullOrWhiteSpace(machineName)) {
                    EventLog.Delete(logName);
                } else {
                    EventLog.Delete(logName, machineName);
                }
            },
            () => LogExists(logName, machineName),
            static () => Thread.Sleep(
                LogRemovalDelayMilliseconds));
        if (LogExists(logName, machineName)) {
            throw new InvalidOperationException(
                $"Windows reported that classic log '{logName}' was deleted, but it remains present.");
        }
        return true;
    }

    internal static void ExecuteLogRemovalWithRetry(
        Action remove,
        Func<bool> logExists,
        Action wait) {

        for (int attempt = 0;
             attempt < LogRemovalAttemptCount;
             attempt++) {
            try {
                remove();
                return;
            } catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception) {
                if (!logExists()) {
                    return;
                }
                if (attempt + 1 >= LogRemovalAttemptCount) {
                    throw;
                }
                wait();
            }
        }
    }

    /// <summary>Deletes a classic source. Returns false only when it is absent.</summary>
    public static bool RemoveSource(
        string sourceName,
        string? machineName = null,
        string? logName = null) {

        if (!SourceExists(sourceName, logName, machineName)) {
            return false;
        }
        if (string.IsNullOrWhiteSpace(machineName)) {
            EventLog.DeleteEventSource(sourceName);
        } else {
            EventLog.DeleteEventSource(sourceName, machineName);
        }
        if (SourceExists(
                sourceName,
                logName: null,
                machineName)) {
            throw new InvalidOperationException(
                $"Windows reported that classic event source '{sourceName}' was deleted, but it remains present.");
        }
        return true;
    }

    /// <summary>
    /// Writes one classic event. A missing source throws unless CreateSourceIfMissing is explicitly enabled.
    /// </summary>
    public static void Write(ClassicEventWriteRequest request) {
        ValidateWriteRequest(request);
        bool sourceExists = SourceExists(
            request.SourceName,
            request.LogName,
            request.MachineName);
        if (!sourceExists) {
            if (!request.CreateSourceIfMissing) {
                throw new InvalidOperationException(
                    $"Event source '{request.SourceName}' is not registered to log '{request.LogName}'. Register it with ClassicEventLogManager.EnsureSource or enable CreateSourceIfMissing explicitly.");
            }
            EnsureSource(new ClassicEventSourceConfiguration {
                SourceName = request.SourceName,
                LogName = request.LogName,
                MachineName = request.MachineName
            });
        }

        using EventLog log = OpenLog(
            request.LogName,
            request.MachineName,
            request.SourceName);
        string[] replacementStrings =
            request.ReplacementStrings?.ToArray() ??
            Array.Empty<string>();
        if (request.RawData == null &&
            replacementStrings.Length == 0) {
            log.WriteEntry(
                request.Message,
                request.EntryType,
                request.EventId,
                checked((short)request.Category));
            return;
        }

        var instance = new EventInstance(
            request.EventId,
            request.Category,
            request.EntryType);
        string[] insertionStrings = new[] { request.Message }
            .Concat(replacementStrings)
            .ToArray();
        log.WriteEvent(
            instance,
            request.RawData,
            insertionStrings);
    }

    private static bool ApplyConfiguration(
        ClassicEventLogConfiguration configuration) {

        bool changed = false;
        using EventLog log = OpenLog(
            configuration.LogName,
            configuration.MachineName);
        if (configuration.MaximumKilobytes.HasValue &&
            log.MaximumKilobytes !=
            configuration.MaximumKilobytes.Value) {
            log.MaximumKilobytes =
                configuration.MaximumKilobytes.Value;
            changed = true;
        }
        if (!configuration.OverflowAction.HasValue) {
            return changed;
        }
        OverflowAction overflowAction =
            configuration.OverflowAction.Value;
        int retention = overflowAction ==
            OverflowAction.OverwriteOlder
            ? configuration.RetentionDays!.Value
            : 0;
        if (log.OverflowAction != overflowAction ||
            log.MinimumRetentionDays != retention) {
            log.ModifyOverflowPolicy(
                overflowAction,
                retention);
            changed = true;
        }
        return changed;
    }

    private static EventLog OpenLog(
        string logName,
        string? machineName,
        string? sourceName = null) {

        return new EventLog(
            logName,
            string.IsNullOrWhiteSpace(machineName)
                ? "."
                : machineName,
            sourceName ?? string.Empty);
    }

    private static string? GetRegisteredSourceLog(
        string sourceName,
        string? machineName) {

        if (!string.IsNullOrWhiteSpace(machineName)) {
            return EventLog.SourceExists(
                    sourceName,
                    machineName)
                ? EventLog.LogNameFromSourceName(
                    sourceName,
                    machineName)
                : null;
        }

        using RegistryKey? eventLogRoot =
            Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\EventLog",
                writable: false);
        if (eventLogRoot == null) {
            return null;
        }
        foreach (string logName in
                 eventLogRoot.GetSubKeyNames()) {
            using RegistryKey? source =
                eventLogRoot.OpenSubKey(
                    $"{logName}\\{sourceName}",
                    writable: false);
            if (source != null) {
                return logName;
            }
        }
        return null;
    }

    private static void ValidateConfiguration(
        ClassicEventLogConfiguration configuration) {

        if (configuration == null) {
            throw new ArgumentNullException(nameof(configuration));
        }
        ValidateName(
            configuration.LogName,
            nameof(configuration.LogName),
            "Log");
        if (configuration.MaximumKilobytes is <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(configuration.MaximumKilobytes),
                "MaximumKilobytes must be greater than zero when specified.");
        }
        if (configuration.OverflowAction.HasValue &&
            !Enum.IsDefined(
                typeof(OverflowAction),
                configuration.OverflowAction.Value)) {
            throw new ArgumentOutOfRangeException(
                nameof(configuration.OverflowAction));
        }
        if (configuration.OverflowAction ==
            OverflowAction.OverwriteOlder) {
            if (configuration.RetentionDays is null or < 1 or > 365) {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration.RetentionDays),
                    "RetentionDays must be between 1 and 365 for OverwriteOlder.");
            }
        } else if (configuration.RetentionDays.HasValue) {
            throw new ArgumentException(
                "RetentionDays is only valid with OverwriteOlder.",
                nameof(configuration));
        }
    }

    private static void VerifyConfiguration(
        ClassicEventLogConfiguration configuration,
        string sourceName,
        ClassicLogState after) {

        if (!after.LogExists ||
            !after.SourceExists ||
            !string.Equals(
                after.SourceRegisteredLogName,
                configuration.LogName,
                StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"Windows did not retain the requested classic log/source state for '{configuration.LogName}/{sourceName}'.");
        }
        if (configuration.MaximumKilobytes.HasValue &&
            after.MaximumKilobytes !=
            configuration.MaximumKilobytes.Value) {
            throw new InvalidOperationException(
                $"Classic log '{configuration.LogName}' retained MaximumKilobytes={after.MaximumKilobytes} instead of {configuration.MaximumKilobytes.Value}.");
        }
        if (!configuration.OverflowAction.HasValue) {
            return;
        }
        string requestedAction =
            ClassicLogOverflowActions.Normalize(
                configuration.OverflowAction.Value);
        int requestedRetention =
            configuration.OverflowAction ==
            OverflowAction.OverwriteOlder
                ? configuration.RetentionDays!.Value
                : 0;
        if (!string.Equals(
                after.OverflowActionName,
                requestedAction,
                StringComparison.OrdinalIgnoreCase) ||
            after.MinimumRetentionDays !=
            requestedRetention) {
            throw new InvalidOperationException(
                $"Classic log '{configuration.LogName}' did not retain the requested overflow policy.");
        }
    }

    private static void ValidateSourceConfiguration(
        ClassicEventSourceConfiguration configuration) {

        if (configuration == null) {
            throw new ArgumentNullException(nameof(configuration));
        }
        ValidateName(
            configuration.SourceName,
            nameof(configuration.SourceName),
            "Source");
        ValidateName(
            configuration.LogName,
            nameof(configuration.LogName),
            "Log");
        if (configuration.CategoryCount < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(configuration.CategoryCount));
        }
        if (configuration.CategoryCount > 0 &&
            string.IsNullOrWhiteSpace(
                configuration.CategoryResourceFile)) {
            throw new ArgumentException(
                "CategoryResourceFile is required when CategoryCount is greater than zero.",
                nameof(configuration));
        }
    }

    private static void ValidateWriteRequest(
        ClassicEventWriteRequest request) {

        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        ValidateName(
            request.SourceName,
            nameof(request.SourceName),
            "Source");
        ValidateName(
            request.LogName,
            nameof(request.LogName),
            "Log");
        if (request.Message == null) {
            throw new ArgumentNullException(nameof(request.Message));
        }
        if (request.Category is < 0 or > short.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(request.Category),
                $"Category must be between 0 and {short.MaxValue}.");
        }
        if (request.EventId is < 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(request.EventId),
                $"EventId must be between 0 and {ushort.MaxValue}.");
        }
        if (!Enum.IsDefined(
                typeof(EventLogEntryType),
                request.EntryType)) {
            throw new ArgumentOutOfRangeException(
                nameof(request.EntryType));
        }
    }

    private static void ValidateName(
        string value,
        string parameterName,
        string label) {

        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException(
                $"{label} name cannot be null or empty.",
                parameterName);
        }
    }
}
