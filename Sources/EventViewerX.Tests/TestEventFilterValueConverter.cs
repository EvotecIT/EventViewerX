using System.Globalization;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventFilterValueConverter {
    [Fact]
    public void CultureSensitiveScalarsUseStableFilterText() {
        CultureInfo originalCulture =
            CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo("pl-PL");
            string polishDecimal =
                EventFilterValueConverter.ToInvariantString(
                    1234.5m);

            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo("en-US");
            string englishDecimal =
                EventFilterValueConverter.ToInvariantString(
                    1234.5m);

            Assert.Equal("1234.5", polishDecimal);
            Assert.Equal(polishDecimal, englishDecimal);
        } finally {
            CultureInfo.CurrentCulture =
                originalCulture;
        }
    }

    [Fact]
    public void DateValuesUseTheCheckpointRepresentation() {
        var value = new DateTimeOffset(
            2026,
            7,
            26,
            8,
            15,
            30,
            TimeSpan.FromHours(2));

        Assert.Equal(
            "2026-07-26T06:15:30.0000000+00:00",
            EventFilterValueConverter.ToInvariantString(value));
    }
}
