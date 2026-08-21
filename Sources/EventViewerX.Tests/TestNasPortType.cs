using Xunit;

namespace EventViewerX.Tests;

public class TestNasPortType {
    [Fact]
    public void ParseNumericValue() {
        var result = EventsHelper.GetNasPortType("15");
        Assert.Equal(NasPortType.Ethernet, result);
    }

    [Fact]
    public void ParseStringValue() {
        var result = EventsHelper.GetNasPortType("WirelessIEEE80211");
        Assert.Equal(NasPortType.WirelessIEEE80211, result);
    }
}
