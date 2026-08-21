namespace EventViewerX;

using System;
using System.Diagnostics.Eventing.Reader;

/// <summary>
/// Canonical string names for Event Log channel retention modes.
/// </summary>
public static class ChannelPolicyModeNames {
    /// <summary>Supported mode names exposed to callers.</summary>
    public static readonly string[] Names = {
        "circular",
        "retain",
        "auto_backup"
    };

    /// <summary>
    /// Tries to parse a canonical mode name into the corresponding <see cref="EventLogMode"/>.
    /// </summary>
    public static bool TryParse(string? value, out EventLogMode? mode, out string? error) {
        mode = null;
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
            case "circular":
                mode = EventLogMode.Circular;
                return true;
            case "retain":
                mode = EventLogMode.Retain;
                return true;
            case "auto_backup":
                mode = EventLogMode.AutoBackup;
                return true;
            default:
                error = $"mode must be one of: {string.Join(", ", Names)}.";
                return false;
        }
    }

    /// <summary>
    /// Normalizes an <see cref="EventLogMode"/> into the shared string contract.
    /// </summary>
    public static string? Normalize(EventLogMode? mode) {
        if (!mode.HasValue) {
            return null;
        }

        return mode.Value switch {
            EventLogMode.Circular => "circular",
            EventLogMode.Retain => "retain",
            EventLogMode.AutoBackup => "auto_backup",
            _ => mode.Value.ToString().ToLowerInvariant()
        };
    }
}
