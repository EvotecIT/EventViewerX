using System.Diagnostics.Eventing.Reader;

namespace EventViewerX;

/// <summary>
/// Describes configuration and status of a single event log.
/// </summary>
public class EventLogDetails {
    private readonly List<EventLogDetailsDiagnostic> _diagnostics = new();

    /// <summary>Machine that hosts the log.</summary>
    public string MachineName { get; set; } = string.Empty;
    /// <summary>Name of the log.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Type of the log.</summary>
    public string LogType { get; set; } = string.Empty;
    /// <summary>Log isolation mode.</summary>
    public EventLogIsolation LogIsolation { get; set; }
    /// <summary>Indicates whether the log is enabled.</summary>
    public bool IsEnabled { get; set; }
    /// <summary>Indicates whether the log file reached its maximum size.</summary>
    public bool? IsLogFull { get; set; }
    /// <summary>Maximum configured size in bytes.</summary>
    public long MaximumSizeInBytes { get; set; }
    /// <summary>Path to the physical log file.</summary>
    public string LogFilePath { get; set; } = string.Empty;
    /// <summary>Current logging mode.</summary>
    public string LogMode { get; set; } = string.Empty;
    /// <summary>Owning provider name.</summary>
    public string OwningProviderName { get; set; } = string.Empty;
    /// <summary>List of providers registered for the log.</summary>
    public List<string> ProviderNames { get; set; } = new List<string>();
    /// <summary>Provider level mask.</summary>
    public string ProviderLevel { get; set; } = string.Empty;
    /// <summary>Provider keywords mask.</summary>
    public string ProviderKeywords { get; set; } = string.Empty;
    /// <summary>Buffer size used by the provider.</summary>
    public int ProviderBufferSize { get; set; }
    /// <summary>Minimum number of buffers for the provider.</summary>
    public int ProviderMinimumNumberOfBuffers { get; set; }
    /// <summary>Maximum number of buffers for the provider.</summary>
    public int ProviderMaximumNumberOfBuffers { get; set; }
    /// <summary>Provider latency setting.</summary>
    public int ProviderLatency { get; set; }
    /// <summary>Control GUID for the provider.</summary>
    public string ProviderControlGuid { get; set; } = string.Empty;
    /// <summary>Creation time of the log file.</summary>
    public DateTime? CreationTime { get; set; }
    /// <summary>Last access time of the log file.</summary>
    public DateTime? LastAccessTime { get; set; }
    /// <summary>Last write time of the log file.</summary>
    public DateTime? LastWriteTime { get; set; }
    /// <summary>Current file size in bytes.</summary>
    public long? FileSize { get; set; }
    /// <summary>Maximum configured file size in bytes.</summary>
    public long? FileSizeMaximum { get; set; }
    /// <summary>Current file size in megabytes.</summary>
    public double? FileSizeCurrentMB { get; set; }
    /// <summary>Maximum file size in megabytes.</summary>
    public double? FileSizeMaximumMB { get; set; }
    /// <summary>Total number of records.</summary>
    public long? RecordCount { get; set; }
    /// <summary>Oldest record number.</summary>
    public long? OldestRecordNumber { get; set; }
    /// <summary>Security descriptor of the log.</summary>
    public string SecurityDescriptor { get; set; } = string.Empty;
    /// <summary>Indicates if the log is classic type.</summary>
    public bool IsClassicLog { get; set; }

    /// <summary>Newest event timestamp.</summary>
    public DateTime? NewestEvent { get; set; }
    /// <summary>Oldest event timestamp.</summary>
    public DateTime? OldestEvent { get; set; }
    /// <summary>Additional log attributes.</summary>
    public int? Attributes { get; set; }

    /// <summary>Property-level failures captured while constructing this otherwise usable snapshot.</summary>
    public IReadOnlyList<EventLogDetailsDiagnostic> Diagnostics => _diagnostics;

    /// <summary>True when one or more configuration or runtime-information properties could not be projected.</summary>
    public bool HasDiagnostics => _diagnostics.Count > 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogDetails"/> class.
    /// </summary>
    /// <param name="internalLogger">Compatibility logger parameter; property failures are retained in <see cref="Diagnostics"/>.</param>
    /// <param name="machineName">Name of the computer hosting the log.</param>
    /// <param name="logConfig">Event log configuration.</param>
    /// <param name="logInfoObj">Optional log information object.</param>
    public EventLogDetails(InternalLogger internalLogger, string machineName, EventLogConfiguration logConfig, EventLogInformation? logInfoObj) {
        if (internalLogger == null) throw new ArgumentNullException(nameof(internalLogger));
        if (logConfig == null) throw new ArgumentNullException(nameof(logConfig));

        MachineName = machineName ?? string.Empty;
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.LogName), () => logConfig.LogName ?? string.Empty, value => LogName = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.LogType), () => logConfig.LogType.ToString(), value => LogType = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.IsEnabled), () => logConfig.IsEnabled, value => IsEnabled = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.MaximumSizeInBytes), () => logConfig.MaximumSizeInBytes, value => MaximumSizeInBytes = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.LogFilePath), () => logConfig.LogFilePath ?? string.Empty, value => LogFilePath = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.LogIsolation), () => logConfig.LogIsolation, value => LogIsolation = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.LogMode), () => logConfig.LogMode.ToString(), value => LogMode = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.OwningProviderName), () => logConfig.OwningProviderName ?? string.Empty, value => OwningProviderName = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderNames), () => new List<string>(logConfig.ProviderNames), value => ProviderNames = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderBufferSize), () => logConfig.ProviderBufferSize.GetValueOrDefault(), value => ProviderBufferSize = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderMinimumNumberOfBuffers), () => logConfig.ProviderMinimumNumberOfBuffers.GetValueOrDefault(), value => ProviderMinimumNumberOfBuffers = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderMaximumNumberOfBuffers), () => logConfig.ProviderMaximumNumberOfBuffers.GetValueOrDefault(), value => ProviderMaximumNumberOfBuffers = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderLatency), () => logConfig.ProviderLatency.GetValueOrDefault(), value => ProviderLatency = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderControlGuid), () => logConfig.ProviderControlGuid?.ToString() ?? string.Empty, value => ProviderControlGuid = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.SecurityDescriptor), () => logConfig.SecurityDescriptor ?? string.Empty, value => SecurityDescriptor = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderLevel), () => logConfig.ProviderLevel?.ToString() ?? string.Empty, value => ProviderLevel = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.ProviderKeywords), () => logConfig.ProviderKeywords?.ToString() ?? string.Empty, value => ProviderKeywords = value, EventLogDetailsStatus.LogConfigurationUnavailable);
        Capture(EventLogDetailsReadStage.Configuration, nameof(logConfig.IsClassicLog), () => logConfig.IsClassicLog, value => IsClassicLog = value, EventLogDetailsStatus.LogConfigurationUnavailable);

        if (logInfoObj != null) {
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.FileSize), () => logInfoObj.FileSize, value => FileSize = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.RecordCount), () => logInfoObj.RecordCount, value => RecordCount = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.OldestRecordNumber), () => logInfoObj.OldestRecordNumber, value => OldestRecordNumber = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.LastAccessTime), () => logInfoObj.LastAccessTime, value => LastAccessTime = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.LastWriteTime), () => logInfoObj.LastWriteTime, value => LastWriteTime = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.CreationTime), () => logInfoObj.CreationTime, value => CreationTime = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.IsLogFull), () => logInfoObj.IsLogFull, value => IsLogFull = value, EventLogDetailsStatus.LogInformationUnavailable);
            Capture(EventLogDetailsReadStage.RuntimeInformation, nameof(logInfoObj.Attributes), () => logInfoObj.Attributes, value => Attributes = value, EventLogDetailsStatus.LogInformationUnavailable);
            FileSizeCurrentMB = ConvertSize(FileSize, "B", "MB", 2);
        }

        FileSizeMaximum = MaximumSizeInBytes;
        FileSizeMaximumMB = ConvertSize(FileSizeMaximum, "B", "MB", 2);
    }

    private void Capture<T>(
        EventLogDetailsReadStage stage,
        string propertyName,
        Func<T> read,
        Action<T> assign,
        EventLogDetailsStatus fallbackStatus) {

        try {
            assign(read());
        } catch (Exception ex) {
            _diagnostics.Add(new EventLogDetailsDiagnostic {
                Stage = stage,
                PropertyName = propertyName,
                Status = ClassifyProjectionFailure(ex, fallbackStatus),
                ErrorType = ex.GetType().Name,
                Message = $"Couldn't read {stage} property '{propertyName}' for '{(string.IsNullOrWhiteSpace(LogName) ? "event log" : LogName)}' on '{MachineName}': {ex.Message}"
            });
        }
    }

    private static EventLogDetailsStatus ClassifyProjectionFailure(Exception exception, EventLogDetailsStatus fallbackStatus) {
        if (exception is EventLogSessionException sessionException) {
            return EventLogCatalog.MapSessionFailureStatus(sessionException.Status);
        }

        return exception switch {
            UnauthorizedAccessException => EventLogDetailsStatus.AccessDenied,
            TimeoutException => EventLogDetailsStatus.Timeout,
            _ => fallbackStatus
        };
    }

    /// <summary>
    /// Converts a numeric size value between units.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <param name="fromUnit">Current unit of measure.</param>
    /// <param name="toUnit">Destination unit of measure.</param>
    /// <param name="precision">Number of decimal places.</param>
    /// <returns>Converted value.</returns>
    private static double ConvertSize(double? value, string fromUnit, string toUnit, int precision) {
        if (!value.HasValue || value.Value <= 0) {
            return 0;
        }

        double size = value.Value;

        switch (fromUnit.ToUpperInvariant()) {
            case "B":
                break;
            case "KB":
                size *= 1024.0;
                break;
            case "MB":
                size *= 1024.0 * 1024.0;
                break;
            case "GB":
                size *= 1024.0 * 1024.0 * 1024.0;
                break;
            case "TB":
                size *= 1024.0 * 1024.0 * 1024.0 * 1024.0;
                break;
            default:
                // Treat unknown units as bytes
                break;
        }

        switch (toUnit.ToUpperInvariant()) {
            case "B":
                break;
            case "KB":
                size /= 1024.0;
                break;
            case "MB":
                size /= 1024.0 * 1024.0;
                break;
            case "GB":
                size /= 1024.0 * 1024.0 * 1024.0;
                break;
            case "TB":
                size /= 1024.0 * 1024.0 * 1024.0 * 1024.0;
                break;
            default:
                // Keep size unchanged for unknown units
                break;
        }

        return Math.Round(size, precision);
    }
}
