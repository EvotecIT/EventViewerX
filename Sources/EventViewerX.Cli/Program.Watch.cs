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
        long outboxMaximumBatchBytes = options.Get("outbox-maximum-batch-bytes") is string maximumBatchBytesText
            ? long.Parse(maximumBatchBytesText, CultureInfo.InvariantCulture)
            : 64L * 1024 * 1024;
        long outboxMaximumBytes = options.Get("outbox-maximum-bytes") is string maximumOutboxBytesText
            ? long.Parse(maximumOutboxBytesText, CultureInfo.InvariantCulture)
            : 1024L * 1024 * 1024;
        int outboxMaximumPendingBatches = options.Get("outbox-maximum-pending-batches") is string maximumPendingBatchesText
            ? int.Parse(maximumPendingBatchesText, CultureInfo.InvariantCulture)
            : 10000;
        var outboxLimits = new EventNotificationOutboxLimits(
            outboxMaximumBatchBytes,
            outboxMaximumBytes,
            outboxMaximumPendingBatches);
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
        string? terminalBatchId = null;
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

        bool CompleteActiveBatch(string batchId) {
            lock (bufferLock) {
                if (activeBatch == null ||
                    !string.Equals(activeBatchStem, batchId, StringComparison.Ordinal)) {
                    return false;
                }
                buffer.RemoveRange(0, activeBatch.Count);
                activeBatch = null;
                activeBatchStem = null;
                return buffer.Count > 0;
            }
        }

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
                if (string.Equals(terminalBatchId, batchStem, StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"Notification batch '{batchStem}' reached the dead-letter threshold; its checkpoint was not advanced.");
                }
                if (string.IsNullOrWhiteSpace(outbox) && mailProfile == null) {
                    return;
                }
                EventReport report = EventReportEngine.Create(
                    batch.Select(static item => item.Projected).ToArray(),
                    options.Get("title") ?? "EventViewerX notification");
                EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report).ConfigureAwait(false);
                EventNotificationCheckpointBoundary[] checkpointBoundaries = CreateCheckpointBoundaries(
                    batch.Select(static item => item.Delivery));
                EventNotificationOutboxBatch? durableBatch = null;
                if (!string.IsNullOrWhiteSpace(outbox)) {
                    EventNotificationOutbox.Save(
                        outbox!,
                        batchStem,
                        report,
                        email,
                        batch.Count,
                        checkpointBoundaries,
                        requiresExternalTransport: mailProfile != null,
                        limits: outboxLimits);
                    durableBatch = EventNotificationOutbox.GetPending(outbox!)
                        .Single(candidate => string.Equals(
                            candidate.Manifest.BatchId,
                            batchStem,
                            StringComparison.Ordinal));
                }
                try {
                    if (durableBatch != null) {
                        TimeSpan remaining = retryPolicy.GetRemainingDelay(durableBatch.Delivery);
                        if (remaining > TimeSpan.Zero) {
                            return;
                        }
                    }
                    if (mailProfile != null &&
                        (durableBatch == null || !durableBatch.Delivery.TransportAcknowledgedUtc.HasValue)) {
                        await mailProfile.SendAsync(email, report.Title).ConfigureAwait(false);
                        if (durableBatch != null) {
                            EventNotificationOutbox.MarkTransportAcknowledged(durableBatch);
                        }
                    } else if (mailProfile == null && durableBatch != null &&
                               !durableBatch.Delivery.TransportAcknowledgedUtc.HasValue) {
                        EventNotificationOutbox.MarkTransportAcknowledged(durableBatch);
                    }
                    await AdvanceCheckpointsAsync(batch.Select(static item => item.Delivery)).ConfigureAwait(false);
                    if (durableBatch != null) {
                        EventNotificationOutbox.MarkDelivered(durableBatch);
                    }
                } catch (Exception exception) {
                    if (durableBatch == null) {
                        throw;
                    }
                    EventNotificationOutbox.RecordFailure(durableBatch, exception);
                    return;
                }
                bool hasBufferedNotifications = CompleteActiveBatch(batchStem);
                Interlocked.Increment(ref deliveredBatches);
                if (hasBufferedNotifications) {
                    QueueFlush();
                }
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

        EventNotificationCheckpointBoundary[] CreateCheckpointBoundaries(
            IEnumerable<WatchDelivery> deliveries) {

            if (checkpointStore == null) {
                return Array.Empty<EventNotificationCheckpointBoundary>();
            }
            return deliveries
                .Where(static delivery => !string.IsNullOrWhiteSpace(delivery.Source.BookmarkXml))
                .GroupBy(static delivery => delivery.Checkpoint)
                .Select(group => {
                    WatchDelivery newest = group.Last();
                    EventStoreCheckpoint? expected = newest.Checkpoint.Current;
                    return new EventNotificationCheckpointBoundary {
                        Consumer = checkpointConsumer,
                        Computer = newest.Checkpoint.Computer,
                        Container = newest.Checkpoint.Container,
                        RecordId = newest.Source.RecordId,
                        BookmarkXml = newest.Source.BookmarkXml,
                        ExpectedExists = expected != null,
                        ExpectedRecordId = expected?.RecordId,
                        ExpectedBookmarkXml = expected?.BookmarkXml,
                        ExpectedUpdatedAtUtc = expected?.UpdatedAtUtc
                    };
                })
                .ToArray();
        }

        async Task AdvancePersistedCheckpointsAsync(
            IEnumerable<EventNotificationCheckpointBoundary> boundaries) {

            EventNotificationCheckpointBoundary[] snapshot = boundaries.ToArray();
            if (checkpointStore == null) {
                if (snapshot.Length != 0) {
                    throw new InvalidOperationException(
                        "A pending notification batch owns checkpoint boundaries, but this watcher was restarted without --checkpoint-store.");
                }
                return;
            }
            foreach (EventNotificationCheckpointBoundary boundary in snapshot) {
                EventStoreCheckpoint next = new() {
                    Consumer = boundary.Consumer,
                    Computer = boundary.Computer,
                    Container = boundary.Container,
                    RecordId = boundary.RecordId,
                    BookmarkXml = boundary.BookmarkXml,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                EventStoreCheckpoint? current = await checkpointStore.GetCheckpointAsync(
                    boundary.Consumer,
                    boundary.Computer,
                    boundary.Container).ConfigureAwait(false);
                if (CheckpointMatches(current, next)) {
                    continue;
                }
                EventStoreCheckpoint? expected = boundary.ExpectedExists
                    ? new EventStoreCheckpoint {
                        Consumer = boundary.Consumer,
                        Computer = boundary.Computer,
                        Container = boundary.Container,
                        RecordId = boundary.ExpectedRecordId,
                        BookmarkXml = boundary.ExpectedBookmarkXml,
                        UpdatedAtUtc = boundary.ExpectedUpdatedAtUtc ?? DateTime.MinValue
                    }
                    : null;
                await checkpointStore.AdvanceCheckpointAsync(next, expected).ConfigureAwait(false);
            }
        }

        async Task RefreshActiveBatchCheckpointsAsync(string batchId) {
            if (checkpointStore == null) {
                return;
            }
            WatchCheckpointContext[] contexts;
            lock (bufferLock) {
                if (activeBatch == null ||
                    !string.Equals(activeBatchStem, batchId, StringComparison.Ordinal)) {
                    return;
                }
                contexts = activeBatch
                    .Select(static item => item.Delivery.Checkpoint)
                    .Distinct()
                    .ToArray();
            }
            foreach (WatchCheckpointContext context in contexts) {
                context.Current = await checkpointStore.GetCheckpointAsync(
                    checkpointConsumer,
                    context.Computer,
                    context.Container).ConfigureAwait(false);
            }
        }

        static bool CheckpointMatches(EventStoreCheckpoint? current, EventStoreCheckpoint expected) =>
            current != null &&
            current.RecordId == expected.RecordId &&
            string.Equals(current.BookmarkXml, expected.BookmarkXml, StringComparison.Ordinal);

        async Task<TimeSpan> ResumeOutboxAsync() {
            TimeSpan idleDelay = TimeSpan.FromMinutes(1);
            if (string.IsNullOrWhiteSpace(outbox)) {
                return idleDelay;
            }
            await flushGate.WaitAsync().ConfigureAwait(false);
            try {
                TimeSpan? nextDelay = null;
                foreach (EventNotificationOutboxBatch batch in EventNotificationOutbox.GetPending(outbox!)) {
                    if (batch.Delivery.FailedAttempts >= deadLetterAfter) {
                        EventNotificationOutbox.MoveToDeadLetter(batch);
                        Interlocked.Increment(ref deadLetterBatches);
                        lock (bufferLock) {
                            if (string.Equals(activeBatchStem, batch.Manifest.BatchId, StringComparison.Ordinal)) {
                                terminalBatchId = batch.Manifest.BatchId;
                            }
                        }
                        if (string.Equals(terminalBatchId, batch.Manifest.BatchId, StringComparison.Ordinal)) {
                            completed.TrySetException(new InvalidOperationException(
                                $"Notification batch '{batch.Manifest.BatchId}' reached the dead-letter threshold; its checkpoint was not advanced."));
                        }
                        continue;
                    }
                    TimeSpan remaining = retryPolicy.GetRemainingDelay(batch.Delivery);
                    if (remaining > TimeSpan.Zero) {
                        nextDelay = !nextDelay.HasValue || remaining < nextDelay.Value
                            ? remaining
                            : nextDelay;
                        continue;
                    }
                    if (!batch.Delivery.TransportAcknowledgedUtc.HasValue &&
                        batch.Manifest.RequiresExternalTransport &&
                        mailProfile == null) {
                        throw new InvalidOperationException(
                            $"Pending notification batch '{batch.Manifest.BatchId}' requires an external transport, but this watcher was restarted without --mail-profile.");
                    }
                    if (batch.Manifest.Checkpoints.Length != 0 && checkpointStore == null) {
                        throw new InvalidOperationException(
                            $"Pending notification batch '{batch.Manifest.BatchId}' owns checkpoint boundaries, but this watcher was restarted without --checkpoint-store.");
                    }
                    try {
                        string title = string.IsNullOrWhiteSpace(batch.Manifest.Title)
                            ? "EventViewerX notification"
                            : batch.Manifest.Title;
                        if (!batch.Delivery.TransportAcknowledgedUtc.HasValue) {
                            if (batch.Manifest.RequiresExternalTransport) {
                                await mailProfile!.SendAsync(batch.Html, batch.PlainText, title).ConfigureAwait(false);
                            }
                            EventNotificationOutbox.MarkTransportAcknowledged(batch);
                        }
                        await AdvancePersistedCheckpointsAsync(batch.Manifest.Checkpoints).ConfigureAwait(false);
                        await RefreshActiveBatchCheckpointsAsync(batch.Manifest.BatchId).ConfigureAwait(false);
                        EventNotificationOutbox.MarkDelivered(batch);
                        bool hasBufferedNotifications = CompleteActiveBatch(batch.Manifest.BatchId);
                        Interlocked.Increment(ref resumedBatches);
                        if (hasBufferedNotifications) {
                            QueueFlush();
                        }
                    } catch (Exception exception) {
                        EventNotificationOutbox.RecordFailure(batch, exception);
                        EventNotificationOutboxBatch failed = EventNotificationOutbox.GetPending(outbox!)
                            .Single(candidate => string.Equals(
                                candidate.Manifest.BatchId,
                                batch.Manifest.BatchId,
                                StringComparison.Ordinal));
                        if (failed.Delivery.FailedAttempts >= deadLetterAfter) {
                            EventNotificationOutbox.MoveToDeadLetter(failed);
                            Interlocked.Increment(ref deadLetterBatches);
                            lock (bufferLock) {
                                if (string.Equals(activeBatchStem, failed.Manifest.BatchId, StringComparison.Ordinal)) {
                                    terminalBatchId = failed.Manifest.BatchId;
                                }
                            }
                            if (string.Equals(terminalBatchId, failed.Manifest.BatchId, StringComparison.Ordinal)) {
                                completed.TrySetException(new InvalidOperationException(
                                    $"Notification batch '{failed.Manifest.BatchId}' reached the dead-letter threshold; its checkpoint was not advanced."));
                            }
                            continue;
                        }
                        TimeSpan retry = retryPolicy.GetRemainingDelay(failed.Delivery);
                        nextDelay = !nextDelay.HasValue || retry < nextDelay.Value
                            ? retry
                            : nextDelay;
                    }
                }
                return nextDelay ?? idleDelay;
            } finally {
                flushGate.Release();
            }
        }

        async Task MonitorOutboxRetriesAsync(TimeSpan initialDelay, CancellationToken cancellationToken) {
            try {
                await OutboxRetryLoopAsync(initialDelay, ResumeOutboxAsync, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            } catch (Exception exception) {
                completed.TrySetException(exception);
                throw;
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
        using var backgroundCancellation = new CancellationTokenSource();
        Task timerTask = Task.CompletedTask;
        Task outboxRetryTask = Task.CompletedTask;
        try {
            TimeSpan initialOutboxRetryDelay = await ResumeOutboxAsync().ConfigureAwait(false);
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
            timerTask = interval.HasValue
                ? PeriodicFlushAsync(interval.Value, QueueFlushAndWaitAsync, backgroundCancellation.Token)
                : Task.CompletedTask;
            if (!string.IsNullOrWhiteSpace(outbox)) {
                outboxRetryTask = MonitorOutboxRetriesAsync(
                    initialOutboxRetryDelay,
                    backgroundCancellation.Token);
            }
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
                backgroundCancellation.Cancel();
                try { await timerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
                try { await outboxRetryTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
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
                    OutboxOldestPendingSeconds = outboxHealth?.OldestPendingAge.TotalSeconds,
                    OutboxTotalBytes = outboxHealth?.TotalBytes,
                    OutboxPendingBytes = outboxHealth?.PendingBytes,
                    OutboxDeliveredBytes = outboxHealth?.DeliveredBytes,
                    OutboxDeadLetterBytes = outboxHealth?.DeadLetterBytes,
                    OutboxStagingBytes = outboxHealth?.StagingBytes,
                    OutboxMaximumBatchBytes = outboxLimits.MaximumBatchBytes,
                    OutboxMaximumBytes = outboxLimits.MaximumOutboxBytes,
                    OutboxMaximumPendingBatches = outboxLimits.MaximumPendingBatches
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

    private static async Task OutboxRetryLoopAsync(
        TimeSpan initialDelay,
        Func<Task<TimeSpan>> resume,
        CancellationToken cancellationToken) {

        if (initialDelay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(initialDelay));
        }
        TimeSpan delay = initialDelay;
        while (true) {
            if (delay > TimeSpan.Zero) {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            delay = await resume().ConfigureAwait(false);
            if (delay <= TimeSpan.Zero) {
                delay = TimeSpan.FromMilliseconds(100);
            }
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
