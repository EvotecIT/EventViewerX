using Xunit;

namespace EventViewerX.Tests;

public sealed class TestWatcherPartialStartup {
    [Fact]
    public void ToleratedMissingChannelDoesNotDeadlockWatcherStartup() {
        if (!OperatingSystem.IsWindows()) { return; }
        const string query = """
            <QueryList>
              <Query Id="0" Path="System"><Select Path="System">*</Select></Query>
              <Query Id="1" Path="EVX-Nonexistent-Startup-Test"><Select Path="EVX-Nonexistent-Startup-Test">*</Select></Query>
            </QueryList>
            """;
        using var watcher = new WatchEvents();
        int failures = 0;
        watcher.SubscriptionFailed += (_, failure) => {
            Assert.False(failure.Terminal);
            Interlocked.Increment(ref failures);
        };
        watcher.Watch(new EventLogSubscriptionQuery("System") {
            XPath = query, Start = EventLogSubscriptionStart.Future,
            ReadMode = EventReadMode.Metadata, TolerateQueryErrors = true,
            RemoteConnectionTimeoutMilliseconds = 2000
        }, _ => { });
        Assert.Equal(1, failures);
        Assert.NotNull(watcher.LastFailure);
    }
}
