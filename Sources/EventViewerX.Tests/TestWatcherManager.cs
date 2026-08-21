using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Net;
using Xunit;

namespace EventViewerX.Tests {
    [Collection("WatcherManager")]
    public class TestWatcherManager {
        [Fact]
        public void StartWatcherReturnsExistingInstance() {
            // ensure a clean slate so name-based reuse isn't impacted by previous tests
            WatcherManager.StopAll();
            Action<EventObject> action = _ => { };
            var first = WatcherManager.StartWatcher(
                "unit", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), action, false, false, 0, null);
            var second = WatcherManager.StartWatcher(
                "unit", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), action, false, false, 0, null);
            if (first.EndTime == null) {
                Assert.Same(first, second);
            } else {
                // If the first watcher stopped immediately (e.g., provider not available on this host),
                // we expect a fresh instance to be created.
                Assert.NotSame(first, second);
            }
            WatcherManager.StopAll();
        }

        [Fact]
        public void StartWatcherUsesStableActionIdentityForRecreatedHostDelegates() {
            WatcherManager.StopAll();
            Action<EventObject> firstAction = _ => { };
            var existing = new WatcherInfo(
                "unit-stable-action", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), firstAction, false, false, 0, null) {
                ActionIdentity = "script:stable"
            };
            var watchersField = typeof(WatcherManager).GetField("_watchers", BindingFlags.NonPublic | BindingFlags.Static);
            var namesField = typeof(WatcherManager).GetField("_watchersByName", BindingFlags.NonPublic | BindingFlags.Static);
            var watchers = Assert.IsType<ConcurrentDictionary<Guid, WatcherInfo>>(watchersField!.GetValue(null));
            var names = Assert.IsType<ConcurrentDictionary<string, WatcherInfo>>(namesField!.GetValue(null));
            watchers[existing.Id] = existing;
            names[existing.Name] = existing;

            try {
                WatcherInfo reused = WatcherManager.StartWatcher(
                    existing.Name,
                    existing.MachineName,
                    existing.LogName,
                    new List<int> { 1 },
                    new List<EventType>(),
                    _ => { },
                    false,
                    false,
                    0,
                    null,
                    actionIdentity: "script:stable");

                Assert.Same(existing, reused);
                Assert.Throws<InvalidOperationException>(() => WatcherManager.StartWatcher(
                    existing.Name,
                    existing.MachineName,
                    existing.LogName,
                    new List<int> { 1 },
                    new List<EventType>(),
                    _ => { },
                    false,
                    false,
                    0,
                    null,
                    actionIdentity: "script:different"));
            } finally {
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StableWatcherReuseIsIsolatedByHostScope() {
            WatcherManager.StopAll();
            const string watcherName = "unit-scoped-reuse";
            try {
                WatcherInfo first = WatcherManager.StartWatcher(
                    watcherName,
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    new List<EventType>(),
                    _ => { },
                    false,
                    false,
                    0,
                    null,
                    actionIdentity: "script:stable",
                    reuseScopeIdentity: "scope-a");
                WatcherInfo second = WatcherManager.StartWatcher(
                    watcherName,
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    new List<EventType>(),
                    _ => { },
                    false,
                    false,
                    0,
                    null,
                    actionIdentity: "script:stable",
                    reuseScopeIdentity: "scope-b");

                Assert.NotSame(first, second);
                Assert.NotEqual(first.Id, second.Id);
                Assert.Contains(first, WatcherManager.GetWatchers(watcherName));
                Assert.Contains(second, WatcherManager.GetWatchers(watcherName));

                WatcherManager.StopWatchersByName(watcherName);
                Assert.Empty(WatcherManager.GetWatchers(watcherName));
            } finally {
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StartWatcherRejectsSameNameWithDifferentConfiguration() {
            WatcherManager.StopAll();
            Action<EventObject> action = _ => { };
            var existing = new WatcherInfo(
                "unit-mismatch", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), action, false, false, 0, null);
            var watchersField = typeof(WatcherManager).GetField("_watchers", BindingFlags.NonPublic | BindingFlags.Static);
            var namesField = typeof(WatcherManager).GetField("_watchersByName", BindingFlags.NonPublic | BindingFlags.Static);
            var watchers = Assert.IsType<ConcurrentDictionary<Guid, WatcherInfo>>(watchersField!.GetValue(null));
            var names = Assert.IsType<ConcurrentDictionary<string, WatcherInfo>>(namesField!.GetValue(null));
            watchers[existing.Id] = existing;
            names[existing.Name] = existing;

            try {
                var exception = Assert.Throws<InvalidOperationException>(() => WatcherManager.StartWatcher(
                    "unit-mismatch", Environment.MachineName, "Application", new List<int> { 2 }, new List<EventType>(), action, false, false, 0, null));

                Assert.Contains("different configuration", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StartWatcherDoesNotReuseExplicitQueryForGeneratedDefault() {
            WatcherManager.StopAll();
            const string watcherName =
                "unit-explicit-versus-generated";
            Action<EventObject> action = _ => { };
            var explicitQuery =
                new EventLogSubscriptionQuery(
                    "Application") {
                    MachineName = Environment.MachineName,
                    XPath = "*[System[EventID=2]]",
                    Start = EventLogSubscriptionStart.Future,
                    ReadMode = EventReadMode.Full,
                    RemoteConnectionTimeoutMilliseconds =
                        Settings.SessionTimeoutMs,
                    BufferCapacity = 256
                };
            var existing = new WatcherInfo(
                watcherName,
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                action,
                false,
                false,
                0,
                null,
                explicitQuery) {
                    ActionIdentity = "script:stable"
                };
            var watchersField = typeof(WatcherManager).GetField(
                "_watchers",
                BindingFlags.NonPublic |
                BindingFlags.Static);
            var namesField = typeof(WatcherManager).GetField(
                "_watchersByName",
                BindingFlags.NonPublic |
                BindingFlags.Static);
            var watchers =
                Assert.IsType<ConcurrentDictionary<Guid, WatcherInfo>>(
                    watchersField!.GetValue(null));
            var names =
                Assert.IsType<ConcurrentDictionary<string, WatcherInfo>>(
                    namesField!.GetValue(null));
            watchers[existing.Id] = existing;
            names[watcherName] = existing;

            try {
                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(() =>
                        WatcherManager.StartWatcher(
                            watcherName,
                            Environment.MachineName,
                            "Application",
                            new List<int> { 1 },
                            new List<EventType>(),
                            _ => { },
                            false,
                            false,
                            0,
                            null,
                            actionIdentity:
                                "script:stable"));

                Assert.Contains(
                    "different configuration",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            } finally {
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StartWatcherRejectsRotatedPasswordForNamedRemoteConfiguration() {
            WatcherManager.StopAll();
            const string watcherName = "unit-password-rotation";
            const string machineName = "server.example.test";
            Action<EventObject> action = _ => { };
            var existingQuery = new EventLogSubscriptionQuery(
                "Application") {
                MachineName = machineName,
                Credential = new NetworkCredential(
                    "watcher-user",
                    "old-password",
                    "EXAMPLE")
            };
            var requestedQuery = new EventLogSubscriptionQuery(
                "Application") {
                MachineName = machineName,
                Credential = new NetworkCredential(
                    "watcher-user",
                    "new-password",
                    "EXAMPLE")
            };
            var existing = new WatcherInfo(
                watcherName,
                machineName,
                "Application",
                new List<int>(),
                new List<EventType>(),
                action,
                false,
                false,
                0,
                null,
                existingQuery) {
                ActionIdentity = "script:stable"
            };
            var watchersField = typeof(WatcherManager).GetField(
                "_watchers",
                BindingFlags.NonPublic |
                BindingFlags.Static);
            var namesField = typeof(WatcherManager).GetField(
                "_watchersByName",
                BindingFlags.NonPublic |
                BindingFlags.Static);
            var watchers =
                Assert.IsType<ConcurrentDictionary<Guid, WatcherInfo>>(
                    watchersField!.GetValue(null));
            var names =
                Assert.IsType<ConcurrentDictionary<string, WatcherInfo>>(
                    namesField!.GetValue(null));
            watchers[existing.Id] = existing;
            names[watcherName] = existing;

            try {
                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(() =>
                        WatcherManager.StartWatcher(
                            watcherName,
                            requestedQuery,
                            action,
                            actionIdentity: "script:stable"));

                Assert.Contains(
                    "different configuration",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            } finally {
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StartWatcherAcceptsPartitionedQueriesWithoutDummyEventIds() {
            WatcherManager.StopAll();
            string watcherName =
                "unit-partitioned-" +
                Guid.NewGuid().ToString("N");
            WatcherInfo? watcher = null;
            try {
                var query = new EventLogSubscriptionQuery(
                    "Application") {
                    XPath =
                        "*[System[EventID=2147483647]]"
                };

                watcher = WatcherManager.StartWatcher(
                    watcherName,
                    Environment.MachineName,
                    "Application",
                    new List<int>(),
                    new List<EventType>(),
                    _ => { },
                    staging: false,
                    stopOnMatch: false,
                    stopAfter: 0,
                    timeout: null,
                    subscriptionQuery: null,
                    subscriptionQueries:
                        new[] { query });

                Assert.NotNull(watcher);
            } finally {
                if (watcher != null &&
                    !watcher.IsStopped) {
                    WatcherManager.StopWatcher(
                        watcher.Id);
                }
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void ExplicitWatcherFiltersAcceptEventIdZero() {
            EventLogSubscriptionQuery query =
                WatcherInfo.CreateSubscriptionQuery(
                    Environment.MachineName,
                    "Application",
                    new[] { 0 },
                    staging: false);

            Assert.Contains(
                "EventID=0",
                query.XPath,
                StringComparison.Ordinal);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EventIdValidation.Normalize(
                    new[] { -1 },
                    "eventIds"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EventIdValidation.Normalize(
                    new[] { 65536 },
                    "eventIds"));
        }

        [Fact]
        public void WatcherExposesDefensiveSubscriptionContracts() {
            var source =
                new EventLogSubscriptionQuery(
                    "Application") {
                    XPath = "*[System[EventID=1]]",
                    Credential =
                        new System.Net.NetworkCredential(
                            "original",
                            "secret",
                            "domain")
                };
            using var watcher = new WatcherInfo(
                "immutable-contract",
                Environment.MachineName,
                "Application",
                new List<int>(),
                new List<EventType>(),
                _ => { },
                staging: false,
                stopOnMatch: false,
                stopAfter: 0,
                timeout: null,
                subscriptionQuery: source);

            source.XPath = "*[System[EventID=2]]";
            EventLogSubscriptionQuery exposed =
                watcher.SubscriptionQuery;
            exposed.XPath = "*[System[EventID=3]]";
            exposed.Credential!.UserName = "mutated";

            Assert.Equal(
                "*[System[EventID=1]]",
                watcher.SubscriptionQuery.XPath);
            Assert.Equal(
                "original",
                watcher.SubscriptionQuery
                    .Credential!.UserName);
            Assert.Equal(
                "*[System[EventID=1]]",
                watcher.SubscriptionContracts[0].XPath);
        }

        [Fact]
        public void StartWatcherThrowsWhenDuplicatesExist() {
            var field = typeof(WatcherManager).GetField("_watchers", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            var dict = (ConcurrentDictionary<Guid, WatcherInfo>)field!.GetValue(null)!;
            var watcher1 = new WatcherInfo("dup", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), _ => { }, false, false, 0, null);
            var watcher2 = new WatcherInfo("dup", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), _ => { }, false, false, 0, null);
            dict.TryAdd(Guid.NewGuid(), watcher1);
            dict.TryAdd(Guid.NewGuid(), watcher2);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                WatcherManager.StartWatcher("dup", Environment.MachineName, "Application", new List<int> { 1 }, new List<EventType>(), _ => { }, false, false, 0, null));
            Assert.Contains("Multiple watchers", ex.Message);
            WatcherManager.StopAll();
        }

        [Fact]
        public void OnEventLogsWarningOnException() {
            var info = (WatcherInfo)Activator.CreateInstance(typeof(WatcherInfo),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] {
                    "test", Environment.MachineName, "Application", new List<int> { 1 },
                    new List<EventType>(), new Action<EventObject>(_ => throw new InvalidOperationException("fail")),
                    false, false, 0, null
                }, null)!;

            Exception? captured = null;
            info.ActionException += (_, ex) => captured = ex;
            string? message = null;
            EventHandler<LogEventArgs> handler = (_, e) => message = e.FullMessage;
            Settings._logger.OnWarningMessage += handler;
            try {
                var dummy = (EventObject)FormatterServices.GetUninitializedObject(typeof(EventObject));
                var method = typeof(WatcherInfo).GetMethod("OnEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
                method.Invoke(info, new object[] { dummy });
                Assert.NotNull(captured);
                Assert.Contains("fail", captured!.Message);
                Assert.NotNull(message);
            } finally {
                Settings._logger.OnWarningMessage -= handler;
            }
        }

        [Fact]
        public void OnEventHonorsStopAfterExactly() {
            int delivered = 0;
            var info = (WatcherInfo)Activator.CreateInstance(
                typeof(WatcherInfo),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] {
                    "limited",
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    new List<EventType>(),
                    new Action<EventObject>(_ =>
                        Interlocked.Increment(ref delivered)),
                    false,
                    false,
                    1,
                    null
                },
                null)!;
            var dummy = (EventObject)FormatterServices
                .GetUninitializedObject(typeof(EventObject));
            var method = typeof(WatcherInfo).GetMethod(
                "OnEvent",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!;

            for (int i = 0; i < 128; i++) {
                method.Invoke(info, new object[] { dummy });
            }

            Assert.Equal(1, delivered);
            Assert.Equal(1, info.EventsFound);
            info.Dispose();
        }

        [Fact]
        public void PartitionedWatcherDeliversOneLogicalRecordOnce() {
            int delivered = 0;
            var queries =
                new[] {
                    new EventLogSubscriptionQuery(
                        "Application") {
                        XPath = "*[System[EventID=1]]"
                    },
                    new EventLogSubscriptionQuery(
                        "Application") {
                        XPath = "*[System[EventID=2]]"
                    }
                };
            var info = new WatcherInfo(
                "partitioned-dedup",
                Environment.MachineName,
                "Application",
                new List<int>(),
                new List<EventType>(),
                _ => Interlocked.Increment(
                    ref delivered),
                false,
                false,
                0,
                null,
                queries);
            var duplicate =
                (EventObject)FormatterServices
                    .GetUninitializedObject(
                        typeof(EventObject));
            var method = typeof(WatcherInfo).GetMethod(
                "OnEvent",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!;

            method.Invoke(
                info,
                new object[] { duplicate });
            method.Invoke(
                info,
                new object[] { duplicate });

            Assert.Equal(1, delivered);
            Assert.Equal(1, info.EventsFound);
            info.Dispose();
        }

        [Fact]
        public void StopDoesNotHoldStateLockWhileCancellingSubscriptions() {
            var info = new WatcherInfo(
                "stop-lock",
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                _ => { },
                false,
                false,
                0,
                null);
            using CancellationTokenRegistration registration =
                info.Cancellation.Token.Register(
                    () => {
                        _ = info.IsStopped;
                        _ = info.EndTime;
                    });

            Task stop = Task.Run(info.Stop);

            Assert.True(
                stop.Wait(TimeSpan.FromSeconds(5)),
                "Watcher stop deadlocked while a cancellation callback read logical state.");
            Assert.True(info.IsStopped);
            Assert.NotNull(info.EndTime);
        }

        [Fact]
        public void LastTerminalSubscriptionRetiresTheLogicalWatcher() {
            WatcherManager.StopAll();
            string watcherName =
                "unit-terminal-" +
                Guid.NewGuid().ToString("N");
            WatcherInfo? info = null;
            try {
                info = WatcherManager.StartWatcher(
                    watcherName,
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    new List<EventType>(),
                    _ => { },
                    false,
                    false,
                    0,
                    null);
                EventHandler? stopped =
                    Assert.IsType<EventHandler>(
                        typeof(WatchEvents)
                            .GetField(
                                "Stopped",
                                BindingFlags.Instance |
                                BindingFlags.NonPublic)!
                            .GetValue(info.Watcher));

                stopped(
                    info.Watcher,
                    EventArgs.Empty);

                Assert.True(
                    SpinWait.SpinUntil(
                        () => info.IsStopped,
                        TimeSpan.FromSeconds(5)));
                Assert.DoesNotContain(
                    info,
                    WatcherManager.GetWatchers(
                        watcherName));
            } finally {
                if (info != null &&
                    !info.IsStopped) {
                    WatcherManager.StopWatcher(
                        info.Id);
                }
                WatcherManager.StopAll();
            }
        }

        [Fact]
        public void StartWatcherIsThreadSafe() {
            WatcherManager.StopAll();
            var tasks = new List<Task<WatcherInfo>>();
            Action<EventObject> action = _ => { };
            for (int i = 0; i < 5; i++) {
                tasks.Add(Task.Run(() => WatcherManager.StartWatcher(
                    "sync", Environment.MachineName, "Application", new List<int> { 1 },
                    new List<EventType>(), action, false, false, 0, null)));
            }
            Task.WaitAll(tasks.ToArray());
            var first = tasks[0].Result;
            foreach (var t in tasks) {
                Assert.Same(first, t.Result);
            }
            WatcherManager.StopAll();
        }

        [Fact]
        public void StartWatcherRejectsCancelledStartupBeforeRegistration() {
            WatcherManager.StopAll();
            using var cancellation =
                new CancellationTokenSource();
            cancellation.Cancel();
            string watcherName =
                "cancelled-start-" +
                Guid.NewGuid().ToString("N");
            var query =
                new EventLogSubscriptionQuery(
                    "Application");

            Assert.Throws<OperationCanceledException>(() =>
                WatcherManager.StartWatcher(
                    watcherName,
                    query,
                    _ => { },
                    cancellationToken:
                        cancellation.Token));
            Assert.Empty(
                WatcherManager.GetWatchers(
                    watcherName));
        }

        [Fact]
        public void WatcherStartupCancellationWinsBeforeCommit() {
            using var cancellation =
                new CancellationTokenSource();
            var info = new WatcherInfo(
                "cancel-before-commit",
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                _ => { },
                false,
                false,
                0,
                null) {
                BeforeStartupCommit =
                    cancellation.Cancel
            };

            Assert.Throws<OperationCanceledException>(() =>
                info.Start(
                    cancellation.Token));

            Assert.True(info.IsStopped);
            info.Dispose();
        }

        [Fact]
        public async Task PendingNameReservationDoesNotBlockIndependentWatcherStartup() {
            WatcherManager.StopAll();
            string reservedName =
                "reserved-" +
                Guid.NewGuid().ToString("N");
            string independentName =
                "independent-" +
                Guid.NewGuid().ToString("N");
            Action<EventObject> action = _ => { };
            var reserved = new WatcherInfo(
                reservedName,
                Environment.MachineName,
                "Application",
                new List<int> { 1 },
                new List<EventType>(),
                action,
                false,
                false,
                0,
                null);
            reserved.ReserveStartup();
            FieldInfo? watchersField =
                typeof(WatcherManager).GetField(
                    "_watchers",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            FieldInfo? namesField =
                typeof(WatcherManager).GetField(
                    "_watchersByName",
                    BindingFlags.NonPublic |
                    BindingFlags.Static);
            Assert.NotNull(watchersField);
            Assert.NotNull(namesField);
            var watchers =
                Assert.IsType<ConcurrentDictionary<Guid, WatcherInfo>>(
                    watchersField!.GetValue(null));
            var names =
                Assert.IsType<ConcurrentDictionary<string, WatcherInfo>>(
                    namesField!.GetValue(null));
            watchers[reserved.Id] = reserved;
            names[$"U:{reservedName}"] = reserved;
            using var cancellation =
                new CancellationTokenSource();
            Task<WatcherInfo> waiting = Task.Run(() =>
                WatcherManager.StartWatcher(
                    reservedName,
                    Environment.MachineName,
                    "Application",
                    new List<int> { 1 },
                    new List<EventType>(),
                    action,
                    false,
                    false,
                    0,
                    null,
                    cancellationToken:
                        cancellation.Token));
            try {
                await Task.Delay(100);
                Assert.False(waiting.IsCompleted);

                WatcherInfo independent =
                    WatcherManager.StartWatcher(
                        independentName,
                        Environment.MachineName,
                        "Application",
                        new List<int> { 1 },
                        new List<EventType>(),
                        action,
                        false,
                        false,
                        0,
                        null);

                Assert.False(independent.IsStopped);
            } finally {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () =>
                        await waiting);
                WatcherManager.StopAll();
            }
        }
    }
}
