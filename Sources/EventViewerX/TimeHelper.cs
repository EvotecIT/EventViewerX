using System;

namespace EventViewerX {
    /// <summary>
    /// Commonly used time ranges for event queries.
    /// </summary>
    public enum TimePeriod {
        /// <summary>Previous full hour ending at the top of the current hour.</summary>
        PastHour,
        /// <summary>The current hour from :00 to :59.</summary>
        CurrentHour,
        /// <summary>The previous calendar day (00:00–23:59) relative to today.</summary>
        PastDay,
        /// <summary>The current calendar day (00:00–23:59).</summary>
        CurrentDay,
        /// <summary>The entire previous calendar month.</summary>
        PastMonth,
        /// <summary>The current calendar month.</summary>
        CurrentMonth,
        /// <summary>The previous calendar quarter.</summary>
        PastQuarter,
        /// <summary>The current calendar quarter.</summary>
        CurrentQuarter,
        /// <summary>Last three full days ending now.</summary>
        Last3Days,
        /// <summary>Last seven full days ending now.</summary>
        Last7Days,
        /// <summary>Last fourteen full days ending now.</summary>
        Last14Days,
        /// <summary>Rolling one-hour window ending now.</summary>
        Last1Hour,
        /// <summary>Rolling two-hour window ending now.</summary>
        Last2Hours,
        /// <summary>Rolling three-hour window ending now.</summary>
        Last3Hours,
        /// <summary>Rolling six-hour window ending now.</summary>
        Last6Hours,
        /// <summary>Rolling twelve-hour window ending now.</summary>
        Last12Hours,
        /// <summary>Rolling sixteen-hour window ending now.</summary>
        Last16Hours,
        /// <summary>Rolling twenty-four-hour window ending now.</summary>
        Last24Hours,
        /// <summary>Events that occurred today.</summary>
        Today,
        /// <summary>Events that occurred yesterday.</summary>
        Yesterday,
        /// <summary>No time filtering; return everything.</summary>
        Everything,
        /// <summary>From the start of the most recent Monday to now.</summary>
        TillLastMonday,
        /// <summary>From the start of the most recent Tuesday to now.</summary>
        TillLastTuesday,
        /// <summary>From the start of the most recent Wednesday to now.</summary>
        TillLastWednesday,
        /// <summary>From the start of the most recent Thursday to now.</summary>
        TillLastThursday,
        /// <summary>From the start of the most recent Friday to now.</summary>
        TillLastFriday,
        /// <summary>From the start of the most recent Saturday to now.</summary>
        TillLastSaturday,
        /// <summary>From the start of the most recent Sunday to now.</summary>
        TillLastSunday,
        /// <summary>Rolling fifteen-minute window ending now.</summary>
        Last15Minutes,
        /// <summary>Rolling thirty-minute window ending now.</summary>
        Last30Minutes,
    }

    internal static class TimeHelper {
        /// <summary>
        /// Converts a <see cref="TimePeriod"/> to start and end timestamps.
        /// </summary>
        /// <param name="timePeriod">Time range to convert.</param>
        /// <returns>Tuple describing the time range.</returns>
        internal static (DateTime? StartTime, DateTime? EndTime, TimeSpan? LastPeriod) GetTimePeriod(TimePeriod timePeriod) {
            DateTime now = DateTime.Now;
            DateTime? startTime = null;
            DateTime? endTime = null;
            TimeSpan? lastPeriod = null;

            switch (timePeriod) {
                case TimePeriod.PastHour:
                    startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-1);
                    endTime = startTime.Value.AddHours(1);
                    break;
                case TimePeriod.CurrentHour:
                    startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
                    endTime = startTime.Value.AddHours(1);
                    break;
                case TimePeriod.PastDay:
                    startTime = now.Date.AddDays(-1);
                    endTime = startTime.Value.AddDays(1);
                    break;
                case TimePeriod.CurrentDay:
                    startTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
                    endTime = startTime.Value.AddDays(1);
                    break;
                case TimePeriod.PastMonth:
                    startTime = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    endTime = new DateTime(now.Year, now.Month, 1);
                    break;
                case TimePeriod.CurrentMonth:
                    startTime = new DateTime(now.Year, now.Month, 1);
                    endTime = startTime.Value.AddMonths(1);
                    break;
                case TimePeriod.PastQuarter:
                    int currentQuarter = (now.Month - 1) / 3 + 1;
                    DateTime startOfCurrentQuarter = new DateTime(now.Year, (currentQuarter - 1) * 3 + 1, 1);
                    startTime = startOfCurrentQuarter.AddMonths(-3);
                    endTime = startOfCurrentQuarter;
                    break;
                case TimePeriod.CurrentQuarter:
                    currentQuarter = (now.Month - 1) / 3 + 1;
                    startTime = new DateTime(now.Year, (currentQuarter - 1) * 3 + 1, 1);
                    endTime = startTime.Value.AddMonths(3);
                    break;
                case TimePeriod.Last3Days:
                    startTime = now.Date.AddDays(-3);
                    break;
                case TimePeriod.Last7Days:
                    startTime = now.Date.AddDays(-7);
                    break;
                case TimePeriod.Last14Days:
                    startTime = now.Date.AddDays(-14);
                    break;
                case TimePeriod.Last1Hour:
                    lastPeriod = TimeSpan.FromHours(1);
                    break;
                case TimePeriod.Last2Hours:
                    lastPeriod = TimeSpan.FromHours(2);
                    break;
                case TimePeriod.Last3Hours:
                    lastPeriod = TimeSpan.FromHours(3);
                    break;
                case TimePeriod.Last6Hours:
                    lastPeriod = TimeSpan.FromHours(6);
                    break;
                case TimePeriod.Last12Hours:
                    lastPeriod = TimeSpan.FromHours(12);
                    break;
                case TimePeriod.Last16Hours:
                    lastPeriod = TimeSpan.FromHours(16);
                    break;
                case TimePeriod.Last24Hours:
                    lastPeriod = TimeSpan.FromHours(24);
                    break;
                case TimePeriod.Last15Minutes:
                    lastPeriod = TimeSpan.FromMinutes(15);
                    break;
                case TimePeriod.Last30Minutes:
                    lastPeriod = TimeSpan.FromMinutes(30);
                    break;
                case TimePeriod.Today:
                    startTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
                    endTime = startTime.Value.AddDays(1);
                    break;
                case TimePeriod.Yesterday:
                    startTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddDays(-1);
                    endTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
                    break;
                case TimePeriod.Everything:
                    startTime = null;
                    endTime = null;
                    break;
                case TimePeriod.TillLastMonday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Monday, now);
                    break;
                case TimePeriod.TillLastTuesday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Tuesday, now);
                    break;
                case TimePeriod.TillLastWednesday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Wednesday, now);
                    break;
                case TimePeriod.TillLastThursday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Thursday, now);
                    break;
                case TimePeriod.TillLastFriday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Friday, now);
                    break;
                case TimePeriod.TillLastSaturday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Saturday, now);
                    break;
                case TimePeriod.TillLastSunday:
                    startTime = GetLastDayOfWeek(DayOfWeek.Sunday, now);
                    break;
            }

            return (startTime, endTime, lastPeriod);
        }
        private static DateTime GetLastDayOfWeek(DayOfWeek dayOfWeek, DateTime now) {
            int daysUntilDayOfWeek = dayOfWeek - now.DayOfWeek;
            if (daysUntilDayOfWeek > 0) daysUntilDayOfWeek -= 7;
            return now.AddDays(daysUntilDayOfWeek);
        }

    }

}
