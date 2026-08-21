namespace EventViewerX;
/// <summary>
/// Internal logger that allows to write to console, error or wherever else is needed
/// </summary>
public class InternalLogger {
    private readonly object _lock = new object();

    /// <summary>
    /// Define Verbose message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnVerboseMessage;

    /// <summary>
    /// Define Warning message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnWarningMessage;

    /// <summary>
    /// Define Error message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnErrorMessage;

    /// <summary>
    /// Define Debug message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnDebugMessage;

    /// <summary>
    /// Define Progress message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnProgressMessage;

    /// <summary>
    /// Define Information message event
    /// </summary>
    public event EventHandler<LogEventArgs>? OnInformationMessage;

    /// <summary>
    /// If true, will write verbose messages to console
    /// </summary>
    public bool IsVerbose { get; set; }

    /// <summary>
    /// If true, will write error messages to console
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// If true, will write warning messages to console
    /// </summary>
    public bool IsWarning { get; set; }

    /// <summary>
    /// If true, will write debug messages to console
    /// </summary>
    public bool IsDebug { get; set; }

    /// <summary>
    /// If true, will write information messages to console
    /// </summary>
    public bool IsInformation { get; set; }

    /// <summary>
    /// if true, will write progress messages to console
    /// </summary>
    public bool IsProgress { get; set; }

    /// <summary>
    /// Initialize logger
    /// </summary>
    /// <param name="isVerbose"></param>
    public InternalLogger(bool isVerbose = false) {
        IsVerbose = isVerbose;
    }

    /// <summary>
    /// Raises a progress update and optionally writes it to the console.
    /// </summary>
    /// <param name="activity">High-level activity name.</param>
    /// <param name="currentOperation">Current operation within the activity.</param>
    /// <param name="percentCompleted">Completion percentage (0-100).</param>
    /// <param name="currentSteps">Current step index when a step count is known.</param>
    /// <param name="totalSteps">Total number of steps when known.</param>
    public void WriteProgress(string activity, string currentOperation, int percentCompleted, int? currentSteps = null, int? totalSteps = null) {
        lock (_lock) {
            OnProgressMessage?.Invoke(this, new LogEventArgs(activity, currentOperation, currentSteps, totalSteps, totalSteps));
            if (IsProgress) {
                if (currentSteps.HasValue && totalSteps.HasValue) {
                    Console.WriteLine("[progress] activity: {0} / operation: {1} / percent completed: {2}% ({3} out of {4})", activity, currentOperation, percentCompleted, currentSteps, totalSteps);
                } else {
                    Console.WriteLine("[progress] activity: {0} / operation: {1} / percent completed: {2}%", activity, currentOperation, percentCompleted);
                }
            }
        }
    }

    /// <summary>Logs an error message and raises the error event.</summary>
    /// <param name="message">Error text to emit.</param>
    public void WriteError(string message) {
        lock (_lock) {
            OnErrorMessage?.Invoke(this, new LogEventArgs(message));
            if (IsError) {
                Console.WriteLine("[error] " + message);
            }
        }
    }

    /// <summary>Formats and logs an error message with arguments.</summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteError(string message, params object[] args) {
        lock (_lock) {
            OnErrorMessage?.Invoke(this, new LogEventArgs(string.Format(message, args)));
            if (IsError) {
                Console.WriteLine("[error] " + message, args);
            }
        }
    }

    /// <summary>Logs a warning message.</summary>
    /// <param name="message">Warning text to emit.</param>
    public void WriteWarning(string message) {
        lock (_lock) {
            OnWarningMessage?.Invoke(this, new LogEventArgs(message));
            if (IsWarning) {
                Console.WriteLine("[warning] " + message);
            }
        }
    }

    /// <summary>Formats and logs a warning message with arguments.</summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteWarning(string message, params object[] args) {
        lock (_lock) {
            OnWarningMessage?.Invoke(this, new LogEventArgs(string.Format(message, args)));
            if (IsWarning) {
                Console.WriteLine("[warning] " + message, args);
            }
        }
    }

    /// <summary>Emits a verbose message.</summary>
    /// <param name="message">Verbose text to write.</param>
    public void WriteVerbose(string message) {
        lock (_lock) {
            OnVerboseMessage?.Invoke(this, new LogEventArgs(message));
            if (IsVerbose) {
                Console.WriteLine(message);
            }
        }
    }

    /// <summary>Formats and emits a verbose message.</summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteVerbose(string message, params object[] args) {
        lock (_lock) {
            OnVerboseMessage?.Invoke(this, new LogEventArgs(message, args));
            if (IsVerbose) {
                Console.WriteLine(message, args);
            }
        }
    }

    /// <summary>Formats and emits a debug message.</summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteDebug(string message, params object[] args) {
        lock (_lock) {
            OnDebugMessage?.Invoke(this, new LogEventArgs(message, args));
            if (IsDebug) {
                Console.WriteLine("[debug] " + message, args);
            }
        }
    }

    /// <summary>Formats and emits an informational message.</summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteInformation(string message, params object[] args) {
        lock (_lock) {
            OnInformationMessage?.Invoke(this, new LogEventArgs(message, args));
            if (IsInformation) {
                Console.WriteLine("[information] " + message, args);
            }
        }
    }
}

/// <summary>
/// Event arguments used by <see cref="InternalLogger"/> events.
/// </summary>
public class LogEventArgs : EventArgs {
    /// <summary>
    /// Progress percentage
    /// </summary>
    public int? ProgressPercentage { get; set; }

    /// <summary>
    /// Progress total steps
    /// </summary>
    public int? ProgressTotalSteps { get; set; }

    /// <summary>
    /// Progress current steps
    /// </summary>
    public int? ProgressCurrentSteps { get; set; }

    /// <summary>
    /// Progress current operation
    /// </summary>
    public string? ProgressCurrentOperation { get; set; }

    /// <summary>
    /// Progress activity
    /// </summary>
    public string? ProgressActivity { get; set; }

    /// <summary>
    /// Message to be written including arguments substitution
    /// </summary>
    public string FullMessage { get; set; } = string.Empty;

    /// <summary>
    /// Message to be written
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Original format arguments supplied to the logger.</summary>
    public object[] Args { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Creates event args for formatted log messages.
    /// </summary>
    /// <param name="message">Composite format string.</param>
    /// <param name="args">Arguments used to format the message.</param>
    public LogEventArgs(string message, object[] args) {
        Message = message;
        Args = args;
        FullMessage = string.Format(message, args);
    }

    /// <summary>
    /// Creates event args for a simple message without formatting.
    /// </summary>
    /// <param name="message">Message to expose to subscribers.</param>
    public LogEventArgs(string message) {
        Message = message;
        FullMessage = message;
    }

    /// <summary>
    /// Creates progress-oriented event args for long-running operations.
    /// </summary>
    /// <param name="activity">High-level activity label.</param>
    /// <param name="currentOperation">Current operation name.</param>
    /// <param name="currentSteps">Completed steps so far.</param>
    /// <param name="totalSteps">Total steps expected.</param>
    /// <param name="percentage">Optional completion percentage.</param>
    public LogEventArgs(string activity, string currentOperation, int? currentSteps, int? totalSteps, int? percentage) {
        ProgressActivity = activity;
        ProgressCurrentOperation = currentOperation;
        ProgressCurrentSteps = currentSteps;
        ProgressTotalSteps = totalSteps;
        ProgressPercentage = percentage;
    }
}
