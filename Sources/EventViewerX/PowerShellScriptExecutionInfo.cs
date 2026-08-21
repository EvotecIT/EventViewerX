namespace EventViewerX;

/// <summary>
/// Represents details of a PowerShell engine start event.
/// </summary>
public class PowerShellScriptExecutionInfo {
    private static int _executionCount;

    /// <summary>
    /// Gets the sequential index of this execution.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Resets internal state used to track executions.
    /// </summary>
    public static void ResetState() {
        Interlocked.Exchange(ref _executionCount, 0);
    }

    /// <summary>
    /// Gets the managed event snapshot containing script execution details.
    /// </summary>
    public EventObject Event { get; }

    /// <summary>
    /// Gets parsed data values extracted from the event.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Data { get; }

    internal PowerShellScriptExecutionInfo(EventObject eventObject, IDictionary<string, string?> data) {
        Event = eventObject ?? throw new ArgumentNullException(nameof(eventObject));
        Data = new Dictionary<string, string?>(data ?? throw new ArgumentNullException(nameof(data)));
        Index = Interlocked.Increment(ref _executionCount);
    }
}
