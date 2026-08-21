using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX {
    /// <summary>
    /// Watches an event log and invokes a callback for matching events.
    /// </summary>
    public sealed class WatchEvents : IDisposable {
        private static int _numberOfEventsFound;
        private readonly object _lifecycleSync = new();
        private readonly InternalLogger _instanceLogger;
        private volatile HashSet<int> _watchEventIds = new();
        private readonly List<EventLogSubscription> _subscriptions = new();
        private CancellationTokenRegistration _cancellationRegistration;
        private bool _hasCancellationRegistration;
        private EventLogSubscriptionLifetime?
            _subscriptionLifetime;
        private Action<EventObject>? _eventAction;
        private string? _machineName;
        private int _eventsFound;
        private long _lifecycleVersion;
        private bool _stoppedRaised;

        /// <summary>Total number of matching events observed by all watchers in this process.</summary>
        public static int NumberOfEventsFound => Volatile.Read(ref _numberOfEventsFound);

        /// <summary>Number of events captured during the current watch session.</summary>
        public int EventsFound => Volatile.Read(ref _eventsFound);

        /// <summary>Time when the current or most recent watch started.</summary>
        public DateTime StartTime { get; private set; }

        /// <summary>The most recent managed event snapshot observed by this watcher.</summary>
        public EventObject? LastEvent { get; private set; }

        /// <summary>The most recent asynchronous subscription failure.</summary>
        public EventLogSubscriptionFailure? LastFailure { get; private set; }

        /// <summary>Raised when the native subscription reports a recoverable or terminal failure.</summary>
        public event EventHandler<EventLogSubscriptionFailure>? SubscriptionFailed;

        /// <summary>Raised when every native subscription owned by this logical watcher has terminated.</summary>
        public event EventHandler? Stopped;

        /// <summary>Indicates whether staging event ID 350 is included.</summary>
        public bool StagingEnabled { get; private set; }

        /// <summary>Username of the account that enabled staging.</summary>
        public string? StagingEnabledBy { get; private set; }

        /// <summary>
        /// Initializes a new watcher.
        /// </summary>
        /// <param name="internalLogger">Optional logger used by watcher callbacks.</param>
        public WatchEvents(InternalLogger? internalLogger = null) {
            _instanceLogger = internalLogger ?? Settings._logger;
        }

        /// <summary>Enables console error output for this watcher.</summary>
        public bool Error {
            get => _instanceLogger.IsError;
            set => _instanceLogger.IsError = value;
        }

        /// <summary>Enables console warning output for this watcher.</summary>
        public bool Warning {
            get => _instanceLogger.IsWarning;
            set => _instanceLogger.IsWarning = value;
        }

        /// <summary>Enables console verbose output for this watcher.</summary>
        public bool Verbose {
            get => _instanceLogger.IsVerbose;
            set => _instanceLogger.IsVerbose = value;
        }

        /// <summary>Enables console debug output for this watcher.</summary>
        public bool Debug {
            get => _instanceLogger.IsDebug;
            set => _instanceLogger.IsDebug = value;
        }

        /// <summary>Enables console information output for this watcher.</summary>
        public bool Information {
            get => _instanceLogger.IsInformation;
            set => _instanceLogger.IsInformation = value;
        }

        /// <summary>Enables console progress output for this watcher.</summary>
        public bool Progress {
            get => _instanceLogger.IsProgress;
            set => _instanceLogger.IsProgress = value;
        }

        /// <summary>Resets the process-wide matching event counter.</summary>
        public static void ResetGlobalEventCount() {
            Interlocked.Exchange(ref _numberOfEventsFound, 0);
        }

        /// <summary>
        /// Starts watching for specified event IDs.
        /// </summary>
        /// <param name="machineName">Target machine name; null targets the local computer.</param>
        /// <param name="logName">Event log channel name.</param>
        /// <param name="eventId">Event identifiers from 0 through 65535 to monitor.</param>
        /// <param name="eventAction">Callback invoked for each matching event.</param>
        /// <param name="cancellationToken">Cancellation token used to stop watching.</param>
        /// <param name="staging">Whether event ID 350 is also monitored.</param>
        /// <param name="enabledBy">Account that enabled staging.</param>
        public void Watch(
            string? machineName,
            string logName,
            List<int> eventId,
            Action<EventObject>? eventAction = null,
            CancellationToken cancellationToken = default,
            bool staging = false,
            string? enabledBy = null) {

            if (string.IsNullOrWhiteSpace(logName)) {
                throw new ArgumentException("Log name cannot be null or whitespace.", nameof(logName));
            }
            if (eventId == null) {
                throw new ArgumentNullException(nameof(eventId));
            }
            cancellationToken.ThrowIfCancellationRequested();

            var ids = new HashSet<int>(
                EventIdValidation.Normalize(
                    eventId,
                    nameof(eventId)));
            if (staging) {
                ids.Add(350);
            }
            if (ids.Count == 0) {
                throw new ArgumentException(
                    "At least one event ID is required.",
                    nameof(eventId));
            }

            string xpath = EventFilterCompiler.BuildXPath(new EventFilter {
                EventIds = ids.OrderBy(static id => id).ToArray()
            });
            var query = new EventLogSubscriptionQuery(logName) {
                MachineName = machineName,
                XPath = xpath,
                Start = EventLogSubscriptionStart.Future,
                ReadMode = EventReadMode.Full,
                RemoteConnectionTimeoutMilliseconds =
                    Settings.SessionTimeoutMs,
                BufferCapacity = 256
            };
            StartSubscription(
                new[] { query },
                ids,
                eventAction,
                cancellationToken,
                staging,
                enabledBy);
        }

        /// <summary>
        /// Starts a watcher from the complete native subscription contract.
        /// The XPath is the sole event-selection authority.
        /// </summary>
        public void Watch(
            EventLogSubscriptionQuery query,
            Action<EventObject>? eventAction = null,
            CancellationToken cancellationToken = default) {

            if (query == null) {
                throw new ArgumentNullException(nameof(query));
            }
            StartSubscription(
                new[] { query },
                new HashSet<int>(),
                eventAction,
                cancellationToken,
                staging: false,
                enabledBy: null);
        }

        /// <summary>
        /// Starts a logical watcher backed by several partitioned native subscriptions.
        /// Each query must target the same machine and channel.
        /// </summary>
        public void Watch(
            IEnumerable<EventLogSubscriptionQuery> queries,
            Action<EventObject>? eventAction = null,
            CancellationToken cancellationToken = default) {

            if (queries == null) {
                throw new ArgumentNullException(nameof(queries));
            }
            EventLogSubscriptionQuery[] snapshot = queries.ToArray();
            if (snapshot.Length == 0) {
                throw new ArgumentException(
                    "At least one subscription query is required.",
                    nameof(queries));
            }
            string machineName = snapshot[0].MachineName ?? string.Empty;
            string logName = snapshot[0].LogName;
            if (snapshot.Any(query =>
                    !string.Equals(
                        query.MachineName ?? string.Empty,
                        machineName,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        query.LogName,
                        logName,
                        StringComparison.OrdinalIgnoreCase))) {
                throw new ArgumentException(
                    "Every partitioned subscription must target the same machine and channel.",
                    nameof(queries));
            }
            StartSubscription(
                snapshot,
                new HashSet<int>(),
                eventAction,
                cancellationToken,
                staging: false,
                enabledBy: null);
        }

        private void StartSubscription(
            IReadOnlyList<EventLogSubscriptionQuery> queries,
            HashSet<int> ids,
            Action<EventObject>? eventAction,
            CancellationToken cancellationToken,
            bool staging,
            string? enabledBy) {

            cancellationToken.ThrowIfCancellationRequested();
            CancellationTokenRegistration? previousRegistration;
            EventLogSubscription[] previousSubscriptions;
            long lifecycleVersion;
            lock (_lifecycleSync) {
                previousRegistration = DetachCancellationRegistration();
                previousSubscriptions = DetachSubscriptionsCore();
                lifecycleVersion = _lifecycleVersion;
            }
            previousRegistration?.Dispose();
            DisposeSubscriptions(previousSubscriptions);

            EventLogSubscription[] startupFailedSubscriptions =
                Array.Empty<EventLogSubscription>();
            ExceptionDispatchInfo? startFailure = null;
            lock (_lifecycleSync) {
                if (_lifecycleVersion != lifecycleVersion) {
                    startFailure = ExceptionDispatchInfo.Capture(
                        new InvalidOperationException(
                            "The watcher lifecycle changed while the previous subscription was stopping."));
                } else {
                    _watchEventIds = ids;
                    _eventAction = eventAction;
                    _machineName = string.IsNullOrWhiteSpace(
                        queries[0].MachineName)
                        ? Environment.MachineName
                        : queries[0].MachineName;
                    _eventsFound = 0;
                    LastEvent = null;
                    LastFailure = null;
                    StartTime = DateTime.UtcNow;
                    StagingEnabled = staging;
                    StagingEnabledBy = staging ? enabledBy ?? Environment.UserName : null;
                    var subscriptionLifetime =
                        new EventLogSubscriptionLifetime(
                            queries.Count);
                    _subscriptionLifetime =
                        subscriptionLifetime;
                    _stoppedRaised = false;

                    try {
                        for (int index = 0;
                             index < queries.Count;
                             index++) {
                            EventLogSubscriptionQuery query =
                                queries[index];
                            int subscriptionIndex = index;
                            _subscriptions.Add(new EventLogSubscription(
                                query,
                                DetectEvent,
                                failure => DetectFailure(
                                    subscriptionLifetime,
                                    subscriptionIndex,
                                    failure),
                                cancellationToken));
                        }
                    } catch (Exception exception) {
                        startupFailedSubscriptions =
                            DetachSubscriptionsCore();
                        startFailure =
                            ExceptionDispatchInfo.Capture(
                                exception);
                    }
                }
            }
            DisposeSubscriptions(startupFailedSubscriptions);
            startFailure?.Throw();

            CancellationTokenRegistration? newRegistration = null;
            try {
                if (cancellationToken.CanBeCanceled) {
                    newRegistration = cancellationToken.Register(
                        CancelWatch);
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_lifecycleSync) {
                        if (_lifecycleVersion != lifecycleVersion ||
                            _subscriptions.Count == 0) {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new InvalidOperationException("The event-log subscription stopped before cancellation registration completed.");
                        }
                        _cancellationRegistration = newRegistration.Value;
                        _hasCancellationRegistration = true;
                        newRegistration = null;
                    }
                }
                foreach (EventLogSubscriptionQuery query in queries) {
                    _instanceLogger.WriteVerbose(
                        "Created event log subscription to {0} for XPath {1}.",
                        _machineName ?? Environment.MachineName,
                        query.XPath);
                }
            } catch {
                newRegistration?.Dispose();
                CancellationTokenRegistration? failedRegistration;
                EventLogSubscription[] registrationFailedSubscriptions;
                lock (_lifecycleSync) {
                    if (_lifecycleVersion == lifecycleVersion) {
                        failedRegistration =
                            DetachCancellationRegistration();
                        registrationFailedSubscriptions =
                            DetachSubscriptionsCore();
                    } else {
                        failedRegistration = null;
                        registrationFailedSubscriptions =
                            Array.Empty<EventLogSubscription>();
                    }
                }
                failedRegistration?.Dispose();
                DisposeSubscriptions(
                    registrationFailedSubscriptions);
                throw;
            }
        }

        private void DetectFailure(
            EventLogSubscriptionLifetime subscriptionLifetime,
            int subscriptionIndex,
            EventLogSubscriptionFailure failure) {

            bool stopped = false;
            lock (_lifecycleSync) {
                if (!ReferenceEquals(
                        _subscriptionLifetime,
                        subscriptionLifetime)) {
                    return;
                }
                LastFailure = failure;
                if (failure.Terminal) {
                    stopped =
                        subscriptionLifetime.MarkTerminal(
                            subscriptionIndex);
                    if (stopped) {
                        stopped = TryMarkStoppedCore();
                    }
                }
            }
            _instanceLogger.WriteWarning(
                "Event log subscription failed: {0}",
                failure.Exception.Message.Trim());
            try {
                SubscriptionFailed?.Invoke(this, failure);
            } catch (Exception exception) {
                _instanceLogger.WriteWarning(
                    "Event watcher failure callback threw: {0}",
                    exception.Message.Trim());
            }
            if (!stopped) {
                return;
            }
            RaiseStopped();
        }

        private void DetectEvent(EventObject eventObject) {
            try {
                HashSet<int> ids = _watchEventIds;
                if (ids.Count > 0 &&
                    !ids.Contains(eventObject.Id)) {
                    return;
                }

                LastEvent = eventObject;
                Interlocked.Increment(ref _numberOfEventsFound);
                Interlocked.Increment(ref _eventsFound);
                _instanceLogger.WriteVerbose("Found event id {0} on {1}.", eventObject.Id, eventObject.MachineName);

                try {
                    _eventAction?.Invoke(eventObject);
                } catch (Exception ex) {
                    _instanceLogger.WriteWarning("Event watcher callback threw: {0}", ex.Message.Trim());
                }
            } catch (Exception ex) {
                _instanceLogger.WriteWarning("Event log subscription callback failed: {0}", ex.Message.Trim());
            }
        }

        private void CancelWatch() {
            CancellationTokenRegistration? registration;
            EventLogSubscription[] subscriptions;
            bool stopped;
            lock (_lifecycleSync) {
                stopped = TryMarkStoppedCore();
                registration = DetachCancellationRegistration();
                subscriptions = DetachSubscriptionsCore();
            }
            if (registration == null &&
                subscriptions.Length == 0 &&
                !stopped) {
                return;
            }
            _ = Task.Run(() =>
                CompleteCancellation(
                    registration,
                    subscriptions,
                    stopped));
        }

        private void CompleteCancellation(
            CancellationTokenRegistration? registration,
            EventLogSubscription[] subscriptions,
            bool stopped) {

            try {
                registration?.Dispose();
                DisposeSubscriptions(subscriptions);
            } catch (Exception exception) {
                _instanceLogger.WriteWarning(
                    "Event watcher cancellation cleanup failed: {0}",
                    exception.Message.Trim());
            } finally {
                if (stopped) {
                    RaiseStopped();
                }
            }
        }

        /// <summary>Stops watching and releases native watcher/session resources.</summary>
        public void Dispose() {
            CancellationTokenRegistration? registration;
            EventLogSubscription[] subscriptions;
            bool stopped;
            lock (_lifecycleSync) {
                stopped = TryMarkStoppedCore();
                registration = DetachCancellationRegistration();
                subscriptions = DetachSubscriptionsCore();
            }
            registration?.Dispose();
            DisposeSubscriptions(subscriptions);
            if (stopped) {
                RaiseStopped();
            }
        }

        private bool TryMarkStoppedCore() {
            if (_subscriptionLifetime == null ||
                _stoppedRaised) {
                return false;
            }
            _stoppedRaised = true;
            return true;
        }

        private void RaiseStopped() {
            try {
                Stopped?.Invoke(
                    this,
                    EventArgs.Empty);
            } catch (Exception exception) {
                _instanceLogger.WriteWarning(
                    "Event watcher stopped callback threw: {0}",
                    exception.Message.Trim());
            }
        }

        private static void DisposeSubscriptions(
            IEnumerable<EventLogSubscription> subscriptions) {

            foreach (EventLogSubscription subscription in subscriptions) {
                subscription.Dispose();
            }
        }

        private EventLogSubscription[] DetachSubscriptionsCore() {
            EventLogSubscription[] subscriptions =
                _subscriptions.ToArray();
            _subscriptions.Clear();
            _subscriptionLifetime = null;
            _watchEventIds = new HashSet<int>();
            _eventAction = null;
            StagingEnabled = false;
            StagingEnabledBy = null;
            _lifecycleVersion++;
            return subscriptions;
        }

        private CancellationTokenRegistration? DetachCancellationRegistration() {
            if (!_hasCancellationRegistration) {
                return null;
            }

            CancellationTokenRegistration registration = _cancellationRegistration;
            _cancellationRegistration = default;
            _hasCancellationRegistration = false;
            return registration;
        }
    }
}
