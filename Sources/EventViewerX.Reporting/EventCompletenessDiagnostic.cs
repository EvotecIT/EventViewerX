namespace EventViewerX.Reporting;

internal static class EventCompletenessDiagnostic {
    internal static string? Compose(params string?[] diagnostics) {
        string[] values = diagnostics
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim().TrimEnd('.'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return values.Length == 0 ? null : string.Join(". ", values) + ".";
    }
}
