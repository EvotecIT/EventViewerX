using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX {
    /// <summary>
    /// Represents information about a running watcher instance.
    /// </summary>
    public class WatcherInfo : IDisposable {
        internal static readonly TimeSpan MaximumSupportedTimeout =
            TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

        internal WatcherInfo(
            string name,
            string machineName,
            string logName,
            List<int> eventIds,
            List<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout)
            : this(
                name,
                machineName,
                logName,
                eventIds,
                namedEvents,
                action,
                staging,
                stopOnMatch,
                stopAfter,
                timeout,
                subscriptionQuery: null) {
        }

        internal WatcherInfo(
            string name,
            string machineName,
            string logName,
            List<int> eventIds,
            List<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout,
            EventLogSubscriptionQuery? subscriptionQuery = null) {

            ValidateTimeout(timeout);
            Name = name;
            MachineName = machineName;
            LogName = logName;
            EventIds = eventIds.ToArray();
            Types = namedEvents.ToArray();
            Action = action;
            Staging = staging;
            StopOnMatch = stopOnMatch;
            StopAfter = stopAfter;
            Timeout = timeout;
            _staging = staging;
            Watcher = new WatchEvents(new InternalLogger(false));
            Watcher.Stopped += OnWatcherStopped;
            _subscriptionQueries = new[] {
                subscriptionQuery == null
                    ? CreateSubscriptionQuery(
                        machineName,
                        logName,
                        eventIds,
                        staging)
                    : CloneSubscriptionQuery(subscriptionQuery)
            };
        }

        internal WatcherInfo(
            string name,
            string machineName,
            string logName,
            List<int> eventIds,
            List<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout,
            IReadOnlyList<EventLogSubscriptionQuery> subscriptionQueries)
            : this(
                name,
                machineName,
                logName,
                eventIds,
                namedEvents,
                action,
                staging,
                stopOnMatch,
                stopAfter,
                timeout,
                subscriptionQuery: subscriptionQueries?.FirstOrDefault()) {

            if (subscriptionQueries == null ||
                subscriptionQueries.Count == 0) {
                throw new ArgumentException(
                    "At least one subscription query is required.",
                    nameof(subscriptionQueries));
            }
            _subscriptionQueries = subscriptionQueries
                .Select(CloneSubscriptionQuery)
                .ToArray();
            _eventDeduplicator =
                _subscriptionQueries.Count > 1
                    ? new EventDeliveryDeduplicator()
                    : null;
        }

        private readonly bool _staging;
        private readonly IReadOnlyList<EventLogSubscriptionQuery>
            _subscriptionQueries;
        private readonly EventDeliveryDeduplicator?
            _eventDeduplicator;
        private readonly object _stopSync = new();
        private bool _startupCancellationRequested;
        private bool _starting;
        private bool _started;
        private bool _stopRequested;
        private bool _stopped;
        private int _eventsAccepted;
        private int _stopScheduled;
        private int _startupOwnerClaimed;
        private DateTime? _endTime;
        private readonly TaskCompletionSource<ExceptionDispatchInfo?>
            _startupCompletion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Unique identifier assigned to the watcher instance.</summary>
        public Guid Id { get; } = Guid.NewGuid();
        /// <summary>User-friendly name used to find and deduplicate watchers.</summary>
        public string Name { get; }
        /// <summary>Target computer name for the watcher.</summary>
        public string MachineName { get; }
        /// <summary>Event log name being monitored.</summary>
        public string LogName { get; }
        /// <summary>Event IDs the watcher listens for.</summary>
        public IReadOnlyList<int> EventIds { get; }
        /// <summary>EventType packs that were expanded into <see cref="EventIds"/>.</summary>
        public IReadOnlyList<EventType> Types { get; }
        /// <summary>Callback invoked when a matching event arrives.</summary>
        public Action<EventObject> Action { get; }
        internal string? ActionIdentity { get; set; }
        internal string? ReuseScopeIdentity { get; set; }
        /// <summary>Whether staging event ID 350 is included in the subscription.</summary>
        public bool Staging { get; }
        /// <summary>Stops the watcher after the first match when <c>true</c>.</summary>
        public bool StopOnMatch { get; }
        /// <summary>Optional cap on number of matching events before stopping.</summary>
        public int StopAfter { get; }
        /// <summary>Optional timeout after which the watcher is stopped automatically.</summary>
        public TimeSpan? Timeout { get; }
        internal CancellationTokenSource Cancellation { get; } = new();
        internal Task? TimeoutTask { get; private set; }
        internal Action? BeforeStartupCommit { get; set; }
        /// <summary>Underlying watcher engine instance.</summary>
        public WatchEvents Watcher { get; }
        /// <summary>Complete native subscription contract owned by this watcher.</summary>
        public IReadOnlyList<EventLogSubscriptionQuery>
            SubscriptionQueries =>
                _subscriptionQueries
                    .Select(CloneSubscriptionQuery)
                    .ToArray();
        internal IReadOnlyList<EventLogSubscriptionQuery>
            SubscriptionContracts =>
                _subscriptionQueries;

        /// <summary>
        /// First native subscription contract. Use <see cref="SubscriptionQueries"/>
        /// when the watcher was partitioned across native XPath limits.
        /// </summary>
        public EventLogSubscriptionQuery SubscriptionQuery =>
            CloneSubscriptionQuery(
                _subscriptionQueries[0]);

        /// <summary>Total number of matching events accepted for delivery by this watcher.</summary>
        public int EventsFound => Volatile.Read(ref _eventsAccepted);
        /// <summary>UTC start time of the watcher.</summary>
        public DateTime StartTime => Watcher.StartTime;
        /// <summary>UTC stop time of the watcher if it has ended; otherwise <c>null</c>.</summary>
        public DateTime? EndTime {
            get {
                lock (_stopSync) {
                    return _endTime;
                }
            }
        }
        /// <summary>Whether this watcher has completed resource shutdown.</summary>
        public bool IsStopped {
            get {
                lock (_stopSync) {
                    return _stopped;
                }
            }
        }

        /// <summary>Begins monitoring and starts the optional timeout timer.</summary>
        public void Start(
            CancellationToken startupCancellationToken = default) {

            if (Interlocked.CompareExchange(
                    ref _startupOwnerClaimed,
                    1,
                    0) != 0) {
                WaitForStartup(
                    startupCancellationToken);
                return;
            }
            CompleteReservedStartup(
                startupCancellationToken);
        }

        internal bool StartupWasClaimed =>
            Volatile.Read(
                ref _startupOwnerClaimed) != 0;

        internal void ReserveStartup() {
            if (Interlocked.CompareExchange(
                    ref _startupOwnerClaimed,
                    1,
                    0) != 0) {
                throw new InvalidOperationException(
                    "The watcher startup is already reserved.");
            }
        }

        internal void CompleteReservedStartup(
            CancellationToken startupCancellationToken) {

            try {
                StartCore(
                    startupCancellationToken);
                _startupCompletion.TrySetResult(null);
            } catch (Exception exception) {
                _startupCompletion.TrySetResult(
                    ExceptionDispatchInfo.Capture(
                        exception));
                throw;
            }
        }

        private void StartCore(
            CancellationToken startupCancellationToken) {

            startupCancellationToken.ThrowIfCancellationRequested();
            lock (_stopSync) {
                if (_stopRequested ||
                    _stopped) {
                    throw new ObjectDisposedException(nameof(WatcherInfo));
                }
                if (_started) {
                    return;
                }
                if (_starting) {
                    throw new InvalidOperationException(
                        "The watcher is already starting.");
                }
                _startupCancellationRequested = false;
                _starting = true;
            }

            using CancellationTokenRegistration startupRegistration =
                startupCancellationToken.Register(
                    static state =>
                        ((WatcherInfo)state!).CancelStartup(),
                    this);
            try {
                Watcher.Watch(
                    _subscriptionQueries,
                    OnEvent,
                    Cancellation.Token);
                startupCancellationToken.ThrowIfCancellationRequested();
                BeforeStartupCommit?.Invoke();
                lock (_stopSync) {
                    _starting = false;
                    if (_startupCancellationRequested) {
                        throw new OperationCanceledException(
                            startupCancellationToken);
                    }
                    startupCancellationToken
                        .ThrowIfCancellationRequested();
                    if (_stopRequested ||
                        _stopped) {
                        throw new ObjectDisposedException(
                            nameof(WatcherInfo));
                    }
                    _started = true;
                    if (Timeout.HasValue) {
                        TimeoutTask =
                            StopAfterTimeoutAsync(
                                Timeout.Value,
                                Cancellation.Token);
                    }
                }
            } catch (Exception) when (
                startupCancellationToken.IsCancellationRequested) {
                lock (_stopSync) {
                    _starting = false;
                }
                Stop();
                throw new OperationCanceledException(
                    startupCancellationToken);
            } catch {
                lock (_stopSync) {
                    _starting = false;
                }
                throw;
            }
        }

        internal void WaitForStartup(
            CancellationToken cancellationToken) {

            _startupCompletion.Task.Wait(
                cancellationToken);
            ExceptionDispatchInfo? failure =
                _startupCompletion.Task
                    .GetAwaiter()
                    .GetResult();
            failure?.Throw();
        }

        private void CancelStartup() {
            bool cancel;
            lock (_stopSync) {
                cancel = _starting;
                if (cancel) {
                    _startupCancellationRequested = true;
                }
            }
            if (!cancel) {
                return;
            }
            try {
                Cancellation.Cancel();
            } catch (ObjectDisposedException) {
            }
        }

        private async Task StopAfterTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken) {
            try {
                await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
                Stop();
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            }
        }

        internal static void ValidateTimeout(
            TimeSpan? timeout) {

            if (timeout.HasValue &&
                timeout.Value > MaximumSupportedTimeout) {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout),
                    $"Watcher timeout cannot exceed {MaximumSupportedTimeout.TotalMilliseconds:0} milliseconds.");
            }
        }

        /// <summary>Invokes the user callback for each matching event and applies stop conditions.</summary>
        /// <param name="obj">Event object passed to the callback.</param>
        private void OnEvent(EventObject obj) {
            if (_eventDeduplicator != null &&
                !_eventDeduplicator.TryAccept(obj)) {
                return;
            }
            int acceptedEventNumber = TryAcceptEvent();
            if (acceptedEventNumber == 0) {
                return;
            }

            Exception? exCaught = null;
            try {
                Action?.Invoke(obj);
            } catch (Exception ex) {
                exCaught = ex;
                Settings._logger.WriteWarning("OnEvent callback threw: {0}", ex.Message.Trim());
            }

            if (StopOnMatch ||
                (StopAfter > 0 &&
                 acceptedEventNumber >= StopAfter)) {
                ScheduleStop();
            }

            if (exCaught != null) {
                ActionException?.Invoke(this, exCaught);
            }
        }

        private int TryAcceptEvent() {
            int deliveryLimit = StopOnMatch
                ? 1
                : StopAfter;
            if (deliveryLimit <= 0) {
                return Interlocked.Increment(
                    ref _eventsAccepted);
            }

            while (true) {
                int current = Volatile.Read(
                    ref _eventsAccepted);
                if (current >= deliveryLimit) {
                    return 0;
                }
                int next = current + 1;
                if (Interlocked.CompareExchange(
                        ref _eventsAccepted,
                        next,
                        current) == current) {
                    return next;
                }
            }
        }

        /// <summary>Raised when the user-supplied <see cref="Action"/> throws.</summary>
        public event EventHandler<Exception>? ActionException;

        /// <summary>Raised after the watcher has released its native resources.</summary>
        public event EventHandler? Stopped;

        private void ScheduleStop() {
            if (Interlocked.Exchange(ref _stopScheduled, 1) == 0) {
                _ = Task.Run(Stop);
            }
        }

        private void OnWatcherStopped(
            object? sender,
            EventArgs args) {

            ScheduleStop();
        }

        /// <summary>Stops the watcher, disposes resources, and records end time.</summary>
        public void Stop() {
            lock (_stopSync) {
                if (_stopRequested ||
                    _stopped) {
                    return;
                }
                _stopRequested = true;
            }

            try {
                Watcher.Stopped -= OnWatcherStopped;
                Cancellation.Cancel();
            } finally {
                try {
                    Watcher.Dispose();
                } finally {
                    Cancellation.Dispose();
                    lock (_stopSync) {
                        _endTime = DateTime.UtcNow;
                        _stopped = true;
                    }
                }
            }
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Stops the watcher and disposes internal cancellation token.</summary>
        public void Dispose() {
            Stop();
        }

        internal static EventLogSubscriptionQuery CreateSubscriptionQuery(
            string machineName,
            string logName,
            IReadOnlyList<int> eventIds,
            bool staging) {

            var ids = new HashSet<int>(
                EventIdValidation.Normalize(
                    eventIds,
                    nameof(eventIds)));
            if (staging) {
                ids.Add(350);
            }
            return new EventLogSubscriptionQuery(logName) {
                MachineName = machineName,
                XPath = EventFilterCompiler.BuildXPath(
                    new EventFilter {
                        EventIds = ids
                            .OrderBy(static id => id)
                            .ToArray()
                    }),
                Start = EventLogSubscriptionStart.Future,
                ReadMode = EventReadMode.Full,
                RemoteConnectionTimeoutMilliseconds =
                    Settings.SessionTimeoutMs,
                BufferCapacity = 256
            };
        }

        private static EventLogSubscriptionQuery CloneSubscriptionQuery(
            EventLogSubscriptionQuery source) {

            return new EventLogSubscriptionQuery(source.LogName) {
                MachineName = source.MachineName,
                Credential = source.Credential == null
                    ? null
                    : new System.Net.NetworkCredential(
                        source.Credential.UserName,
                        source.Credential.Password,
                        source.Credential.Domain),
                Authentication = source.Authentication,
                XPath = source.XPath,
                Start = source.Start,
                BookmarkXml = source.BookmarkXml,
                StrictBookmark = source.StrictBookmark,
                TolerateQueryErrors = source.TolerateQueryErrors,
                ReadMode = source.ReadMode,
                MessageCulture = source.MessageCulture == null
                    ? null
                    : System.Globalization.CultureInfo.GetCultureInfo(
                        source.MessageCulture.Name),
                FallbackMessageCulture =
                    source.FallbackMessageCulture == null
                        ? null
                        : System.Globalization.CultureInfo.GetCultureInfo(
                            source.FallbackMessageCulture.Name),
                BufferCapacity = source.BufferCapacity,
                RemoteConnectionTimeoutMilliseconds =
                    source.RemoteConnectionTimeoutMilliseconds
            };
        }
    }

}
