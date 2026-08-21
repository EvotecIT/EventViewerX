namespace EventViewerX;

/// <summary>Resolves reusable relative time selections into absolute query boundaries.</summary>
public static class EventTimeRange {
    /// <summary>
    /// Resolves a relative period while preserving explicitly supplied boundaries.
    /// </summary>
    public static (DateTime? StartTime, DateTime? EndTime) Resolve(
        DateTime? startTime,
        DateTime? endTime,
        TimePeriod? timePeriod) {

        if (!timePeriod.HasValue) {
            return (startTime, endTime);
        }
        if (startTime.HasValue || endTime.HasValue) {
            throw new ArgumentException(
                "TimePeriod cannot be combined with StartTime or EndTime.",
                nameof(timePeriod));
        }
        if (!Enum.IsDefined(
                typeof(TimePeriod),
                timePeriod.Value)) {
            throw new ArgumentOutOfRangeException(
                nameof(timePeriod),
                timePeriod.Value,
                "TimePeriod must be a defined relative period.");
        }

        (DateTime? periodStart, DateTime? periodEnd, TimeSpan? rollingPeriod) =
            TimeHelper.GetTimePeriod(timePeriod.Value);
        if (!periodStart.HasValue && rollingPeriod.HasValue) {
            periodStart = DateTime.Now.Subtract(rollingPeriod.Value);
        }
        return (periodStart, periodEnd);
    }
}
