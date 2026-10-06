using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventWatcherDelivery {
    [Fact]
    public void ConcurrentAdmissionNeverExceedsLifetimeLimit() {
        var delivery = new EventWatcherDelivery(100, 7);
        var tickets = new System.Collections.Concurrent.ConcurrentBag<long>();
        int stops = 0;
        delivery.StopRequested += (_, _) => Interlocked.Increment(ref stops);
        Parallel.For(0, 1000, _ => {
            long ticket = delivery.TryAccept();
            if (ticket != 0) { tickets.Add(ticket); }
        });
        Assert.Equal(7, tickets.Count);
        Assert.Equal(1, stops);
        Assert.False(delivery.DrainCompletion.IsCompleted);
        foreach (long ticket in tickets) { delivery.Begin(ticket); delivery.Acknowledge(ticket, true); }
        Assert.True(delivery.DrainCompletion.IsCompletedSuccessfully);
        Assert.Equal(7, delivery.GetHealth().Acknowledged);
    }

    [Fact]
    public void OverloadStopsCollectionButRetainsAcceptedActionsAndFailureEvidence() {
        var delivery = new EventWatcherDelivery(2);
        long first = delivery.TryAccept(DateTime.UtcNow.AddSeconds(-10));
        long second = delivery.TryAccept();
        delivery.Begin(first);
        Assert.Equal(0, delivery.TryAccept());
        EventWatcherHealth health = delivery.GetHealth();
        Assert.True(health.IsOverloaded);
        Assert.True(health.IsStopping);
        Assert.Equal(1, health.Queued);
        Assert.Equal(1, health.Running);
        Assert.NotNull(health.OldestPendingAge);
        Assert.True(health.SourceLag >= TimeSpan.FromSeconds(10));
        delivery.Acknowledge(first, false);
        delivery.Acknowledge(first, true);
        Assert.False(delivery.DrainCompletion.IsCompleted);
        delivery.Acknowledge(second, true);
        health = delivery.GetHealth();
        Assert.Equal(1, health.Failed);
        Assert.Equal(1, health.Completed);
        Assert.Equal(2, health.Acknowledged);
        Assert.True(health.IsDrained);
        Assert.True(delivery.DrainCompletion.IsCompletedSuccessfully);
    }
}
