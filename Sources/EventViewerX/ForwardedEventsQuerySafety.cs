using System.Xml.Linq;

namespace EventViewerX;

/// <summary>
/// Keeps filtered XPath out of ForwardedEvents native queries. Affected Windows
/// Server 2025 builds can terminate the Event Log service while evaluating any
/// filtered selector on this channel, so EventViewerX opens the native channel
/// with "*" and applies the complete typed filter while streaming in native
/// order.
/// </summary>
internal static class ForwardedEventsQuerySafety {
    internal const string ChannelName = "ForwardedEvents";

    internal static void Apply(
        EventLogChannelQuery query,
        DateTime? startTime,
        DateTime? endTime) {

        query.ManagedStartTimeUtc = ToUtc(startTime);
        query.ManagedEndTimeUtc = ToUtc(endTime);
    }

    internal static void EnsureNativeChannelQueryIsSafe(
        string logName,
        string xpath) {

        if (string.Equals(
                logName?.Trim(),
                ChannelName,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                xpath.Trim(),
                "*",
                StringComparison.Ordinal)) {
            throw new ArgumentException(
                "Filtered native XPath is unsafe for ForwardedEvents on Windows Server 2025. Use EventLogQueryFactory, EventQueryPlanner, Get-EVXEvent, or a typed definition so EventViewerX can apply the filter in its ordered streaming compatibility path.",
                nameof(xpath));
        }
    }

    internal static void EnsureNativeStructuredQueryIsSafe(
        IEnumerable<XElement> queryElements) {

        foreach (XElement query in queryElements) {
            foreach (XElement selector in query
                         .Descendants()
                         .Where(static element =>
                             string.Equals(element.Name.LocalName, "Select", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(element.Name.LocalName, "Suppress", StringComparison.OrdinalIgnoreCase))) {
                string path = selector.Attribute("Path")?.Value ??
                              query.Attribute("Path")?.Value ??
                              string.Empty;
                if (string.Equals(
                        path.Trim(),
                        ChannelName,
                        StringComparison.OrdinalIgnoreCase)) {
                    throw new ArgumentException(
                        "Structured QueryList execution is unsafe for ForwardedEvents on Windows Server 2025. Use EventLogQueryFactory, EventQueryPlanner, Get-EVXEvent, or an EventLogChannelQuery so EventViewerX keeps this channel on the native single-channel path.",
                        nameof(queryElements));
                }
                EnsureNativeChannelQueryIsSafe(path, selector.Value);
            }
        }
    }

    internal static bool ShouldInclude(
        EventObject eventObject,
        DateTime? startTimeUtc,
        DateTime? endTimeUtc) {

        if (eventObject.TimeCreated == DateTime.MinValue) {
            return false;
        }
        DateTime timeUtc = eventObject.TimeCreated.ToUniversalTime();
        return (!startTimeUtc.HasValue || timeUtc >= startTimeUtc.Value) &&
               (!endTimeUtc.HasValue || timeUtc <= endTimeUtc.Value);
    }

    internal static bool HasCrossedWindow(
        EventObject eventObject,
        bool oldest,
        DateTime? startTimeUtc,
        DateTime? endTimeUtc) {

        if (eventObject.TimeCreated == DateTime.MinValue) {
            return false;
        }
        DateTime timeUtc = eventObject.TimeCreated.ToUniversalTime();
        return oldest
            ? endTimeUtc.HasValue && timeUtc > endTimeUtc.Value
            : startTimeUtc.HasValue && timeUtc < startTimeUtc.Value;
    }

    private static DateTime? ToUtc(DateTime? value) {
        return value?.ToUniversalTime();
    }

    internal static void ValidateTimeWindow(
        DateTime? startTime,
        DateTime? endTime) {

        if (startTime.HasValue &&
            endTime.HasValue &&
            startTime.Value > endTime.Value) {
            throw new ArgumentException(
                "StartTime must be less than or equal to EndTime.",
                nameof(startTime));
        }
    }
}
