using System;
using Xunit;

namespace EventViewerX.Tests;

public class TestSubscriptionsXml
{
    [Fact]
    public void CollectorSubscriptionXml_TryNormalize_ExtractsDescriptionAndQueries()
    {
        const string xml = """
            <Subscription>
              <Description>  Forwarded security events  </Description>
              <Query>
                <Select Path="Security">*[System[EventID=4624]]</Select>
                <Select Path="Application">*[System[Level=2]]</Select>
              </Query>
            </Subscription>
            """;

        var success = CollectorSubscriptionXml.TryNormalize(xml, out var details, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(details);
        Assert.Equal("Forwarded security events", details!.Description);
        Assert.Equal(new[] { "*[System[EventID=4624]]", "*[System[Level=2]]" }, details.Queries);
        Assert.DoesNotContain("\r", details.NormalizedXml, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", details.NormalizedXml, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectorSubscriptionXml_AreEquivalent_IgnoresFormattingDifferences()
    {
        const string left = "<Subscription><Description>Demo</Description><Query><Select Path=\"Security\">*[System[EventID=1]]</Select></Query></Subscription>";
        const string right = """
            <Subscription>
              <Description>Demo</Description>
              <Query>
                <Select Path="Security">*[System[EventID=1]]</Select>
              </Query>
            </Subscription>
            """;

        Assert.True(CollectorSubscriptionXml.AreEquivalent(left, right));
    }

    [Fact]
    public void SetCollectorSubscriptionEnabled_DoesNotCreateMissingSubscription()
    {
        if (!OperatingSystem.IsWindows()) return;

        string name = $"EventViewerX-Missing-{Guid.NewGuid():N}";

        Assert.Throws<FileNotFoundException>(() =>
            CollectorSubscriptionManager
                .SetCollectorSubscriptionEnabled(
                    name,
                    enabled: true));
    }
}
