using System;
using Xunit;

namespace EventViewerX.Tests {
    public class TestTimeHelper {
        [Fact]
        public void PastHourRangeIsLocal() {
            var now = DateTime.Now;
            var expectedStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-1);
            var expectedEnd = expectedStart.AddHours(1);

            var result = TimeHelper.GetTimePeriod(TimePeriod.PastHour);

            Assert.Equal(expectedStart, result.StartTime);
            Assert.Equal(expectedEnd, result.EndTime);
        }

        [Fact]
        public void LastSevenDaysStartsFromLocal() {
            var now = DateTime.Now;
            var expectedStart = now.Date.AddDays(-7);

            var result = TimeHelper.GetTimePeriod(TimePeriod.Last7Days);

            Assert.Equal(expectedStart, result.StartTime);
            Assert.Null(result.EndTime);
        }

        [Fact]
        public void TodayRangeMatchesLocalDay() {
            var now = DateTime.Now;
            var expectedStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
            var expectedEnd = expectedStart.AddDays(1);

            var result = TimeHelper.GetTimePeriod(TimePeriod.Today);

            Assert.Equal(expectedStart, result.StartTime);
            Assert.Equal(expectedEnd, result.EndTime);
        }

        [Theory]
        [InlineData(TimePeriod.Last1Hour, 1)]
        [InlineData(TimePeriod.Last2Hours, 2)]
        [InlineData(TimePeriod.Last3Hours, 3)]
        [InlineData(TimePeriod.Last6Hours, 6)]
        [InlineData(TimePeriod.Last12Hours, 12)]
        [InlineData(TimePeriod.Last16Hours, 16)]
        [InlineData(TimePeriod.Last24Hours, 24)]
        public void EventTimeRangePreservesRollingDurations(
            TimePeriod period,
            int hours) {

            DateTime earliest = DateTime.Now.AddHours(-hours);
            (DateTime? start, DateTime? end) =
                EventTimeRange.Resolve(null, null, period);
            DateTime latest = DateTime.Now.AddHours(-hours);

            Assert.NotNull(start);
            Assert.InRange(start!.Value, earliest, latest);
            Assert.Null(end);
        }

        [Theory]
        [InlineData(TimePeriod.Last15Minutes, 15)]
        [InlineData(TimePeriod.Last30Minutes, 30)]
        public void EventTimeRangePreservesRollingMinuteDurations(
            TimePeriod period,
            int minutes) {

            DateTime earliest = DateTime.Now.AddMinutes(-minutes);
            (DateTime? start, DateTime? end) =
                EventTimeRange.Resolve(null, null, period);
            DateTime latest = DateTime.Now.AddMinutes(-minutes);

            Assert.NotNull(start);
            Assert.InRange(start!.Value, earliest, latest);
            Assert.Null(end);
        }

        [Fact]
        public void EventTimeRangeRejectsUndefinedPeriods() {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EventTimeRange.Resolve(
                    null,
                    null,
                    (TimePeriod)int.MaxValue));
        }
    }
}
