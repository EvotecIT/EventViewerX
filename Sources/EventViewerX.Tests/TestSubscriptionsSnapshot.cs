using Xunit;

namespace EventViewerX.Tests;

public class TestSubscriptionsSnapshot
{
    [Fact]
    public void CollectorSubscriptionSnapshot_FromSubscriptionInfo_NormalizesFields()
    {
        var info = new SubscriptionInfo {
            Name = " ForwardedEvents ",
            MachineName = " collector01.contoso.com ",
            Description = "  Security feed  ",
            Enabled = true,
            ContentFormat = " RenderedText ",
            DeliveryMode = " Push ",
            RawXml = " <Subscription /> ",
            Queries = new[] { " *[System[EventID=1]] ", string.Empty, "  " }
        };

        var snapshot = CollectorSubscriptionSnapshot.FromSubscriptionInfo(info, "fallback-host");

        Assert.Equal(" ForwardedEvents ", snapshot.SubscriptionName);
        Assert.Equal("collector01.contoso.com", snapshot.MachineName);
        Assert.Equal("Security feed", snapshot.Description);
        Assert.Equal("RenderedText", snapshot.ContentFormat);
        Assert.Equal("Push", snapshot.DeliveryMode);
        Assert.Equal("<Subscription />", snapshot.RawXml);
        Assert.True(snapshot.HasXml);
        Assert.Equal(1, snapshot.QueryCount);
        Assert.Equal(new[] { "*[System[EventID=1]]" }, snapshot.Queries);
    }
}
