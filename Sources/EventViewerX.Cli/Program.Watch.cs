using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using EventViewerX.Reporting;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> WatchAsync(CliArguments options) {
        EventType[] types = ParseTypes(options.GetMany("type"));
        EventDefinition? definition = options.Get("definition") is string path ? EventDefinition.Load(path) : null;
        if (types.Length == 0 && definition == null || types.Length > 0 && definition != null) {
            throw new ArgumentException("watch requires exactly one of --type or --definition.");
        }
        string? machine = options.Get("collector") ?? options.Get("machine");
        bool collector = options.Get("collector") != null;
        int stopAfter = options.GetInt("stop-after");
        TimeSpan? timeout = options.Get("timeout") is string timeoutText
            ? TimeSpan.Parse(timeoutText, CultureInfo.InvariantCulture)
            : null;
        TimeSpan? interval = options.Get("interval") is string intervalText
            ? TimeSpan.Parse(intervalText, CultureInfo.InvariantCulture)
            : null;
        string? outbox = options.Get("outbox");
        string? readyFile = options.Get("ready-file");
        string? summaryFile = options.Get("summary-file");
        string? checkpointStorePath = options.Get("checkpoint-store");
        string checkpointConsumer = options.Get("checkpoint-consumer") ?? "evx-watch";
        bool ignoreStaleBookmark = options.Has("ignore-stale-bookmark");
        if (string.IsNullOrWhiteSpace(checkpointConsumer)) {
            throw new ArgumentException("Checkpoint consumer cannot be empty.", "checkpoint-consumer");
        }
        int notificationBufferCapacity = options.Get("notification-buffer-capacity") is string capacityText
            ? int.Parse(capacityText, CultureInfo.InvariantCulture)
            : 4096;
        int deliveryQueueCapacity = options.Get("delivery-queue-capacity") is string queueCapacityText
            ? int.Parse(queueCapacityText, CultureInfo.InvariantCulture)
            : 4096;
        int deadLetterAfter = options.Get("dead-letter-after") is string deadLetterText
            ? int.Parse(deadLetterText, CultureInfo.InvariantCulture)
            : 5;
        TimeSpan retryDelay = options.Get("retry-delay") is string retryDelayText
            ? TimeSpan.Parse(retryDelayText, CultureInfo.InvariantCulture)
            : TimeSpan.FromMinutes(1);
        TimeSpan maximumRetryDelay = options.Get("maximum-retry-delay") is string maximumRetryDelayText
            ? TimeSpan.Parse(maximumRetryDelayText, CultureInfo.InvariantCulture)
            : TimeSpan.FromHours(1);
        var retryPolicy = new EventNotificationRetryPolicy {
            InitialDelay = retryDelay,
            MaximumDelay = maximumRetryDelay
        };
        retryPolicy.Validate();
        if (notificationBufferCapacity <= 0) {
            throw new ArgumentOutOfRangeException(
                "notification-buffer-capacity",
                "Notification buffer capacity must be greater than zero.");
        }
        if (deliveryQueueCapacity <= 0) {
            throw new ArgumentOutOfRangeException(
                "delivery-queue-capacity",
                "Delivery queue capacity must be greater than zero.");
        }
        if (deadLetterAfter <= 0) {
            throw new ArgumentOutOfRangeException(
                "dead-letter-after",
                "Dead-letter attempt count must be greater than zero.");
        }
        using StreamWriter? jsonLines = CreateJsonLinesWriter(options.Get("jsonl"));
        SmtpNotificationProfile? mailProfile = options.Get("mail-profile") is string profilePath
            ? SmtpNotificationProfile.Load(profilePath)
            : null;
        bool bufferNotifications = !string.IsNullOrWhiteSpace(outbox) || mailProfile != null;
        EventStore? checkpointStore = string.IsNullOrWhiteSpace(checkpointStorePath)
            ? null
            : new EventStore(checkpointStorePath!);
        var buffer = new List<WatchBufferedNotification>();
        var bufferLock = new object();
        var flushTaskLock = new object();
        using var flushGate = new SemaphoreSlim(1, 1);
        Task pendingFlush = Task.CompletedTask;
        List<WatchBufferedNotification>? activeBatch = null;
        string? activeBatchStem = null;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        int processed = 0;
        int deliveredBatches = 0;
        int resumedBatches = 0;
        int deadLetterBatches = 0;
        EventTypeProjectionPlan? projectionPlan = types.Length == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(types);
        IReadOnlyList<EventType> leaves = projectionPlan?.ExpandedTypes ?? Array.Empty<EventType>();

        async Task FlushAsync() {
            await flushGate.WaitAsync().ConfigureAwait(false);
            try {
                List<WatchBufferedNotification> batch;
                string batchStem;
                lock (bufferLock) {
                    if (activeBatch == null) {
                        if (buffer.Count == 0) {
                            return;
                        }
                        activeBatch = buffer.ToList();
                        activeBatchStem = $"EventViewerX-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
                    }
                    batch = activeBatch;
                    batchStem = activeBatchStem!;
                }
                if (string.IsNullOrWhiteSpace(outbox) && mailProfile == null) {
                    return;
                }
                EventReport report = EventReportEngine.Create(
                    batch.Select(static item => item.Projected).ToArray(),
                    options.Get("title") ?? "EventViewerX notification");
                EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report).ConfigureAwait(false);
                EventNotificationOutboxBatch? durableBatch = null;
                if (!string.IsNullOrWhiteSpace(outbox)) {
                    EventNotificationOutbox.Save(outbox!, batchStem, report, email, batch.Count);
                    durableBatch = EventNotificationOutbox.GetPending(outbox!)
                        .Single(candidate => string.Equals(
                            candidate.Manifest.BatchId,
                            batchStem,
                            StringComparison.Ordinal));
                }
                if (mailProfile != null) {
                    try {
                        await mailProfile.SendAsync(email, report.Title).ConfigureAwait(false);
                        if (durableBatch != null) {
                            EventNotificationOutbox.MarkDelivered(durableBatch);
                        }
                    } catch (Exception exception) {
                        if (durableBatch != null) {
                            EventNotificationOutbox.RecordFailure(durableBatch, exception);
                        }
                        throw;
                    }
                }
                await AdvanceCheckpointsAsync(batch.Select(static item => item.Delivery)).ConfigureAwait(false);
                lock (bufferLock) {
                    buffer.RemoveRange(0, batch.Count);
                    activeBatch = null;
                    activeBatchStem = null;
                }
                Interlocked.Increment(ref deliveredBatches);
            } finally {
                flushGate.Release();
            }
        }

        async Task AdvanceCheckpointsAsync(IEnumerable<WatchDelivery> deliveries) {
            if (checkpointStore == null) {
                return;
            }
            foreach (IGrouping<WatchCheckpointContext, WatchDelivery> group in deliveries
                         .Where(static delivery => !string.IsNullOrWhiteSpace(delivery.Source.BookmarkXml))
                         .GroupBy(static delivery => delivery.Checkpoint)) {
                WatchDelivery newest = group.Last();
                EventStoreCheckpoint next = new() {
                    Consumer = checkpointConsumer,
                    Computer = newest.Checkpoint.Computer,
                    Container = newest.Checkpoint.Container,
                    RecordId = newest.Source.RecordId,
                    BookmarkXml = newest.Source.BookmarkXml,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                newest.Checkpoint.Current = await checkpointStore.AdvanceCheckpointAsync(
                    next,
                    newest.Checkpoint.Current).ConfigureAwait(false);
            }
        }

        async Task ResumeOutboxAsync() {
            if (string.IsNullOrWhiteSpace(outbox) || mailProfile == null) {
                return;
            }
            foreach (EventNotificationOutboxBatch batch in EventNotificationOutbox.GetPending(outbox!)) {
                if (batch.Delivery.FailedAttempts >= deadLetterAfter) {
                    EventNotificationOutbox.MoveToDeadLetter(batch);
                    Interlocked.Increment(ref deadLetterBatches);
                    continue;
                }
                if (!retryPolicy.IsReady(batch.Delivery)) {
                    continue;
                }
                try {
                    string title = string.IsNullOrWhiteSpace(batch.Manifest.Title)
                        ? "EventViewerX notification"
                        : batch.Manifest.Title;
                    await mailProfile.SendAsync(batch.Html, batch.PlainText, title).ConfigureAwait(false);
                    EventNotificationOutbox.MarkDelivered(batch);
                    Interlocked.Increment(ref resumedBatches);
                } catch (Exception exception) {
                    EventNotificationOutbox.RecordFailure(batch, exception);
                    throw;
                }
            }
        }

        void QueueFlush() {
            lock (flushTaskLock) {
                pendingFlush = FlushAfterAsync(pendingFlush);
            }
        }

        Task QueueFlushAndWaitAsync() {
            lock (flushTaskLock) {
                pendingFlush = FlushAfterAsync(pendingFlush);
                return pendingFlush;
            }
        }

        async Task FlushAfterAsync(Task previous) {
            try {
                await previous.ConfigureAwait(false);
            } catch {
                // The first failure is already propagated through the completion source.
            }
            try {
                await FlushAsync().ConfigureAwait(false);
            } catch (Exception exception) {
                completed.TrySetException(exception);
                throw;
            }
        }

        async ValueTask ProcessAsync(WatchDelivery delivery, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopAfter > 0 && Volatile.Read(ref processed) >= stopAfter) {
                return;
            }
            EventObject source = delivery.Source;
            object? projected = definition != null
                ? EventDefinitionEngine.CreateRecord(definition, source)
                : EventTypeCatalog.CreateEventRule(source, projectionPlan!);
            if (projected == null) {
                return;
            }
            string serialized = JsonSerializer.Serialize(EventReportEngine.CreateRow(projected), JsonOptions);
            if (jsonLines != null) {
                lock (jsonLines) {
                    jsonLines.WriteLine(serialized);
                }
            } else {
                lock (Console.Out) {
                    Console.WriteLine(serialized);
                }
            }
            if (bufferNotifications) {
                bool accepted;
                lock (bufferLock) {
                    accepted = buffer.Count < notificationBufferCapacity;
                    if (accepted) {
                        buffer.Add(new WatchBufferedNotification(projected, delivery));
                    }
                }
                if (!accepted) {
                    throw new InvalidOperationException(
                        $"The notification buffer reached its capacity of {notificationBufferCapacity} events before delivery completed.");
                }
                if (interval == null) {
                    QueueFlush();
                }
            } else {
                jsonLines?.Flush();
                await AdvanceCheckpointsAsync(new[] { delivery }).ConfigureAwait(false);
            }
            int count = Interlocked.Increment(ref processed);
            if (stopAfter > 0 && count >= stopAfter) {
                completed.TrySetResult(true);
            }
        }

        await using var deliveryQueue = new EventDeliveryQueue<WatchDelivery>(
            deliveryQueueCapacity,
            ProcessAsync);

        void Accept(EventObject source, WatchCheckpointContext checkpoint) {
            Interlocked.Increment(ref received);
            if (!deliveryQueue.TryWrite(new WatchDelivery(source, checkpoint))) {
                completed.TrySetException(new InvalidOperationException(
                    $"The watcher delivery queue reached its capacity of {deliveryQueueCapacity} events. " +
                    "No event was silently discarded; the watcher is stopping."));
                return;
            }
        }

        IReadOnlyList<(string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> Providers)> sources = definition != null
            ? definition.Sources.Select(static source => (source.LogName, source.EventIds, source.ProviderNames)).ToArray()
            : EventTypeCatalog.GetSources(types).Select(static source =>
                (source.LogName, source.EventIds, (IReadOnlyList<string>)Array.Empty<string>())).ToArray();
        var watchers = new List<WatcherInfo>();
        DateTime startedUtc = DateTime.UtcNow;
        try {
            await ResumeOutboxAsync().ConfigureAwait(false);
            foreach (var source in sources) {
                string targetLog = collector ? "ForwardedEvents" : source.LogName;
                string xpath = EventDefinitionCompiler.BuildSourceXPath(source.LogName, source.EventIds, source.Providers, collector);
                string targetComputer = string.IsNullOrWhiteSpace(machine) ? Environment.MachineName : machine!;
                string checkpointContainer = CreateWatchCheckpointContainer(targetLog, xpath);
                EventStoreCheckpoint? savedCheckpoint = checkpointStore == null
                    ? null
                    : await checkpointStore.GetCheckpointAsync(
                        checkpointConsumer,
                        targetComputer,
                        checkpointContainer).ConfigureAwait(false);
                var checkpoint = new WatchCheckpointContext(
                    targetComputer,
                    checkpointContainer,
                    savedCheckpoint);
                IReadOnlyList<EventLogSubscriptionQuery> queries = EventSubscriptionPlanner.CreateQueries(new EventSubscriptionDefinition {
                    LogName = targetLog,
                    MachineName = machine,
                    FilterXPath = xpath,
                    ReadMode = EventReadMode.StructuredDataAndMessage,
                    Start = savedCheckpoint?.BookmarkXml == null
                        ? EventLogSubscriptionStart.Future
                        : EventLogSubscriptionStart.AfterBookmark,
                    BookmarkXml = savedCheckpoint?.BookmarkXml,
                    StrictBookmark = !ignoreStaleBookmark
                });
                WatcherInfo watcher = WatcherManager.StartWatcher(
                    null,
                    queries,
                    eventObject => Accept(eventObject, checkpoint),
                    namedEvents: leaves);
                watcher.SubscriptionFailed += (_, failure) => {
                    if (failure.Terminal) {
                        completed.TrySetException(failure.Exception);
                    }
                };
                watcher.ActionException += (_, exception) =>
                    completed.TrySetException(exception);
                if (watcher.LastSubscriptionFailure is { Terminal: true } startupFailure) {
                    completed.TrySetException(startupFailure.Exception);
                }
                watchers.Add(watcher);
            }
            if (!string.IsNullOrWhiteSpace(readyFile)) {
                WriteJsonFileAtomically(readyFile!, new {
                    Ready = true,
                    ProcessId = Environment.ProcessId,
                    StartedUtc = startedUtc,
                    SourceCount = sources.Count,
                    Type = types.Select(static type => type.ToString()).ToArray(),
                    Definition = definition?.Name
                });
            }
            using var timerCancellation = new CancellationTokenSource();
            Task timerTask = interval.HasValue
                ? PeriodicFlushAsync(interval.Value, QueueFlushAndWaitAsync, timerCancellation.Token)
                : Task.CompletedTask;
            ConsoleCancelEventHandler handler = (_, eventArgs) => {
                eventArgs.Cancel = true;
                completed.TrySetResult(true);
            };
            Console.CancelKeyPress += handler;
            try {
                Task wait = Task.WhenAny(completed.Task, deliveryQueue.Completion).Unwrap();
                if (timeout.HasValue) {
                    Task finished = await Task.WhenAny(wait, Task.Delay(timeout.Value)).ConfigureAwait(false);
                    if (finished == wait) {
                        await wait.ConfigureAwait(false);
                    }
                } else {
                    await wait.ConfigureAwait(false);
                }
            } finally {
                foreach (WatcherInfo watcher in watchers) {
                    watcher.Dispose();
                }
                Console.CancelKeyPress -= handler;
                timerCancellation.Cancel();
                try { await timerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            deliveryQueue.Complete();
            await deliveryQueue.Completion.ConfigureAwait(false);
            Task queued;
            lock (flushTaskLock) {
                queued = pendingFlush;
            }
            await queued.ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);
            jsonLines?.Flush();
            if (!string.IsNullOrWhiteSpace(summaryFile)) {
                EventDeliveryQueueSnapshot queueHealth = deliveryQueue.GetSnapshot();
                EventNotificationOutboxHealth? outboxHealth = string.IsNullOrWhiteSpace(outbox)
                    ? null
                    : EventNotificationOutbox.GetHealth(outbox!);
                int pendingNotifications;
                lock (bufferLock) {
                    pendingNotifications = buffer.Count;
                }
                WriteJsonFileAtomically(summaryFile!, new {
                    Received = Volatile.Read(ref received),
                    Observed = Volatile.Read(ref received),
                    Enqueued = queueHealth.Accepted,
                    Processed = Volatile.Read(ref processed),
                    StartedUtc = startedUtc,
                    CompletedUtc = DateTime.UtcNow,
                    StopAfter = stopAfter,
                    SourceCount = sources.Count,
                    CheckpointStore = checkpointStore?.Path,
                    CheckpointConsumer = checkpointStore == null ? null : checkpointConsumer,
                    DeliveryQueueCapacity = queueHealth.Capacity,
                    DeliveryQueueHighWatermark = queueHealth.HighWatermark,
                    DeliveryQueuePending = queueHealth.Pending,
                    DeliveryQueueOldestPendingUtc = queueHealth.OldestPendingUtc,
                    DeliveryQueueOldestPendingSeconds = queueHealth.OldestPendingAge.TotalSeconds,
                    NotificationBufferCapacity = notificationBufferCapacity,
                    PendingNotifications = pendingNotifications,
                    DeliveredBatches = Volatile.Read(ref deliveredBatches),
                    ResumedBatches = Volatile.Read(ref resumedBatches),
                    DeadLetterBatches = Volatile.Read(ref deadLetterBatches),
                    OutboxPendingBatches = outboxHealth?.PendingBatches,
                    OutboxFailedAttempts = outboxHealth?.FailedAttempts,
                    OutboxOldestPendingUtc = outboxHealth?.OldestPendingUtc,
                    OutboxOldestPendingSeconds = outboxHealth?.OldestPendingAge.TotalSeconds
                });
            }
        } finally {
            foreach (WatcherInfo watcher in watchers) {
                watcher.Dispose();
            }
        }
        return 0;
    }

    private static string CreateWatchCheckpointContainer(string logName, string xpath) {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(logName + "\n" + xpath));
        return logName + "|" + BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static void WriteJsonFileAtomically(string path, object value) {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        } finally {
            if (File.Exists(temporaryPath)) {
                File.Delete(temporaryPath);
            }
        }
    }

    private static StreamWriter? CreateJsonLinesWriter(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
        return new StreamWriter(
            new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false),
            bufferSize: 65536);
    }

    private static async Task PeriodicFlushAsync(TimeSpan interval, Func<Task> flush, CancellationToken cancellationToken) {
        if (interval <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
        while (true) {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            await flush().ConfigureAwait(false);
        }
    }

    private sealed class WatchCheckpointContext {
        internal WatchCheckpointContext(string computer, string container, EventStoreCheckpoint? current) {
            Computer = computer;
            Container = container;
            Current = current;
        }

        internal string Computer { get; }
        internal string Container { get; }
        internal EventStoreCheckpoint? Current { get; set; }
    }

    private sealed class WatchDelivery {
        internal WatchDelivery(EventObject source, WatchCheckpointContext checkpoint) {
            Source = source;
            Checkpoint = checkpoint;
        }

        internal EventObject Source { get; }
        internal WatchCheckpointContext Checkpoint { get; }
    }

    private sealed class WatchBufferedNotification {
        internal WatchBufferedNotification(object projected, WatchDelivery delivery) {
            Projected = projected;
            Delivery = delivery;
        }

        internal object Projected { get; }
        internal WatchDelivery Delivery { get; }
    }
}
