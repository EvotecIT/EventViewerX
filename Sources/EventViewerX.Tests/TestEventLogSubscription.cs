using System.Collections.Concurrent;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventLogSubscription {
    [Fact]
    public void QueryListClassificationAcceptsXmlDeclarationsAndComments() {
        const string queryXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- generated query -->
            <QueryList>
              <Query Id="0" Path="System">
                <Select Path="System">*</Select>
              </Query>
            </QueryList>
            """;

        Assert.True(
            EventLogStructuredQueryParser.IsQueryList(
                queryXml));
        if (!OperatingSystem.IsWindows()) return;

        using var subscription = new EventLogSubscription(
            new EventLogSubscriptionQuery(
                "IgnoredByQueryList") {
                XPath = queryXml,
                Start = EventLogSubscriptionStart.Future,
                ReadMode = EventReadMode.Metadata
            },
            _ => { });
    }

    [Fact]
    public void LogicalSubscriptionStopsOnlyAfterEveryPartitionTerminates() {
        var lifetime =
            new EventLogSubscriptionLifetime(
                subscriptionCount: 2);

        Assert.False(
            lifetime.MarkTerminal(
                subscriptionIndex: 0));
        Assert.False(
            lifetime.MarkTerminal(
                subscriptionIndex: 0));
        Assert.True(
            lifetime.MarkTerminal(
                subscriptionIndex: 1));
    }

    [Fact]
    public void OldestSubscriptionDeliversAnExistingExactRecord() {
        if (!OperatingSystem.IsWindows()) return;
        var currentQuery = new EventLogChannelQuery("System") {
            ReadMode = EventReadMode.Metadata,
            MaxEvents = 1
        };
        EventObject current = EventLogEngine.ReadChannel(currentQuery).Single();
        Assert.True(current.RecordId.HasValue);

        using var delivered = new ManualResetEventSlim();
        EventObject? received = null;
        EventLogSubscriptionFailure? failure = null;
        var query = new EventLogSubscriptionQuery("System") {
            XPath = $"*[System[EventRecordID={current.RecordId.Value}]]",
            Start = EventLogSubscriptionStart.Oldest,
            ReadMode = EventReadMode.Metadata
        };
        using var subscription = new EventLogSubscription(
            query,
            eventObject => {
                received = eventObject;
                delivered.Set();
            },
            subscriptionFailure => {
                failure = subscriptionFailure;
                delivered.Set();
            });

        Assert.True(delivered.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.NotNull(received);
        Assert.Equal(current.RecordId, received!.RecordId);
        Assert.True(
            SpinWait.SpinUntil(
                () => subscription.EventsDelivered == 1,
                TimeSpan.FromSeconds(5)),
            $"Expected one completed delivery, but observed {subscription.EventsDelivered}.");
    }

    [Fact]
    public void OldestSubscriptionAcceptsStructuredNamedDataSuppression() {
        if (!OperatingSystem.IsWindows()) return;
        EventObject current = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        Assert.True(current.RecordId.HasValue);
        string queryXml =
            EventFilterCompiler.BuildChannelQueryXml(
                new[] { "System" },
                new EventFilter {
                    RecordIds = new[] {
                        current.RecordId.Value
                    },
                    ExcludedNamedData =
                        new Dictionary<string, IReadOnlyList<string>> {
                            ["FieldThatDoesNotExist"] =
                                new[] { "ExcludedValue" }
                        }
                });
        using var delivered = new ManualResetEventSlim();
        EventObject? received = null;
        EventLogSubscriptionFailure? failure = null;
        using var subscription = new EventLogSubscription(
            new EventLogSubscriptionQuery("System") {
                XPath = queryXml,
                Start = EventLogSubscriptionStart.Oldest,
                ReadMode = EventReadMode.Metadata
            },
            eventObject => {
                received = eventObject;
                delivered.Set();
            },
            subscriptionFailure => {
                failure = subscriptionFailure;
                delivered.Set();
            });

        Assert.True(delivered.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.NotNull(received);
        Assert.Equal(current.RecordId, received!.RecordId);
    }

    [Fact]
    public void EventCallbackCanDisposeItsSubscriptionWithoutDeadlock() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        _ = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        using var assigned = new ManualResetEventSlim();
        using var callbackCompleted = new ManualResetEventSlim();
        EventLogSubscription? subscription = null;
        EventLogSubscriptionFailure? failure = null;
        int callbacks = 0;
        try {
            subscription = new EventLogSubscription(
                new EventLogSubscriptionQuery("System") {
                    XPath = "*",
                    Start = EventLogSubscriptionStart.Oldest,
                    ReadMode = EventReadMode.Metadata
                },
                _ => {
                    Interlocked.Increment(ref callbacks);
                    if (!assigned.Wait(TimeSpan.FromSeconds(10))) {
                        return;
                    }
                    subscription!.Dispose();
                    callbackCompleted.Set();
                },
                subscriptionFailure => {
                    failure = subscriptionFailure;
                    callbackCompleted.Set();
                });
            assigned.Set();

            Assert.True(
                callbackCompleted.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(failure);
            Thread.Sleep(100);
            Assert.Equal(
                1,
                Volatile.Read(ref callbacks));
        } finally {
            subscription?.Dispose();
        }
    }

    [Fact]
    public void ExternalCancellationDoesNotWaitForAConsumerCallback() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        EventObject current = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        EventLogSubscription? subscription = null;
        Task? cancelTask = null;
        try {
            subscription = new EventLogSubscription(
                new EventLogSubscriptionQuery("System") {
                    XPath =
                        $"*[System[EventRecordID={current.RecordId!.Value}]]",
                    Start = EventLogSubscriptionStart.Oldest,
                    ReadMode = EventReadMode.Metadata
                },
                _ => {
                    callbackEntered.Set();
                    releaseCallback.Wait(
                        TimeSpan.FromSeconds(10));
                },
                cancellationToken:
                    cancellation.Token);

            Assert.True(
                callbackEntered.Wait(
                    TimeSpan.FromSeconds(10)));
            cancelTask = Task.Factory.StartNew(
                cancellation.Cancel,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Assert.True(
                cancelTask.Wait(
                    TimeSpan.FromSeconds(2)));
        } finally {
            releaseCallback.Set();
            Assert.True(
                cancelTask?.Wait(
                    TimeSpan.FromSeconds(10)) ??
                true);
            subscription?.Dispose();
        }
    }

    [Fact]
    public void FailureCallbackCanDisposeItsSubscriptionWithoutDeadlock() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        EventObject current = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        using var assigned = new ManualResetEventSlim();
        using var callbackCompleted = new ManualResetEventSlim();
        EventLogSubscription? subscription = null;
        EventLogSubscriptionFailure? failure = null;
        try {
            subscription = new EventLogSubscription(
                new EventLogSubscriptionQuery("System") {
                    XPath =
                        $"*[System[EventRecordID={current.RecordId!.Value}]]",
                    Start = EventLogSubscriptionStart.Oldest,
                    ReadMode = EventReadMode.Metadata
                },
                _ => throw new InvalidOperationException(
                    "callback failure"),
                subscriptionFailure => {
                    failure = subscriptionFailure;
                    if (!assigned.Wait(TimeSpan.FromSeconds(10))) {
                        return;
                    }
                    subscription!.Dispose();
                    callbackCompleted.Set();
                });
            assigned.Set();

            Assert.True(
                callbackCompleted.Wait(TimeSpan.FromSeconds(10)));
            Assert.NotNull(failure);
            Assert.False(failure!.Terminal);
            Assert.IsType<InvalidOperationException>(
                failure.Exception);
        } finally {
            subscription?.Dispose();
        }
    }

    [Fact]
    public void StructuredSubscriptionUsesEachEventsNativeContainerLog() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        EventObject system = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        EventObject application = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("Application") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">" +
            $"*[System[EventRecordID={system.RecordId!.Value}]]" +
            "</Select></Query>" +
            "<Query Id=\"1\" Path=\"Application\">" +
            "<Select Path=\"Application\">" +
            $"*[System[EventRecordID={application.RecordId!.Value}]]" +
            "</Select></Query>" +
            "</QueryList>";
        var received =
            new ConcurrentDictionary<string, EventObject>(
                StringComparer.OrdinalIgnoreCase);
        using var delivered = new CountdownEvent(2);
        EventLogSubscriptionFailure? failure = null;
        using var subscription = new EventLogSubscription(
            new EventLogSubscriptionQuery("System") {
                XPath = queryXml,
                Start = EventLogSubscriptionStart.Oldest,
                ReadMode = EventReadMode.Metadata
            },
            eventObject => {
                if (received.TryAdd(
                        eventObject.LogName,
                        eventObject)) {
                    delivered.Signal();
                }
            },
            subscriptionFailure => {
                failure = subscriptionFailure;
            });

        Assert.True(delivered.Wait(TimeSpan.FromSeconds(10)));
        Assert.Null(failure);
        Assert.Equal(
            "System",
            received["System"].ContainerLog,
            ignoreCase: true);
        Assert.Equal(
            "Application",
            received["Application"].ContainerLog,
            ignoreCase: true);
    }

    [Fact]
    public void SubscriptionRejectsBookmarkWithoutBookmarkStart() {
        var query = new EventLogSubscriptionQuery("System") {
            BookmarkXml = "<BookmarkList />"
        };

        Assert.Throws<ArgumentException>(() =>
            new EventLogSubscription(query, _ => { }));
    }

    [Fact]
    public async Task SubscriptionStartupReturnsWhenNativeCreationIsCancelled() {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        Task<Native.WindowsEventNativeMethods.EventHandle>
            subscription = Task.Run(() =>
                EventLogSubscription
                    .CreateSubscriptionBounded(
                        () => {
                            started.Set();
                            release.Wait();
                            return new Native
                                .WindowsEventNativeMethods
                                .EventHandle();
                        },
                        5000,
                        cancellation.Token));
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(5)));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            subscription,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(
                subscription,
                completed);
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                async () =>
                    await subscription);
        } finally {
            release.Set();
        }
    }

    [Fact]
    public async Task InitialDiagnosticsCancellationRetainsNativeLifetimeUntilCompletion() {
        using var started =
            new ManualResetEventSlim();
        using var release =
            new ManualResetEventSlim();
        using var cancellation =
            new CancellationTokenSource();
        var operationLease =
            new TestOperationLease();
        Task diagnostics = Task.Run(() =>
            EventLogSubscription
                .ReportInitialQueryFailuresBounded(
                    () => {
                        started.Set();
                        release.Wait();
                    },
                    5000,
                    cancellation.Token,
                    operationLease));
        Assert.True(
            started.Wait(
                TimeSpan.FromSeconds(5)));

        cancellation.Cancel();
        Task completed = await Task.WhenAny(
            diagnostics,
            Task.Delay(
                TimeSpan.FromSeconds(5)));
        try {
            Assert.Same(
                diagnostics,
                completed);
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                async () =>
                    await diagnostics);
            Assert.False(
                operationLease.IsDisposed);
        } finally {
            release.Set();
        }
        Assert.True(
            SpinWait.SpinUntil(
                () => operationLease.IsDisposed,
                TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void CancellationReportedDuringQueryDiagnosticsFailsStartup() {
        if (!OperatingSystem.IsWindows()) return;
        const string missingLog =
            "EventViewerX-Missing-Subscription-Cancellation";
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">*</Select>" +
            "</Query>" +
            $"<Query Id=\"1\" Path=\"{missingLog}\">" +
            $"<Select Path=\"{missingLog}\">*</Select>" +
            "</Query>" +
            "</QueryList>";
        using var cancellation =
            new CancellationTokenSource();
        int failures = 0;

        Assert.ThrowsAny<OperationCanceledException>(() =>
            new EventLogSubscription(
                new EventLogSubscriptionQuery("System") {
                    XPath = queryXml,
                    Start = EventLogSubscriptionStart.Future,
                    ReadMode = EventReadMode.Metadata,
                    TolerateQueryErrors = true
                },
                _ => { },
                _ => {
                    Interlocked.Increment(ref failures);
                    cancellation.Cancel();
                },
                cancellation.Token));

        Assert.Equal(
            1,
            Volatile.Read(ref failures));
    }

    [Fact]
    public void FutureSubscriptionIsSignaledAndDeliversANewEvent() {
        if (!OperatingSystem.IsWindows()) return;
        if (!TestEnv.IsAdmin()) return;
        using IDisposable isolation =
            TestClassicEventLogIsolation.Acquire();
        string suffix = Guid.NewGuid().ToString("N");
        string logName = $"EVX{suffix}Subscription";
        string sourceName = $"EVXS{suffix}SubscriptionSource";
        try {
            ClassicEventLogManager.EnsureLog(
                new ClassicEventLogConfiguration {
                    LogName = logName,
                    SourceName = sourceName,
                    MaximumKilobytes = 256,
                    OverflowAction =
                        System.Diagnostics.OverflowAction
                            .OverwriteAsNeeded
                });
            using var delivered =
                new ManualResetEventSlim();
            EventObject? received = null;
            using var subscription =
                new EventLogSubscription(
                    new EventLogSubscriptionQuery(
                        logName) {
                        Start =
                            EventLogSubscriptionStart.Future,
                        XPath = "*[System[EventID=7001]]",
                        ReadMode =
                            EventReadMode.StructuredData,
                        BufferCapacity = 1
                    },
                    eventObject => {
                        received = eventObject;
                        delivered.Set();
                    });

            ClassicEventLogManager.Write(
                new ClassicEventWriteRequest {
                    LogName = logName,
                    SourceName = sourceName,
                    EventId = 7001,
                    Message = "future-subscription"
                });

            Assert.True(
                delivered.Wait(TimeSpan.FromSeconds(10)));
            Assert.NotNull(received);
            Assert.Equal(7001, received!.Id);
            Assert.Equal(logName, received.LogName);
        } finally {
            if (ClassicEventLogManager.LogExists(logName)) {
                ClassicEventLogManager.RemoveLog(logName);
            }
        }
    }

    [Fact]
    public void WatcherManagerAcceptsTheCompleteSubscriptionContract() {
        if (!OperatingSystem.IsWindows()) return;
        EventObject current = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                MaxEvents = 1,
                ReadMode = EventReadMode.Metadata
            }).Single();
        using var delivered = new ManualResetEventSlim();
        var query = new EventLogSubscriptionQuery("System") {
            XPath =
                $"*[System[EventRecordID={current.RecordId!.Value}]]",
            Start = EventLogSubscriptionStart.Oldest,
            ReadMode = EventReadMode.Metadata,
            MessageCulture =
                System.Globalization.CultureInfo.GetCultureInfo(
                    "en-US"),
            BufferCapacity = 8
        };

        WatcherInfo watcher = WatcherManager.StartWatcher(
            name: null,
            query,
            _ => delivered.Set(),
            stopAfter: 1);
        try {
            Assert.True(delivered.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(
                SpinWait.SpinUntil(
                    () => watcher.EndTime.HasValue,
                    TimeSpan.FromSeconds(10)));
            Assert.Equal(1, watcher.EventsFound);
            Assert.Equal(query.XPath, watcher.SubscriptionQuery.XPath);
            Assert.Equal(
                EventLogSubscriptionStart.Oldest,
                watcher.SubscriptionQuery.Start);
        } finally {
            WatcherManager.StopWatcher(watcher.Id);
        }
    }

    [Fact]
    public void WatcherManagerOwnsPartitionedSubscriptionsAsOneWatcher() {
        if (!OperatingSystem.IsWindows()) return;
        var queries = new[] {
            new EventLogSubscriptionQuery("System") {
                XPath = "*[System[EventID=1]]",
                ReadMode = EventReadMode.Metadata
            },
            new EventLogSubscriptionQuery("System") {
                XPath = "*[System[EventID=2]]",
                ReadMode = EventReadMode.Metadata
            }
        };

        WatcherInfo watcher = WatcherManager.StartWatcher(
            name: null,
            queries,
            _ => { });
        try {
            Assert.Equal(2, watcher.SubscriptionQueries.Count);
            Assert.Equal(
                queries.Select(static query => query.XPath),
                watcher.SubscriptionQueries.Select(
                    static query => query.XPath));
        } finally {
            WatcherManager.StopWatcher(watcher.Id);
        }
    }

    private sealed class TestOperationLease : IDisposable {
        private int _disposed;

        internal bool IsDisposed =>
            Volatile.Read(ref _disposed) != 0;

        public void Dispose() {
            Interlocked.Exchange(
                ref _disposed,
                1);
        }
    }
}
