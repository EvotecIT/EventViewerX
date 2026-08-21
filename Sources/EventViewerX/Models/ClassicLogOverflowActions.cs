namespace EventViewerX;

using System.Diagnostics;

/// <summary>
/// Canonical string names for classic Event Log overflow behavior.
/// </summary>
public static class ClassicLogOverflowActions {
    /// <summary>Supported overflow action names exposed to callers.</summary>
    public static readonly string[] Names = {
        "overwrite_as_needed",
        "overwrite_older",
        "do_not_overwrite"
    };

    /// <summary>
    /// Tries to normalize a caller-supplied overflow action name to the shared contract.
    /// </summary>
    public static bool TryNormalize(string? value, out string? normalized, out string? error) {
        normalized = null;
        error = null;

        if (value == null) {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0) {
            return true;
        }

        var candidate = trimmed.ToLowerInvariant();
        switch (candidate) {
            case "overwrite_as_needed":
                normalized = "overwrite_as_needed";
                return true;
            case "overwrite_older":
                normalized = "overwrite_older";
                return true;
            case "do_not_overwrite":
                normalized = "do_not_overwrite";
                return true;
            default:
                error = $"overflow_action must be one of: {string.Join(", ", Names)}.";
                return false;
        }
    }

    /// <summary>
    /// Tries to parse a normalized overflow action name into <see cref="OverflowAction"/>.
    /// </summary>
    public static bool TryParse(string? value, out OverflowAction overflowAction) {
        overflowAction = OverflowAction.OverwriteAsNeeded;
        if (!TryNormalize(value, out var normalized, out _)) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalized)) {
            return true;
        }

        switch (normalized) {
            case "overwrite_as_needed":
                overflowAction = OverflowAction.OverwriteAsNeeded;
                return true;
            case "overwrite_older":
                overflowAction = OverflowAction.OverwriteOlder;
                return true;
            case "do_not_overwrite":
                overflowAction = OverflowAction.DoNotOverwrite;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Normalizes an <see cref="OverflowAction"/> into the shared string contract.
    /// </summary>
    public static string Normalize(OverflowAction overflowAction) {
        return overflowAction switch {
            OverflowAction.OverwriteAsNeeded => "overwrite_as_needed",
            OverflowAction.OverwriteOlder => "overwrite_older",
            OverflowAction.DoNotOverwrite => "do_not_overwrite",
            _ => overflowAction.ToString().ToLowerInvariant()
        };
    }
}
