using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace EventViewerX.Tests {
    public class TestWatchEvents {
        private static HashSet<int> GetIds(WatchEvents watcher) {
            FieldInfo? field = typeof(WatchEvents).GetField("_watchEventIds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (HashSet<int>)field!.GetValue(watcher)!;
        }

        [Fact]
        public void DisposeClearsWatchEventIds() {
            using var watcher = new WatchEvents();
            watcher.Watch(Environment.MachineName, "Application", new List<int> { 1 });
            watcher.Dispose();
            Assert.Empty(GetIds(watcher));
        }

        [Fact]
        public void SubsequentWatchesUseNewIdsOnly() {
            using var watcher = new WatchEvents();
            watcher.Watch(Environment.MachineName, "Application", new List<int> { 1 });
            watcher.Watch(Environment.MachineName, "Application", new List<int> { 2 });
            HashSet<int> ids = GetIds(watcher);
            Assert.DoesNotContain(1, ids);
            Assert.Contains(2, ids);
        }

        [Fact]
        public void StagingAddsEvent350WithoutMutatingCallerList() {
            using var watcher = new WatchEvents();
            var ids = new List<int> { 1 };
            watcher.Watch(Environment.MachineName, "Application", ids, null, default, true, "tester");

            Assert.DoesNotContain(350, ids);
            Assert.Contains(350, GetIds(watcher));
            Assert.Equal("tester", watcher.StagingEnabledBy);
        }

        [Fact]
        public void ResetGlobalEventCountIsExplicit() {
            WatchEvents.ResetGlobalEventCount();
            Assert.Equal(0, WatchEvents.NumberOfEventsFound);
        }

        [Fact]
        public void WatchRejectsEmptyEventFilter() {
            using var watcher = new WatchEvents();
            Assert.Throws<ArgumentException>(() => watcher.Watch(null, "Application", new List<int>()));
        }

        [Fact]
        public void WatchRejectsAnAlreadyCancelledSubscription() {
            using var watcher = new WatchEvents();
            using var cancellation = new System.Threading.CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => watcher.Watch(
                null,
                "Application",
                new List<int> { 1 },
                cancellationToken: cancellation.Token));
            Assert.Empty(GetIds(watcher));
        }

        [Fact]
        public async Task CancellationAndDisposeCompleteWithoutDeadlock() {
            for (int index = 0; index < 25; index++) {
                var watcher = new WatchEvents();
                using var cancellation = new System.Threading.CancellationTokenSource();
                watcher.Watch(
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    cancellationToken: cancellation.Token);

                Task cancel = Task.Run(cancellation.Cancel);
                Task dispose = Task.Run(watcher.Dispose);
                Task operations = Task.WhenAll(cancel, dispose);
                Task completed = await Task.WhenAny(
                    operations,
                    Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Same(operations, completed);
                await operations;
                Assert.Empty(GetIds(watcher));
            }
        }

        [Fact]
        public void CancellationRaisesStoppedExactlyOnce() {
            var watcher = new WatchEvents();
            using var cancellation =
                new System.Threading.CancellationTokenSource();
            int stopped = 0;
            watcher.Stopped += (_, _) =>
                System.Threading.Interlocked.Increment(
                    ref stopped);
            watcher.Watch(
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                cancellationToken: cancellation.Token);

            cancellation.Cancel();
            Assert.True(
                System.Threading.SpinWait.SpinUntil(
                    () =>
                        System.Threading.Volatile.Read(
                            ref stopped) == 1,
                    TimeSpan.FromSeconds(5)));
            watcher.Dispose();

            Assert.Equal(1, stopped);
        }

        [Fact]
        public async Task CancellationDoesNotWaitForStoppedCallbacks() {
            using var watcher = new WatchEvents();
            using var cancellation =
                new System.Threading.CancellationTokenSource();
            using var callbackStarted =
                new System.Threading.ManualResetEventSlim();
            using var releaseCallback =
                new System.Threading.ManualResetEventSlim();
            watcher.Stopped += (_, _) => {
                callbackStarted.Set();
                releaseCallback.Wait(
                    TimeSpan.FromSeconds(30));
            };
            watcher.Watch(
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                cancellationToken: cancellation.Token);

            Task cancel = Task.Run(cancellation.Cancel);
            try {
                Task completed = await Task.WhenAny(
                    cancel,
                    Task.Delay(TimeSpan.FromSeconds(5)));

                Assert.Same(cancel, completed);
                await cancel;
                Assert.True(
                    callbackStarted.Wait(
                        TimeSpan.FromSeconds(5)));
            } finally {
                releaseCallback.Set();
            }
        }
    }
}
