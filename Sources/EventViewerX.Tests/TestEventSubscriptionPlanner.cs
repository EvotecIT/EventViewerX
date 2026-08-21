using EventViewerX;
using Xunit;

namespace EventViewerX.Tests;

public class TestEventSubscriptionPlanner {
    [Fact]
    public void CompilesTypedFilter() {
        EventLogSubscriptionQuery query = Assert.Single(
            EventSubscriptionPlanner.CreateQueries(
                new EventSubscriptionDefinition {
                    LogName = "System",
                    Filter = new EventFilter {
                        EventIds = new[] { 41, 6008 }
                    }
                }));

        Assert.Equal("System", query.LogName);
        Assert.Contains("EventID=41", query.XPath, StringComparison.Ordinal);
        Assert.Equal(EventLogSubscriptionStart.Future, query.Start);
    }

    [Fact]
    public void RejectsBookmarkWithoutAfterBookmarkStart() {
        var definition = new EventSubscriptionDefinition {
            LogName = "System",
            BookmarkXml = "<BookmarkList />"
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventSubscriptionPlanner.CreateQueries(definition));

        Assert.Contains("requires Start=AfterBookmark", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsCredentialForLocalSubscription() {
        var definition = new EventSubscriptionDefinition {
            LogName = "System",
            Credential = new System.Net.NetworkCredential("user", "password")
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EventSubscriptionPlanner.CreateQueries(definition));

        Assert.Contains("remote MachineName", exception.Message, StringComparison.Ordinal);
    }
}
