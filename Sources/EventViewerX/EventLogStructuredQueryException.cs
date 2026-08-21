namespace EventViewerX;

/// <summary>
/// Raised when a tolerant structured query reports path failures without a failure handler.
/// </summary>
public sealed class EventLogStructuredQueryException : Exception {
    internal EventLogStructuredQueryException(
        IReadOnlyList<EventLogQueryFailure> failures)
        : base(CreateMessage(failures)) {

        Failures = failures ??
            throw new ArgumentNullException(nameof(failures));
    }

    /// <summary>Path-specific failures reported by the Windows Event Log query.</summary>
    public IReadOnlyList<EventLogQueryFailure> Failures { get; }

    private static string CreateMessage(
        IReadOnlyList<EventLogQueryFailure> failures) {

        if (failures == null) {
            throw new ArgumentNullException(nameof(failures));
        }
        if (failures.Count == 0) {
            return "The structured Windows Event Log query failed.";
        }

        string details = string.Join(
            "; ",
            failures.Select(failure =>
                $"{failure.Source}: {failure.Exception.Message}"));
        return $"The structured Windows Event Log query returned incomplete results. {details}";
    }
}
