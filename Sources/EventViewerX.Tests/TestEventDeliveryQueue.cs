using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventDeliveryQueue {
    [Fact]
    public async Task AsyncProducerAppliesBackpressureAndDrainsInOrder() {
        var processed = new List<int>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new EventDeliveryQueue<int>(2, async (value, _) => {
            if (value == 1) {
                await release.Task.ConfigureAwait(false);
            }
            processed.Add(value);
        });

        await queue.WriteAsync(1);
        await queue.WriteAsync(2);
        await queue.WriteAsync(3);
        ValueTask fourth = queue.WriteAsync(4);
        Assert.False(fourth.IsCompleted);

        release.SetResult(true);
        await fourth;
        queue.Complete();
        await queue.Completion;

        Assert.Equal(new[] { 1, 2, 3, 4 }, processed);
        EventDeliveryQueueSnapshot snapshot = queue.GetSnapshot();
        Assert.Equal(4, snapshot.Accepted);
        Assert.Equal(4, snapshot.Completed);
        Assert.Equal(0, snapshot.Depth);
        Assert.InRange(snapshot.HighWatermark, 2, 3);
        Assert.Null(snapshot.Failure);
    }

    [Fact]
    public async Task SynchronousProducerFailsExplicitlyAtCapacity() {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var queue = new EventDeliveryQueue<int>(1, async (_, _) => {
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
        });

        Assert.True(queue.TryWrite(1));
        await entered.Task;
        Assert.True(queue.TryWrite(2));
        Assert.False(queue.TryWrite(3));
        EventDeliveryQueueSnapshot pending = queue.GetSnapshot();
        Assert.Equal(2, pending.Pending);
        Assert.NotNull(pending.OldestPendingUtc);
        Assert.True(pending.OldestPendingAge >= TimeSpan.Zero);

        release.SetResult(true);
        queue.Complete();
        await queue.Completion;
        EventDeliveryQueueSnapshot completed = queue.GetSnapshot();
        Assert.Equal(2, completed.Completed);
        Assert.Null(completed.OldestPendingUtc);
        Assert.Equal(TimeSpan.Zero, completed.OldestPendingAge);
    }

    [Fact]
    public async Task ProcessorFailureStopsDeliveryAndSurfacesThroughCompletion() {
        var failure = new InvalidOperationException("delivery failed");
        var queue = new EventDeliveryQueue<int>(2, (_, _) => ValueTask.FromException(failure));
        Assert.True(queue.TryWrite(1));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await queue.Completion);

        Assert.Same(failure, thrown);
        Assert.Same(failure, queue.GetSnapshot().Failure);
        Assert.False(queue.TryWrite(2));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await queue.DisposeAsync());
    }

    [Fact]
    public async Task DisposalCancelsATokenAwareProcessorBeforeAwaitingTheWorker() {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new EventDeliveryQueue<int>(1, async (_, cancellationToken) => {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        });
        Assert.True(queue.TryWrite(1));
        await entered.Task;

        Task disposal = queue.DisposeAsync().AsTask();
        Task completed = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(disposal, completed);
        await disposal;
        Assert.Null(queue.GetSnapshot().Failure);
    }
}
