using System;
using System.Globalization;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Formatting helpers for security event fields.
/// </summary>
internal static class SecurityEventText {
    /// <summary>
    /// Normalizes empty/"-" placeholder values to empty string and trims non-empty values.
    /// </summary>
    public static string NormalizePlaceholder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        var v = value!.Trim();
        if (string.Equals(v, "-", StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }
        return v;
    }

    /// <summary>
    /// Splits an account string of the form <c>DOMAIN\user</c> into (domain,user).
    /// </summary>
    public static (string Domain, string User) SplitAccount(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return (string.Empty, string.Empty);
        }
        var v = value!.Trim();
        var idx = v.IndexOf('\\');
        if (idx <= 0 || idx == v.Length - 1) {
            return (string.Empty, v);
        }
        return (v.Substring(0, idx), v.Substring(idx + 1));
    }

    /// <summary>
    /// Formats a logon type enum to its numeric string value.
    /// </summary>
    public static string FormatLogonType(LogonType? value) {
        if (!value.HasValue) {
            return string.Empty;
        }
        return ((int)value.Value).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a status code enum to a hex string (<c>0xXXXXXXXX</c>).
    /// </summary>
    public static string FormatStatusCode(StatusCode? value) {
        if (!value.HasValue) {
            return string.Empty;
        }
        return "0x" + ((uint)value.Value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a sub status code enum to a hex string (<c>0xXXXXXXXX</c>).
    /// </summary>
    public static string FormatSubStatusCode(SubStatusCode? value) {
        if (!value.HasValue) {
            return string.Empty;
        }
        return "0x" + ((uint)value.Value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a failure reason enum to its name.
    /// </summary>
    public static string FormatFailureReason(FailureReason? value) {
        return value.HasValue ? value.Value.ToString() : string.Empty;
    }
}
