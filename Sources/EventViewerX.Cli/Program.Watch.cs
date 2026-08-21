using System.Text;
using System.Text.Json;
using System.Globalization;
using EventViewerX.Reporting;

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
        using StreamWriter? jsonLines = CreateJsonLinesWriter(options.Get("jsonl"));
        SmtpNotificationProfile? mailProfile = options.Get("mail-profile") is string profilePath
            ? SmtpNotificationProfile.Load(profilePath)
            : null;
        bool bufferNotifications = !string.IsNullOrWhiteSpace(outbox) || mailProfile != null;
        var buffer = new List<object>();
        var bufferLock = new object();
        var flushTaskLock = new object();
        Task pendingFlush = Task.CompletedTask;
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        IReadOnlyList<EventType> leaves = types.Length == 0 ? Array.Empty<EventType>() : EventTypeCatalog.Expand(types);

        async Task FlushAsync() {
            List<object> batch;
            lock (bufferLock) {
                if (buffer.Count == 0) {
                    return;
                }
                batch = buffer.ToList();
                buffer.Clear();
            }
            if (string.IsNullOrWhiteSpace(outbox) && mailProfile == null) {
                return;
            }
            EventReport report = EventReportEngine.Create(batch, options.Get("title") ?? "EventViewerX notification");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(outbox)) {
                string folder = Path.GetFullPath(outbox!);
                Directory.CreateDirectory(folder);
                string stem = $"EventViewerX-{DateTime.Now:yyyyMMdd-HHmmssfff}";
                EventReportHtmlRenderer.Save(report, Path.Combine(folder, stem + ".html"));
                await File.WriteAllTextAsync(Path.Combine(folder, stem + ".email.html"), email.Html, new UTF8Encoding(false)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(folder, stem + ".email.txt"), email.PlainText, new UTF8Encoding(false)).ConfigureAwait(false);
            }
            if (mailProfile != null) {
                await mailProfile.SendAsync(email, report.Title).ConfigureAwait(false);
            }
        }

        void QueueFlush() {
            lock (flushTaskLock) {
                pendingFlush = FlushAfterAsync(pendingFlush);
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

        void Accept(EventObject source) {
            object? projected = definition != null
                ? EventDefinitionEngine.CreateRecord(definition, source)
                : EventTypeCatalog.CreateEventRule(source, leaves.ToList());
            if (projected == null) {
                return;
            }
            int count = Interlocked.Increment(ref received);
            if (stopAfter > 0 && count > stopAfter) {
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
                lock (bufferLock) {
                    buffer.Add(projected);
                }
                if (interval == null) {
                    QueueFlush();
                }
            }
            if (stopAfter > 0 && count >= stopAfter) {
                completed.TrySetResult(true);
            }
        }

        IReadOnlyList<(string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> Providers)> sources = definition != null
            ? definition.Sources.Select(static source => (source.LogName, source.EventIds, source.ProviderNames)).ToArray()
            : EventTypeCatalog.GetSources(types).Select(static source =>
                (source.LogName, source.EventIds, (IReadOnlyList<string>)Array.Empty<string>())).ToArray();
        var watchers = new List<WatcherInfo>();
        DateTime startedUtc = DateTime.UtcNow;
        try {
            foreach (var source in sources) {
                string targetLog = collector ? "ForwardedEvents" : source.LogName;
                string xpath = EventDefinitionCompiler.BuildSourceXPath(source.LogName, source.EventIds, source.Providers, collector);
                IReadOnlyList<EventLogSubscriptionQuery> queries = EventSubscriptionPlanner.CreateQueries(new EventSubscriptionDefinition {
                    LogName = targetLog,
                    MachineName = machine,
                    FilterXPath = xpath,
                    ReadMode = EventReadMode.StructuredDataAndMessage,
                    Start = EventLogSubscriptionStart.Future
                });
                watchers.Add(WatcherManager.StartWatcher(null, queries, Accept, namedEvents: leaves));
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
                ? PeriodicFlushAsync(interval.Value, FlushAsync, timerCancellation.Token)
                : Task.CompletedTask;
            ConsoleCancelEventHandler handler = (_, eventArgs) => {
                eventArgs.Cancel = true;
                completed.TrySetResult(true);
            };
            Console.CancelKeyPress += handler;
            try {
                Task wait = completed.Task;
                if (timeout.HasValue) {
                    await Task.WhenAny(wait, Task.Delay(timeout.Value)).ConfigureAwait(false);
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
            Task queued;
            lock (flushTaskLock) {
                queued = pendingFlush;
            }
            await queued.ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);
            jsonLines?.Flush();
            if (!string.IsNullOrWhiteSpace(summaryFile)) {
                WriteJsonFileAtomically(summaryFile!, new {
                    Received = Math.Min(Volatile.Read(ref received), stopAfter > 0 ? stopAfter : int.MaxValue),
                    StartedUtc = startedUtc,
                    CompletedUtc = DateTime.UtcNow,
                    StopAfter = stopAfter,
                    SourceCount = sources.Count
                });
            }
        } finally {
            foreach (WatcherInfo watcher in watchers) {
                watcher.Dispose();
            }
        }
        return 0;
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
}
