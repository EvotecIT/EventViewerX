using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX {
    /// <summary>Manages active watcher instances.</summary>
    public static class WatcherManager {
        private static readonly ConcurrentDictionary<Guid, WatcherInfo> _watchers = new();
        private static readonly ConcurrentDictionary<string, WatcherInfo> _watchersByName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _syncRoot = new();

        /// <summary>
        /// Starts a watcher from the complete native subscription contract.
        /// </summary>
        public static WatcherInfo StartWatcher(
            string? name,
            EventLogSubscriptionQuery query,
            Action<EventObject> action,
            bool stopOnMatch = false,
            int stopAfter = 0,
            TimeSpan? timeout = null,
            string? actionIdentity = null,
            string? reuseScopeIdentity = null,
            IReadOnlyList<EventType>? namedEvents = null,
            CancellationToken cancellationToken = default) {

            if (query == null) {
                throw new ArgumentNullException(nameof(query));
            }
            return StartWatcher(
                name,
                new[] { query },
                action,
                stopOnMatch,
                stopAfter,
                timeout,
                actionIdentity,
                reuseScopeIdentity,
                namedEvents,
                cancellationToken);
        }

        /// <summary>
        /// Starts one logical watcher from partitioned native subscription contracts.
        /// </summary>
        public static WatcherInfo StartWatcher(
            string? name,
            IReadOnlyList<EventLogSubscriptionQuery> queries,
            Action<EventObject> action,
            bool stopOnMatch = false,
            int stopAfter = 0,
            TimeSpan? timeout = null,
            string? actionIdentity = null,
            string? reuseScopeIdentity = null,
            IReadOnlyList<EventType>? namedEvents = null,
            CancellationToken cancellationToken = default) {

            cancellationToken.ThrowIfCancellationRequested();
            if (queries == null || queries.Count == 0) {
                throw new ArgumentException(
                    "At least one subscription query is required.",
                    nameof(queries));
            }
            EventLogSubscriptionQuery first = queries[0];
            if (queries.Any(query =>
                    !string.Equals(
                        query.MachineName ?? string.Empty,
                        first.MachineName ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        query.LogName,
                        first.LogName,
                        StringComparison.OrdinalIgnoreCase))) {
                throw new ArgumentException(
                    "Every partitioned subscription must target the same machine and channel.",
                    nameof(queries));
            }
            return StartWatcher(
                name,
                string.IsNullOrWhiteSpace(first.MachineName)
                    ? Environment.MachineName
                    : first.MachineName!,
                first.LogName,
                new List<int>(),
                namedEvents?.ToList() ?? new List<EventType>(),
                action,
                staging: false,
                stopOnMatch,
                stopAfter,
                timeout,
                actionIdentity,
                reuseScopeIdentity,
                subscriptionQuery: first,
                subscriptionQueries: queries,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Starts a watcher for the given machine/log and returns the tracking object.
        /// If a running watcher with the same non-empty name and identical configuration exists, that instance is returned.
        /// Reusing a name for different behavior is rejected rather than silently returning the wrong watcher.
        /// </summary>
        /// <param name="name">Optional friendly name used for reuse and lookup.</param>
        /// <param name="machineName">Target computer.</param>
        /// <param name="logName">Log name to monitor.</param>
        /// <param name="eventIds">Event IDs to watch.</param>
        /// <param name="namedEvents">EventType packs expanded for discovery.</param>
        /// <param name="action">Callback invoked for each matching event.</param>
        /// <param name="staging">When true, also watches staging events (e.g., 350).</param>
        /// <param name="stopOnMatch">Stop after first match when true.</param>
        /// <param name="stopAfter">Stop after this many matches when &gt; 0.</param>
        /// <param name="timeout">Optional timeout after which the watcher stops.</param>
        /// <param name="actionIdentity">Optional stable callback identity used by hosts that recreate equivalent delegate instances.</param>
        /// <param name="reuseScopeIdentity">Optional host scope that isolates friendly-name and action reuse from other host instances.</param>
        /// <param name="subscriptionQuery">Optional complete native subscription contract. New integrations should use the dedicated overload.</param>
        /// <param name="subscriptionQueries">Optional partitioned native subscription contracts for one logical watcher.</param>
        /// <param name="cancellationToken">Token used to cancel watcher startup before it is registered as running.</param>
        /// <returns>A <see cref="WatcherInfo"/> describing the running watcher.</returns>
        public static WatcherInfo StartWatcher(
            string? name,
            string machineName,
            string logName,
            List<int> eventIds,
            List<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout,
            string? actionIdentity = null,
            string? reuseScopeIdentity = null,
            EventLogSubscriptionQuery? subscriptionQuery = null,
            IReadOnlyList<EventLogSubscriptionQuery>?
                subscriptionQueries = null,
            CancellationToken cancellationToken = default) {

            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(machineName)) {
                machineName = Environment.MachineName;
            } else {
                machineName = machineName.Trim();
            }
            if (string.IsNullOrWhiteSpace(logName)) {
                throw new ArgumentException("Log name cannot be null or whitespace.", nameof(logName));
            }
            if (eventIds is null) {
                throw new ArgumentNullException(nameof(eventIds));
            }
            if (namedEvents is null) {
                throw new ArgumentNullException(nameof(namedEvents));
            }
            if (action is null) {
                throw new ArgumentNullException(nameof(action));
            }
            if (stopAfter < 0) {
                throw new ArgumentOutOfRangeException(nameof(stopAfter), "Stop-after count cannot be negative.");
            }
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive when provided.");
            }

            logName = logName.Trim();
            name = string.IsNullOrWhiteSpace(name) ? null : name!.Trim();
            actionIdentity = string.IsNullOrWhiteSpace(actionIdentity) ? null : actionIdentity!.Trim();
            reuseScopeIdentity = string.IsNullOrWhiteSpace(reuseScopeIdentity) ? null : reuseScopeIdentity!.Trim();
            eventIds = EventIdValidation.Normalize(
                eventIds,
                nameof(eventIds));
            if (eventIds.Count == 0 &&
                subscriptionQuery == null &&
                (subscriptionQueries == null ||
                 subscriptionQueries.Count == 0)) {
                throw new ArgumentException(
                    "At least one event ID is required.",
                    nameof(eventIds));
            }
            namedEvents = namedEvents.Distinct().OrderBy(static value => value).ToList();

            WatcherInfo? reusable;
            WatcherInfo? info = null;
            string? reservedWatcherKey = null;
            lock (_syncRoot) {
                reusable = FindReusableWatcher(
                    name,
                    reuseScopeIdentity,
                    machineName,
                    logName,
                    eventIds,
                    namedEvents,
                    action,
                    staging,
                    stopOnMatch,
                    stopAfter,
                    timeout,
                    actionIdentity,
                    subscriptionQuery,
                    subscriptionQueries);
                if (reusable == null) {
                    info = subscriptionQueries == null
                        ? new WatcherInfo(name ?? string.Empty, machineName, logName, eventIds, namedEvents, action, staging, stopOnMatch, stopAfter, timeout, subscriptionQuery)
                        : new WatcherInfo(name ?? string.Empty, machineName, logName, eventIds, namedEvents, action, staging, stopOnMatch, stopAfter, timeout, subscriptionQueries);
                    info.ActionIdentity = actionIdentity;
                    info.ReuseScopeIdentity = reuseScopeIdentity;
                    info.Stopped += RemoveStoppedWatcher;
                    info.ReserveStartup();
                    _watchers.TryAdd(info.Id, info);
                    if (!string.IsNullOrEmpty(name)) {
                        reservedWatcherKey =
                            GetWatcherKey(
                                name!,
                                reuseScopeIdentity);
                        _watchersByName[reservedWatcherKey] = info;
                    }
                }
            }
            if (reusable != null) {
                if (reusable.StartupWasClaimed) {
                    reusable.WaitForStartup(
                        cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return reusable;
            }
            WatcherInfo created = info!;
            try {
                created.CompleteReservedStartup(
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return created;
            } catch {
                _watchers.TryRemove(created.Id, out _);
                if (reservedWatcherKey != null &&
                    _watchersByName.TryGetValue(
                        reservedWatcherKey,
                        out WatcherInfo? mapped) &&
                    ReferenceEquals(mapped, created)) {
                    _watchersByName.TryRemove(
                        reservedWatcherKey,
                        out _);
                }
                created.Dispose();
                throw;
            }
        }

        private static WatcherInfo? FindReusableWatcher(
            string? name,
            string? reuseScopeIdentity,
            string machineName,
            string logName,
            IReadOnlyList<int> eventIds,
            IReadOnlyList<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout,
            string? actionIdentity,
            EventLogSubscriptionQuery? subscriptionQuery,
            IReadOnlyList<EventLogSubscriptionQuery>?
                subscriptionQueries) {

            if (string.IsNullOrEmpty(name)) {
                return null;
            }
            string watcherKey =
                GetWatcherKey(
                    name!,
                    reuseScopeIdentity);
            if (_watchersByName.TryGetValue(
                    watcherKey,
                    out WatcherInfo? existingByName) &&
                !existingByName.IsStopped) {
                if (HasEquivalentConfiguration(
                        existingByName,
                        machineName,
                        logName,
                        eventIds,
                        namedEvents,
                        action,
                        staging,
                        stopOnMatch,
                        stopAfter,
                        timeout,
                        actionIdentity,
                        subscriptionQuery,
                        subscriptionQueries)) {
                    return existingByName;
                }
                throw new InvalidOperationException(
                    $"A running watcher named '{name}' already exists with different configuration.");
            }

            WatcherInfo[] sameName = _watchers.Values
                .Where(watcher =>
                    string.Equals(
                        watcher.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        watcher.ReuseScopeIdentity,
                        reuseScopeIdentity,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (sameName.Length > 1 &&
                !_watchersByName.ContainsKey(
                    watcherKey)) {
                throw new InvalidOperationException(
                    $"Multiple watchers with name '{name}' already exist.");
            }
            if (sameName.Length == 0) {
                return null;
            }
            WatcherInfo active =
                sameName.FirstOrDefault(
                    static watcher =>
                        !watcher.IsStopped) ??
                sameName[0];
            _watchersByName[watcherKey] = active;
            if (active.IsStopped) {
                return null;
            }
            if (HasEquivalentConfiguration(
                    active,
                    machineName,
                    logName,
                    eventIds,
                    namedEvents,
                    action,
                    staging,
                    stopOnMatch,
                    stopAfter,
                    timeout,
                    actionIdentity,
                    subscriptionQuery,
                    subscriptionQueries)) {
                return active;
            }
            throw new InvalidOperationException(
                $"A running watcher named '{name}' already exists with different configuration.");
        }

        private static bool HasEquivalentConfiguration(
            WatcherInfo existing,
            string machineName,
            string logName,
            IReadOnlyList<int> eventIds,
            IReadOnlyList<EventType> namedEvents,
            Action<EventObject> action,
            bool staging,
            bool stopOnMatch,
            int stopAfter,
            TimeSpan? timeout,
            string? actionIdentity,
            EventLogSubscriptionQuery? subscriptionQuery,
            IReadOnlyList<EventLogSubscriptionQuery>?
                subscriptionQueries) {

            bool actionMatches = existing.ActionIdentity != null || actionIdentity != null
                ? string.Equals(existing.ActionIdentity, actionIdentity, StringComparison.Ordinal)
                : existing.Action.Equals(action);
            IReadOnlyList<EventLogSubscriptionQuery>
                requestedSubscriptionQueries =
                    subscriptionQueries ??
                    new[] {
                        subscriptionQuery ??
                        WatcherInfo.CreateSubscriptionQuery(
                            machineName,
                            logName,
                            eventIds,
                            staging)
                    };
            return string.Equals(existing.MachineName, machineName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(existing.LogName, logName, StringComparison.OrdinalIgnoreCase) &&
                   existing.EventIds.SequenceEqual(eventIds) &&
                   existing.Types.SequenceEqual(namedEvents) &&
                   actionMatches &&
                   existing.Staging == staging &&
                   existing.StopOnMatch == stopOnMatch &&
                   existing.StopAfter == stopAfter &&
                   existing.Timeout == timeout &&
                    SubscriptionQuerySetsEqual(
                        existing.SubscriptionContracts,
                        requestedSubscriptionQueries);
        }

        private static bool SubscriptionQuerySetsEqual(
            IReadOnlyList<EventLogSubscriptionQuery> existing,
            IReadOnlyList<EventLogSubscriptionQuery> requested) {

            return existing.Count == requested.Count &&
                   existing
                       .Zip(
                           requested,
                           static (current, candidate) =>
                               SubscriptionQueriesEqual(
                                   current,
                                   candidate))
                       .All(static equal => equal);
        }

        private static bool SubscriptionQueriesEqual(
            EventLogSubscriptionQuery existing,
            EventLogSubscriptionQuery requested) {

            return string.Equals(
                       existing.LogName,
                       requested.LogName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       existing.MachineName,
                       requested.MachineName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       existing.XPath,
                       requested.XPath,
                       StringComparison.Ordinal) &&
                   existing.Authentication == requested.Authentication &&
                   existing.Start == requested.Start &&
                   string.Equals(
                       existing.BookmarkXml,
                       requested.BookmarkXml,
                       StringComparison.Ordinal) &&
                   existing.StrictBookmark == requested.StrictBookmark &&
                   existing.TolerateQueryErrors ==
                       requested.TolerateQueryErrors &&
                   existing.ReadMode == requested.ReadMode &&
                   Equals(
                       existing.MessageCulture,
                       requested.MessageCulture) &&
                   Equals(
                       existing.FallbackMessageCulture,
                       requested.FallbackMessageCulture) &&
                   existing.BufferCapacity == requested.BufferCapacity &&
                   existing.RemoteConnectionTimeoutMilliseconds ==
                       requested.RemoteConnectionTimeoutMilliseconds &&
                   EventLogCredentialIdentity.AreEqual(
                       existing.Credential,
                       requested.Credential);
        }

        private static void RemoveStoppedWatcher(object? sender, EventArgs args) {
            if (sender is not WatcherInfo info) {
                return;
            }

            _watchers.TryRemove(info.Id, out _);
            if (!string.IsNullOrEmpty(info.Name) &&
                _watchersByName.TryGetValue(GetWatcherKey(info.Name, info.ReuseScopeIdentity), out WatcherInfo? mapped) &&
                ReferenceEquals(mapped, info)) {
                _watchersByName.TryRemove(GetWatcherKey(info.Name, info.ReuseScopeIdentity), out _);
            }
        }

        private static string GetWatcherKey(string name, string? reuseScopeIdentity)
            => reuseScopeIdentity == null
                ? $"U:{name}"
                : $"S:{reuseScopeIdentity.Length}:{reuseScopeIdentity}:{name}";

        /// <summary>Returns all active watchers or those matching a specific name.</summary>
        public static IReadOnlyCollection<WatcherInfo> GetWatchers(string? name = null) {
            if (string.IsNullOrEmpty(name)) {
                return _watchers.Values.ToList();
            }
            return _watchers.Values.Where(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>Stops and removes a watcher by its identifier.</summary>
        /// <returns><c>true</c> when a watcher was stopped; otherwise <c>false</c>.</returns>
        public static bool StopWatcher(Guid id) {
            if (_watchers.TryRemove(id, out var info)) {
                info.Dispose();
                // Remove name mapping if it points to this instance or if no watcher with that name exists
                if (!string.IsNullOrEmpty(info.Name)) {
                    string watcherKey = GetWatcherKey(info.Name, info.ReuseScopeIdentity);
                    _watchersByName.TryGetValue(watcherKey, out var mapped);
                    if (mapped == null || ReferenceEquals(mapped, info)) {
                        _watchersByName.TryRemove(watcherKey, out _);
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>Stops all watchers that share the given name.</summary>
        /// <returns>The number of watchers that were stopped.</returns>
        public static int StopWatchersByName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException(
                    "Watcher name cannot be null or whitespace.",
                    nameof(name));
            }

            int stopped = 0;
            foreach (var w in GetWatchers(name)) {
                if (StopWatcher(w.Id)) {
                    stopped++;
                }
            }
            return stopped;
        }

        /// <summary>Stops every active watcher and clears internal tracking.</summary>
        /// <returns>The number of watchers that were stopped.</returns>
        public static int StopAll() {
            int stopped = 0;
            foreach (var id in _watchers.Keys.ToList()) {
                if (StopWatcher(id)) {
                    stopped++;
                }
            }
            _watchersByName.Clear();
            return stopped;
        }
    }
}
