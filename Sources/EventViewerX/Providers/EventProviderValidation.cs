namespace EventViewerX.Providers;

/// <summary>Severity of a provider-definition validation issue.</summary>
public enum EventProviderValidationSeverity {
    /// <summary>The definition is valid but deserves operator attention.</summary>
    Warning,
    /// <summary>The definition cannot produce a safe Windows provider.</summary>
    Error
}

/// <summary>One actionable provider-definition validation issue.</summary>
public sealed class EventProviderValidationIssue {
    /// <summary>Issue severity.</summary>
    public EventProviderValidationSeverity Severity { get; internal set; }
    /// <summary>Stable machine-readable issue code.</summary>
    public string Code { get; internal set; } = string.Empty;
    /// <summary>Definition path associated with the issue.</summary>
    public string Path { get; internal set; } = string.Empty;
    /// <summary>Actionable human-readable explanation.</summary>
    public string Message { get; internal set; } = string.Empty;
}

/// <summary>Complete validation result for one provider definition.</summary>
public sealed class EventProviderValidationResult {
    internal EventProviderValidationResult(
        IReadOnlyList<EventProviderValidationIssue> issues) {

        Issues = issues;
    }

    /// <summary>All errors and warnings.</summary>
    public IReadOnlyList<EventProviderValidationIssue> Issues { get; }
    /// <summary>Whether no error-level issues were found.</summary>
    public bool IsValid => Issues.All(static issue =>
        issue.Severity != EventProviderValidationSeverity.Error);
    /// <summary>Error-level issues that prevent a package build.</summary>
    public IReadOnlyList<EventProviderValidationIssue> Errors =>
        Issues.Where(static issue =>
            issue.Severity == EventProviderValidationSeverity.Error)
            .ToArray();
    /// <summary>Non-blocking warnings.</summary>
    public IReadOnlyList<EventProviderValidationIssue> Warnings =>
        Issues.Where(static issue =>
            issue.Severity == EventProviderValidationSeverity.Warning)
            .ToArray();
}

/// <summary>Thrown when a provider definition cannot produce a safe package.</summary>
public sealed class EventProviderValidationException : Exception {
    /// <summary>Creates an exception for a failed validation result.</summary>
    public EventProviderValidationException(
        EventProviderValidationResult result)
        : base(CreateMessage(result)) {

        Result = result;
    }

    /// <summary>Complete structured validation result.</summary>
    public EventProviderValidationResult Result { get; }

    private static string CreateMessage(
        EventProviderValidationResult result) {

        return "The event provider definition is invalid:" +
               Environment.NewLine +
               string.Join(
                   Environment.NewLine,
                   result.Errors.Select(static issue =>
                       $"[{issue.Code}] {issue.Path}: {issue.Message}"));
    }
}
