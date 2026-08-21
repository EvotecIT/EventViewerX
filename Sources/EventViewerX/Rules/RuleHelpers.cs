using System;
using System.Globalization;
using System.Collections.Concurrent;
using System.Linq;

namespace EventViewerX.Rules;

internal static class RuleHelpers
{
    /// <summary>
    /// Parses the common <c>NoNameA0</c>/<c>NoNameA1</c> date and time fragments emitted by many OS events into UTC.
    /// </summary>
    /// <param name="e">Source event whose data dictionary contains the unlabeled date/time fields.</param>
    /// <returns>UTC <see cref="DateTime"/> when both fragments are present and parseable; otherwise <c>null</c>.</returns>
    /// <example>
    /// When Directory Service events store date in <c>NoNameA1</c> (e.g., "2025-02-12") and time in <c>NoNameA0</c> (e.g., "08:51:44"),
    /// call <c>ParseUnlabeledOsTimestamp(evt)</c> to normalise to UTC without hand-written concatenation logic.
    /// </example>
    internal static DateTime? ParseUnlabeledOsTimestamp(EventObject e)
    {
        if (e == null) return null;

        var date = e.GetValueFromDataDictionary("NoNameA1");
        var time = e.GetValueFromDataDictionary("NoNameA0");

        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time))
        {
            return null;
        }

        var candidates = new[]
        {
            $"{date} {time}",
            $"{date}T{time}Z"
        };

        foreach (var candidate in candidates)
        {
            if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                return dt.ToUniversalTime();
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to parse a loosely formatted date/time value and returns the result in UTC.
    /// </summary>
    /// <param name="value">String containing a date/time representation; can be any culture-invariant format accepted by <see cref="DateTime.TryParse(string?, IFormatProvider?, DateTimeStyles, out DateTime)"/>.</param>
    /// <returns>UTC <see cref="DateTime"/> when parsed; otherwise <c>null</c>.</returns>
    internal static DateTime? ParseDateTimeLoose(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return dt.ToUniversalTime();
        }
        return null;
    }

    /// <summary>
    /// Backward-compatibility shim for older rule files expecting <c>ParseUnlabeledDateTime</c>.
    /// </summary>
    /// <param name="e">Event containing <c>NoNameA0</c>/<c>NoNameA1</c> fragments.</param>
    /// <returns>UTC timestamp or <c>null</c> if unavailable.</returns>
    internal static DateTime? ParseUnlabeledDateTime(EventObject e) => ParseUnlabeledOsTimestamp(e);

    /// <summary>
    /// Checks if an event originates from any of the specified providers (case-insensitive).
    /// </summary>
    /// <param name="e">Event to inspect.</param>
    /// <param name="providers">One or more provider names to match.</param>
    /// <returns><c>true</c> when the provider matches; otherwise <c>false</c>.</returns>
    internal static bool IsProvider(EventObject e, params string[] providers)
    {
        if (e == null || providers == null) return false;
        foreach (var p in providers)
        {
            if (string.Equals(e.ProviderName, p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if an event belongs to any of the specified channels (case-insensitive).
    /// </summary>
    /// <param name="e">Event to inspect.</param>
    /// <param name="channels">One or more channel names (e.g., <c>Security</c>, <c>System</c>).</param>
    /// <returns><c>true</c> when the channel matches; otherwise <c>false</c>.</returns>
    internal static bool IsChannel(EventObject e, params string[] channels)
    {
        if (e == null || channels == null) return false;
        foreach (var c in channels)
        {
            if (string.Equals(e.OriginalLogName, c, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Reads the first integer value from the event's data dictionary using the provided keys.
    /// </summary>
    /// <param name="e">Event whose data dictionary should be queried.</param>
    /// <param name="keys">Candidate field names to probe in order.</param>
    /// <returns>Parsed integer when found; otherwise <c>null</c>.</returns>
    internal static int? GetInt(EventObject e, params string[] keys)
    {
        if (e == null || keys == null) return null;
        foreach (var k in keys)
        {
            var v = e.GetValueFromDataDictionary(k);
            if (!string.IsNullOrWhiteSpace(v) && int.TryParse(v, out var i)) return i;
        }
        return null;
    }

    /// <summary>
    /// Normalizes IPv6-mapped and loopback addresses to friendlier IPv4/localhost strings.
    /// </summary>
    internal static string NormalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return string.Empty;
        var trimmedIp = ip!.Trim();
        if (trimmedIp.Equals("::1", StringComparison.OrdinalIgnoreCase)) return "127.0.0.1";
        if (trimmedIp.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase)) return trimmedIp.Substring("::ffff:".Length);
        return trimmedIp;
    }

    // Cache to avoid repeated reflection when describing flags.
    private static readonly ConcurrentDictionary<Type, EnumFlag[]> _enumFlagCache = new();

    private sealed class EnumFlag
    {
        public EnumFlag(Enum value, ulong raw, string name)
        {
            Value = value;
            Raw = raw;
            Name = name;
        }

        public Enum Value { get; }
        public ulong Raw { get; }
        public string Name { get; }
    }

    /// <summary>
    /// Returns a comma-separated list of flagged enum names, falling back to the raw value when no flags are set.
    /// </summary>
    internal static string DescribeFlags<TEnum>(TEnum? value) where TEnum : struct, Enum
    {
        if (!value.HasValue) return string.Empty;

        var enumType = typeof(TEnum);
        var flags = _enumFlagCache.GetOrAdd(enumType, static t =>
            Enum.GetValues(t)
                .Cast<Enum>()
                .Select(ev => new EnumFlag(ev, Convert.ToUInt64(ev), ev.ToString()))
                .ToArray());

        var rawValue = Convert.ToUInt64(value.Value);
        var names = flags
            .Where(f => f.Raw != 0 && (rawValue & f.Raw) == f.Raw)
            .Select(f => f.Name)
            .ToList();

        return names.Count == 0 ? value.Value.ToString() : string.Join(", ", names);
    }

    /// <summary>
    /// Returns the best-available message text for an event, preferring the richest content among subject, rendered message,
    /// unlabeled data (<c>NoNameA0</c>), and <c>#text</c> payloads. Useful when remote queries omit the rendered message.
    /// </summary>
    /// <param name="e">Event whose message text should be resolved.</param>
    /// <returns>Non-null string containing the longest available message fragment.</returns>
    /// <example>
    /// Remote Security log queries sometimes return an empty <c>Message</c>; calling <c>GetMessage(evt)</c> will fall back to
    /// <c>NoNameA0</c> or <c>#text</c> so parsers can still display a meaningful description.
    /// </example>
    internal static string GetMessage(EventObject e)
    {
        if (e == null) return string.Empty;

        var subject = e.MessageSubject;
        var rendered = e.Message;
        var data = e.GetValueFromDataDictionary("NoNameA0");
        var text = e.GetValueFromDataDictionary("#text");

        // Prefer the longest non-empty payload so we keep rich details when rendered message is truncated.
        string best = string.Empty;
        if (!string.IsNullOrWhiteSpace(subject)) best = subject;
        if (!string.IsNullOrWhiteSpace(rendered) && rendered.Length > best.Length) best = rendered;
        if (!string.IsNullOrWhiteSpace(data) && data.Length > best.Length) best = data;
        if (!string.IsNullOrWhiteSpace(text) && text.Length > best.Length) best = text;

        return best ?? string.Empty;
    }
}
