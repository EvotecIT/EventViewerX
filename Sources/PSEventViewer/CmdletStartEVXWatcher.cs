using EventViewerX;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Globalization;

namespace PSEventViewer {
    /// <summary>
    /// <para type="synopsis">Starts real-time monitoring of Windows Event Logs with customizable filters and actions.</para>
    /// <para type="description">Supports explicit event IDs or EventType, provider-side filtering, optional staging events, auto-stop conditions, and a callback for each match.</para>
    /// </summary>
    /// <example>
    ///   <summary>Watch security log for logon failures</summary>
    ///   <code>Start-EVXWatcher -MachineName DC1 -LogName Security -EventId 4625 -Action { Write-Host "Failed logon:" $_.MessageSubject }</code>
    ///   <para>Streams failed logons and prints a summary.</para>
    /// </example>
    /// <example>
    ///   <summary>Use EventType for AD lockouts</summary>
    ///   <code>Start-EVXWatcher -MachineName DC1 -Type ADUserLockouts -Action { $_ | Write-Output }</code>
    ///   <para>Triggers an alert when any AD lockout occurs.</para>
    /// </example>
    /// <example>
    ///   <summary>Stop after first hit</summary>
    ///   <code>Start-EVXWatcher -MachineName SRV1 -LogName System -EventId 41 -StopOnMatch -Action { $_ | Out-File crash.txt }</code>
    ///   <para>Captures the first critical kernel-power event then exits.</para>
    /// </example>
    /// <example>
    ///   <summary>Limit runtime</summary>
    ///   <code>Start-EVXWatcher -MachineName SRV1 -LogName Application -EventId 1000 -TimeOut (New-TimeSpan -Minutes 15) -Action { $_.WriteToHost() }</code>
    ///   <para>Watches for 15 minutes and then stops automatically.</para>
    /// </example>
    [Cmdlet(
        VerbsLifecycle.Start,
        "EVXWatcher",
        DefaultParameterSetName = "EventId")]
    [OutputType(typeof(WatcherInfo))]
    public sealed class CmdletStartEVXWatcher : AsyncPSCmdlet {

        /// <summary>
        /// Optional computer to monitor. The local computer is used by default.
        /// </summary>
        [Parameter]
        public string? MachineName { get; set; }

        /// <summary>
        /// Name of the log to watch on the specified machine.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "EventId")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "FilterHashtable")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Filter")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "FilterXPath")]
        public string LogName { get; set; } = null!;

        /// <summary>
        /// Array of event identifiers to monitor.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "EventId")]
        [ValidateRange(0, ushort.MaxValue)]
        public int[] EventId { get; set; } = Array.Empty<int>();

        /// <summary>
        /// One or more built-in typed event definitions to monitor.
        /// </summary>
        [Alias("NamedEvent", "NamedEvents")]
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Type")]
        public EventType[] Type { get; set; } = Array.Empty<EventType>();

        /// <summary>Custom EventViewerX definition instance or JSON file path.</summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Definition")]
        public object? Definition { get; set; }

        /// <summary>Windows Event Collector computer whose ForwardedEvents channel should be monitored.</summary>
        [Parameter(ParameterSetName = "Type")]
        [Parameter(ParameterSetName = "Definition")]
        public string? Collector { get; set; }

        /// <summary>
        /// Event predicates using the same keys as Get-EVXEvent -FilterHashtable.
        /// LogName and Path are not included because this watcher targets one LogName.
        /// </summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ParameterSetName = "FilterHashtable")]
        public Hashtable? FilterHashtable { get; set; }

        /// <summary>Reusable typed filter produced by New-EVXFilter or EventViewerX.</summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ParameterSetName = "Filter")]
        public EventFilter? Filter { get; set; }

        /// <summary>Native Windows Event Log XPath applied by the subscription.</summary>
        [Parameter(
            Mandatory = true,
            Position = 1,
            ParameterSetName = "FilterXPath")]
        public string? FilterXPath { get; set; }

        /// <summary>
        /// Enables staging mode which also watches for event ID 350.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "EventId")]
        public SwitchParameter Staging { get; set; }

        /// <summary>Credentials used for a remote native subscription.</summary>
        [Credential]
        [Parameter]
        public PSCredential? Credential { get; set; }

        /// <summary>Authentication package used for a remote subscription.</summary>
        [Parameter]
        public EventLogAuthentication Authentication { get; set; }

        /// <summary>Future, Oldest, or AfterBookmark subscription starting position.</summary>
        [Parameter]
        public EventLogSubscriptionStart Start { get; set; } =
            EventLogSubscriptionStart.Future;

        /// <summary>Native bookmark XML used with Start=AfterBookmark.</summary>
        [Parameter]
        public string? BookmarkXml { get; set; }

        /// <summary>Allows a stale bookmark to resume from the closest available record.</summary>
        [Parameter]
        public SwitchParameter IgnoreStaleBookmark { get; set; }

        /// <summary>Allows Windows to tolerate query errors where the native API supports it.</summary>
        [Parameter]
        public SwitchParameter TolerateQueryErrors { get; set; }

        /// <summary>Amount of event data projected for every delivered event.</summary>
        [Parameter]
        public EventReadMode ReadMode { get; set; } = EventReadMode.Full;

        /// <summary>Primary culture for message and provider-label rendering.</summary>
        [Parameter]
        public CultureInfo? MessageCulture { get; set; } =
            CultureInfo.GetCultureInfo("en-US");

        /// <summary>Fallback culture when the primary provider resources are unavailable.</summary>
        [Parameter]
        public CultureInfo? FallbackMessageCulture { get; set; } =
            CultureInfo.CurrentUICulture;

        /// <summary>Maximum detached snapshots buffered before delivery stops rather than dropping data.</summary>
        [Parameter]
        [ValidateRange(1, 65536)]
        public int BufferCapacity { get; set; } = 256;

        /// <summary>Remote native session connection timeout in milliseconds.</summary>
        [Parameter]
        [ValidateRange(1, int.MaxValue)]
        public int SessionTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Script block executed when matching events are detected.
        /// </summary>
        [Parameter(Mandatory = true, Position = 2)]
        public ScriptBlock Action { get; set; } = null!;

        /// <summary>
        /// Optional name for the watcher instance.
        /// </summary>
        [Parameter]
        public string? Name { get; set; }

        /// <summary>
        /// Stable caller-defined identity used to reuse a named watcher across recreated host delegates.
        /// Omit this parameter to reject reuse when the action delegate is not the same instance.
        /// </summary>
        [Parameter]
        public string? ActionIdentity { get; set; }

        /// <summary>
        /// Duration after which the watcher stops automatically.
        /// </summary>
        [Parameter]
        public TimeSpan? TimeOut { get; set; }

        /// <summary>
        /// When set, the watcher stops after the first matching event.
        /// </summary>
        [Parameter]
        public SwitchParameter StopOnMatch { get; set; }

        /// <summary>
        /// Stops watching after processing the specified number of events.
        /// </summary>
        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public int StopAfter { get; set; }

        /// <summary>
        /// Starts the watcher based on provided filters and returns its information.
        /// </summary>
        protected override Task ProcessRecordAsync() {
            if (ParameterSetName == "Type" || ParameterSetName == "Definition") {
                return StartProjectedWatchersAsync();
            }
            var ids = new System.Collections.Generic.List<int>();
            if (ParameterSetName == "EventId" && EventId != null) {
                ids.AddRange(EventId);
            }

            if (ParameterSetName == "EventId" && ids.Count == 0) {
                throw new PSArgumentException($"No event IDs were resolved for log '{LogName}'.");
            }
            if (TimeOut.HasValue && TimeOut.Value <= TimeSpan.Zero) {
                throw new PSArgumentOutOfRangeException(nameof(TimeOut), TimeOut, "TimeOut must be greater than zero when provided.");
            }

            EventFilter? filter = ParameterSetName switch {
                "EventId" => new EventFilter {
                    EventIds = Staging
                        ? ids
                            .Append(350)
                            .Distinct()
                            .OrderBy(static id => id)
                            .ToArray()
                        : ids
                            .Distinct()
                            .OrderBy(static id => id)
                            .ToArray()
                },
                "FilterHashtable" =>
                    PowerShellEventFilterAdapter.BindWatcherFilter(
                        FilterHashtable!),
                "Filter" => Filter,
                _ => null
            };
            EventLogSubscriptionQuery[] queries =
                EventSubscriptionPlanner.CreateQueries(
                    new EventSubscriptionDefinition {
                        LogName = LogName,
                        MachineName = MachineName,
                        Credential = Credential?.GetNetworkCredential(),
                        Authentication = Authentication,
                        Filter = filter,
                        FilterXPath = ParameterSetName == "FilterXPath"
                            ? FilterXPath
                            : null,
                        Start = Start,
                        BookmarkXml = BookmarkXml,
                        StrictBookmark = !IgnoreStaleBookmark,
                        TolerateQueryErrors = TolerateQueryErrors.IsPresent,
                        ReadMode = ReadMode,
                        MessageCulture = MessageCulture,
                        FallbackMessageCulture = FallbackMessageCulture,
                        BufferCapacity = BufferCapacity,
                        RemoteConnectionTimeoutMilliseconds = SessionTimeoutMs
                    },
                    CancelToken)
                .ToArray();

            var bridge = new PowerShellWatcherEventBridge();
            PSEventManager eventManager = Events;
            string sourceIdentifier = $"PSEventViewer.Watcher.{Guid.NewGuid():N}";
            PSEventSubscriber subscriber = eventManager.SubscribeEvent(
                bridge,
                nameof(PowerShellWatcherEventBridge.EventReceived),
                sourceIdentifier,
                PSObject.AsPSObject(Action),
                PowerShellWatcherEventBridge.ActionScript,
                supportEvent: false,
                forwardEvent: false);

            WatcherInfo? watcher = null;
            Action<EventObject> publish = bridge.Publish;
            Guid watcherOwnerId = PowerShellResourceOwnerId;
            EventHandler? stoppedHandler = null;
            bool createdPowerShellWatcher = false;
            int subscriptionRemoved = 0;
            void RemovePowerShellSubscription() {
                if (Interlocked.Exchange(ref subscriptionRemoved, 1) != 0) {
                    return;
                }

                if (watcher != null && stoppedHandler != null) {
                    watcher.Stopped -= stoppedHandler;
                }
                eventManager.UnsubscribeEvent(subscriber);
            }
            bridge.AttachCleanup(RemovePowerShellSubscription);

            try {
                watcher = WatcherManager.StartWatcher(
                    Name,
                    queries,
                    publish,
                    StopOnMatch.IsPresent,
                    StopAfter,
                    TimeOut,
                    string.IsNullOrWhiteSpace(ActionIdentity) ? null : ActionIdentity!.Trim(),
                    reuseScopeIdentity: watcherOwnerId.ToString("N"),
                    namedEvents: null,
                    cancellationToken: CancelToken);
                createdPowerShellWatcher = watcher.Action.Equals(publish);
                using CancellationTokenRegistration startupCancellation =
                    createdPowerShellWatcher
                        ? CancelToken.Register(
                            static state =>
                                WatcherManager.StopWatcher(
                                    (Guid)state!),
                            watcher.Id)
                        : default;
                CancelToken.ThrowIfCancellationRequested();
                if (!createdPowerShellWatcher) {
                    RemovePowerShellSubscription();
                } else {
                    PowerShellWatcherRegistry.Register(watcherOwnerId, watcher.Id);
                    stoppedHandler = (_, _) => {
                        PowerShellWatcherRegistry.Unregister(watcherOwnerId, watcher.Id);
                        bridge.RequestCleanup(
                            synchronousWhenIdle: true);
                    };
                    watcher.Stopped += stoppedHandler;
                    if (watcher.IsStopped) {
                        PowerShellWatcherRegistry.Unregister(watcherOwnerId, watcher.Id);
                        bridge.RequestCleanup(
                            synchronousWhenIdle: true);
                    }
                }
                CancelToken.ThrowIfCancellationRequested();
                WriteObject(watcher);
            } catch {
                RemovePowerShellSubscription();
                if (createdPowerShellWatcher && watcher != null) {
                    PowerShellWatcherRegistry.Unregister(watcherOwnerId, watcher.Id);
                    WatcherManager.StopWatcher(watcher.Id);
                }
                throw;
            }
            return Task.CompletedTask;
        }

        private Task StartProjectedWatchersAsync() {
            bool custom = ParameterSetName == "Definition";
            if (!custom && (Type == null || Type.Length == 0)) {
                throw new PSArgumentException("Type requires at least one event definition.");
            }
            if (!string.IsNullOrWhiteSpace(Collector) && !string.IsNullOrWhiteSpace(MachineName)) {
                throw new PSArgumentException("Collector and MachineName are mutually exclusive.");
            }
            if (TimeOut.HasValue && TimeOut.Value <= TimeSpan.Zero) {
                throw new PSArgumentOutOfRangeException(nameof(TimeOut), TimeOut, "TimeOut must be greater than zero when provided.");
            }
            EventDefinition? definition = null;
            EventTypeProjectionPlan? projectionPlan = null;
            IReadOnlyList<EventType> leaves = Array.Empty<EventType>();
            IReadOnlyList<(string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> Providers)> sources;
            if (custom) {
                object? value = Definition;
                while (value is PSObject wrapper && wrapper.BaseObject != value) {
                    value = wrapper.BaseObject;
                }
                definition = value switch {
                    EventDefinition typed => typed,
                    string path => EventDefinition.Load(path),
                    _ => throw new PSArgumentException(
                        "Definition must be an EventDefinition instance or a JSON file path.",
                        nameof(Definition))
                };
                definition.Validate();
                sources = definition.Sources
                    .Select(static source => (
                        source.LogName,
                        source.EventIds,
                        (IReadOnlyList<string>)source.ProviderNames))
                    .ToArray();
            } else {
                projectionPlan = EventTypeCatalog.CompileProjectionPlan(Type);
                leaves = projectionPlan.ExpandedTypes;
                sources = EventTypeCatalog.GetSources(leaves)
                    .Select(static source => (
                        source.LogName,
                        source.EventIds,
                        (IReadOnlyList<string>)Array.Empty<string>()))
                    .ToArray();
            }
            var bridge = new PowerShellWatcherEventBridge();
            PSEventManager eventManager = Events;
            string sourceIdentifier = $"PSEventViewer.Watcher.{Guid.NewGuid():N}";
            PSEventSubscriber subscriber = eventManager.SubscribeEvent(
                bridge,
                nameof(PowerShellWatcherEventBridge.EventReceived),
                sourceIdentifier,
                PSObject.AsPSObject(Action),
                PowerShellWatcherEventBridge.ActionScript,
                supportEvent: false,
                forwardEvent: false);
            Guid ownerId = PowerShellResourceOwnerId;
            var ownedWatchers = new List<WatcherInfo>();
            int remaining = 0;
            int delivered = 0;
            int cleanup = 0;
            int threshold = StopOnMatch.IsPresent ? 1 : StopAfter;
            void RemoveSubscription() {
                if (Interlocked.Exchange(ref cleanup, 1) == 0) {
                    eventManager.UnsubscribeEvent(subscriber);
                }
            }
            bridge.AttachCleanup(RemoveSubscription);
            void Publish(EventObject source) {
                object? target = definition == null
                    ? EventTypeCatalog.CreateEventRule(source, projectionPlan!)
                    : EventDefinitionEngine.CreateRecord(definition, source);
                if (target == null) {
                    return;
                }
                bridge.Publish(target);
                int count = Interlocked.Increment(ref delivered);
                if (threshold > 0 && count >= threshold) {
                    WatcherInfo[] snapshot;
                    lock (ownedWatchers) {
                        snapshot = ownedWatchers.ToArray();
                    }
                    _ = Task.Run(() => {
                        foreach (WatcherInfo watcher in snapshot) {
                            WatcherManager.StopWatcher(watcher.Id);
                        }
                    });
                }
            }

            try {
                int sourceIndex = 0;
                foreach ((string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> Providers) source in sources) {
                    if (threshold > 0 && Volatile.Read(ref delivered) >= threshold) {
                        break;
                    }
                    bool collector = !string.IsNullOrWhiteSpace(Collector);
                    string targetLog = collector ? "ForwardedEvents" : source.LogName;
                    string? targetMachine = collector ? Collector : MachineName;
                    string xpath = EventDefinitionCompiler.BuildSourceXPath(
                        source.LogName,
                        source.EventIds,
                        source.Providers,
                        collector);
                    IReadOnlyList<EventLogSubscriptionQuery> queries = EventSubscriptionPlanner.CreateQueries(
                        new EventSubscriptionDefinition {
                            LogName = targetLog,
                            MachineName = targetMachine,
                            Credential = Credential?.GetNetworkCredential(),
                            Authentication = Authentication,
                            FilterXPath = xpath,
                            Start = Start,
                            BookmarkXml = BookmarkXml,
                            StrictBookmark = !IgnoreStaleBookmark,
                            TolerateQueryErrors = TolerateQueryErrors.IsPresent,
                            ReadMode = MyInvocation.BoundParameters.ContainsKey(nameof(ReadMode))
                                ? ReadMode
                                : EventReadMode.StructuredDataAndMessage,
                            MessageCulture = MessageCulture,
                            FallbackMessageCulture = FallbackMessageCulture,
                            BufferCapacity = BufferCapacity,
                            RemoteConnectionTimeoutMilliseconds = SessionTimeoutMs
                        }, CancelToken);
                    string? watcherName = string.IsNullOrWhiteSpace(Name)
                        ? null
                        : sources.Count == 1 ? Name!.Trim() : $"{Name!.Trim()}:{source.LogName}:{sourceIndex}";
                    WatcherInfo watcher = WatcherManager.StartWatcher(
                        watcherName,
                        queries,
                        Publish,
                        stopOnMatch: false,
                        stopAfter: 0,
                        TimeOut,
                        string.IsNullOrWhiteSpace(ActionIdentity) ? null : $"{ActionIdentity!.Trim()}:{source.LogName}:{sourceIndex}",
                        reuseScopeIdentity: ownerId.ToString("N"),
                        namedEvents: custom ? null : leaves,
                        cancellationToken: CancelToken);
                    bool owned = watcher.Action.Equals((Action<EventObject>)Publish);
                    if (owned) {
                        lock (ownedWatchers) {
                            ownedWatchers.Add(watcher);
                        }
                        Interlocked.Increment(ref remaining);
                        PowerShellWatcherRegistry.Register(ownerId, watcher.Id);
                        int watcherCompleted = 0;
                        void CompleteWatcher() {
                            if (Interlocked.Exchange(ref watcherCompleted, 1) != 0) {
                                return;
                            }
                            PowerShellWatcherRegistry.Unregister(ownerId, watcher.Id);
                            if (Interlocked.Decrement(ref remaining) == 0) {
                                bridge.RequestCleanup(synchronousWhenIdle: true);
                            }
                        }
                        watcher.Stopped += (_, _) => CompleteWatcher();
                        if (watcher.IsStopped) {
                            CompleteWatcher();
                        } else if (threshold > 0 && Volatile.Read(ref delivered) >= threshold) {
                            _ = Task.Run(() => WatcherManager.StopWatcher(watcher.Id));
                        }
                    }
                    WriteObject(watcher);
                    sourceIndex++;
                }
                if (Volatile.Read(ref remaining) == 0) {
                    RemoveSubscription();
                }
            } catch {
                foreach (WatcherInfo watcher in ownedWatchers) {
                    PowerShellWatcherRegistry.Unregister(ownerId, watcher.Id);
                    WatcherManager.StopWatcher(watcher.Id);
                }
                RemoveSubscription();
                throw;
            }
            return Task.CompletedTask;
        }
    }
}
