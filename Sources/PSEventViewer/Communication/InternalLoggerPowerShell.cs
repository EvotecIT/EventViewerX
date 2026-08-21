namespace PSEventViewer;
/// <summary>
/// This class allow connecting to the InternalLogger class of ADPlayground and act on events from it in different streams
/// </summary>
public class InternalLoggerPowerShell {
    private readonly InternalLogger _logger;
    private readonly Action<string>? _writeVerboseAction;
    private readonly Action<string>? _writeDebugAction;
    private readonly Action<InformationRecord>? _writeInformationAction;
    private readonly Action<string>? _writeWarningAction;
    private readonly Action<ErrorRecord>? _writeErrorAction;
    private readonly Action<ProgressRecord>? _writeProgressAction;

    /// <summary>
    /// Initialize the InternalLoggerPowerShell class
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="writeVerboseAction"></param>
    /// <param name="writeWarningAction"></param>
    /// <param name="writeDebugAction"></param>
    /// <param name="writeErrorAction"></param>
    /// <param name="writeProgressAction"></param>
    /// <param name="writeInformationAction"></param>
    public InternalLoggerPowerShell(InternalLogger logger, Action<string>? writeVerboseAction = null, Action<string>? writeWarningAction = null, Action<string>? writeDebugAction = null, Action<ErrorRecord>? writeErrorAction = null, Action<ProgressRecord>? writeProgressAction = null, Action<InformationRecord>? writeInformationAction = null) {
        _logger = logger;

        if (writeVerboseAction != null) {
            _writeVerboseAction = writeVerboseAction;
            _logger.OnVerboseMessage += Logger_OnVerboseMessage;
        }

        if (writeWarningAction != null) {
            _writeWarningAction = writeWarningAction;
            _logger.OnWarningMessage += Logger_OnWarningMessage;
        }

        if (writeDebugAction != null) {
            _writeDebugAction = writeDebugAction;
            _logger.OnDebugMessage += Logger_OnDebugMessage;
        }

        if (writeErrorAction != null) {
            _writeErrorAction = writeErrorAction;
            _logger.OnErrorMessage += Logger_OnErrorMessage;
        }

        if (writeProgressAction != null) {
            _writeProgressAction = writeProgressAction;
            _logger.OnProgressMessage += Logger_OnProgressMessage;
        }

        if (writeInformationAction != null) {
            _writeInformationAction = writeInformationAction;
            _logger.OnInformationMessage += Logger_OnInformationMessage;
        }
    }

    /// <summary>
    /// Message event handler
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Logger_OnVerboseMessage(object? sender, LogEventArgs e) {
        if (e.Args != null && e.Args.Length > 0) {
            WriteVerbose(e.Message, e.Args);
        } else {
            WriteVerbose(e.Message);
        }
    }
    private void Logger_OnDebugMessage(object? sender, LogEventArgs e) {
        WriteDebug(e.Message);
    }
    private void Logger_OnWarningMessage(object? sender, LogEventArgs e) {
        WriteWarning(e.Message);
    }
    private void Logger_OnErrorMessage(object? sender, LogEventArgs e) {
        ErrorRecord errorRecord = new ErrorRecord(new Exception(e.Message), "1", ErrorCategory.NotSpecified, null);
        WriteError(errorRecord);
    }
    private int _activityIdCounter = 0;

    private int _currentActivityId = 1;
    private bool _isCurrentActivityCompleted = true;

    private int GetNextActivityId() {
        return ++_activityIdCounter;
    }
    private void Logger_OnProgressMessage(object? sender, LogEventArgs e) {
        if (_isCurrentActivityCompleted) {
            _currentActivityId = GetNextActivityId();
            _isCurrentActivityCompleted = false;
        }
        var progressMessage = e.ProgressCurrentOperation ?? "Processing...: ";
        var progressRecord = new ProgressRecord(_currentActivityId, e.ProgressActivity, progressMessage);
        if (e.ProgressPercentage.HasValue) {
            if (e.ProgressPercentage.Value >= 0 && e.ProgressPercentage.Value <= 100) {
                progressRecord.PercentComplete = e.ProgressPercentage.Value;
            } else {
                progressRecord.PercentComplete = 100;
            }
        } else {
            progressRecord.PercentComplete = 50;
        }
        if (progressRecord.PercentComplete == 100) {
            progressRecord.RecordType = ProgressRecordType.Completed;
            _isCurrentActivityCompleted = true;
        }
        WriteProgress(progressRecord);
    }
    private void Logger_OnInformationMessage(object? sender, LogEventArgs e) {
        WriteInformation(e.Message);
    }

    private void WriteVerbose(string message) {
        _writeVerboseAction?.Invoke(message);
    }

    /// <summary>
    /// Method to write verbose message to PowerShell
    /// </summary>
    /// <param name="message"></param>
    /// <param name="eArgs"></param>
    private void WriteVerbose(string message, object[] eArgs) {
        // Write to PowerShell verbose stream
        var fullMessage = string.Format(message, eArgs);
        _writeVerboseAction?.Invoke(fullMessage);
    }

    private void WriteDebug(string message) {
        // Write to PowerShell debug stream
        _writeDebugAction?.Invoke(message);
    }

    private void WriteInformation(string message) {
        InformationRecord informationRecord = new InformationRecord(message, "PSEventViewer");
        // Write to PowerShell information stream
        _writeInformationAction?.Invoke(informationRecord);
    }

    private void WriteWarning(string message) {
        // Write to PowerShell warning stream
        _writeWarningAction?.Invoke(message);
    }

    //private void WriteError(string message) {
    //    // Write to PowerShell error stream
    //    _writeErrorAction?.Invoke(message);
    //}
    private void WriteError(ErrorRecord errorRecord) {
        // Write to PowerShell error stream
        _writeErrorAction?.Invoke(errorRecord);
    }

    private void WriteProgress(ProgressRecord progressRecord) {
        // Write to PowerShell progress stream
        _writeProgressAction?.Invoke(progressRecord);
    }
}
