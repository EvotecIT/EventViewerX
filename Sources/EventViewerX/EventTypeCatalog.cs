using System.Reflection;
using System.Linq.Expressions;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace EventViewerX;

/// <summary>
/// Discovers, registers, and projects event-type rules independently of typed output records.
/// </summary>
public static partial class EventTypeCatalog {
    private sealed class RuleFactoryRegistration {
        public RuleFactoryRegistration(EventType namedEvent, string logName, IReadOnlyList<int> eventIds,
            Func<EventObject, EventTypeRecord> factory, Func<EventObject, bool>? canHandle,
#if NET5_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)]
#endif
            Type? ruleType,
            int matchPriority,
            IReadOnlyList<string> providerNames) {
            Type = namedEvent;
            LogName = logName;
            EventIds = eventIds;
            Factory = factory;
            CanHandle = canHandle;
            RuleType = ruleType;
            MatchPriority = matchPriority;
            ProviderNames = providerNames;
        }

        public EventType Type { get; }
        public string LogName { get; }
        public IReadOnlyList<int> EventIds { get; }
        public Func<EventObject, EventTypeRecord> Factory { get; }
        public Func<EventObject, bool>? CanHandle { get; }
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        public Type? RuleType { get; }
        public int MatchPriority { get; }
        public IReadOnlyList<string> ProviderNames { get; }
    }

    private static readonly Dictionary<EventType, RuleFactoryRegistration> _reflectionRuleTypes = new();
    private static readonly Dictionary<EventType, int> _reflectionRulePriorities = new();
    private static readonly Dictionary<EventType, (string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> ProviderNames)> _reflectionRuleSources = new();
    private static readonly Dictionary<EventType, EventProjectorDefinition> _reflectionProjectors = new();
    private static readonly Dictionary<(int EventId, string LogName), List<Type>> _reflectionHandlers = new(EventHandlerKeyComparer.Instance);

    private static readonly Dictionary<EventType, Type> _explicitRuleTypes = new();
    private static readonly Dictionary<(int EventId, string LogName), List<Type>> _explicitHandlers = new(EventHandlerKeyComparer.Instance);

    // AOT-friendly path: explicit, delegate-based rule registration.
    private static readonly Dictionary<EventType, RuleFactoryRegistration> _ruleFactories = new();
    private static readonly Dictionary<(int EventId, string LogName), List<RuleFactoryRegistration>> _factoryHandlers = new(EventHandlerKeyComparer.Instance);

    private sealed class EventHandlerKeyComparer : IEqualityComparer<(int EventId, string LogName)> {
        internal static EventHandlerKeyComparer Instance { get; } = new();

        public bool Equals((int EventId, string LogName) x, (int EventId, string LogName) y) {
            return x.EventId == y.EventId && string.Equals(x.LogName, y.LogName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((int EventId, string LogName) value) {
            unchecked {
                return (value.EventId * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(value.LogName ?? string.Empty);
            }
        }
    }

    private static readonly object _initLock = new();
    private static volatile bool _initialized;
    private static EventRuleDiscoveryMode _discoveryMode = EventRuleDiscoveryMode.Auto;

    /// <summary>
    /// Configures rule registration behavior before the first query. Built-in rules use static factories in every mode.
    /// </summary>
    public static void Configure(EventRuleDiscoveryMode mode) {
        lock (_initLock) {
            if (_initialized) {
                throw new InvalidOperationException(
                    "EventTypeCatalog has already been initialized. Configure() must be called before first use.");
            }
            _discoveryMode = mode;
        }
    }

    /// <summary>Registers a delegate-based rule factory without discovering a runtime record schema.</summary>
    /// <param name="namedEvent">Named event identifier.</param>
    /// <param name="logName">Windows channel.</param>
    /// <param name="eventIds">Handled event identifiers.</param>
    /// <param name="factory">Typed record factory.</param>
    /// <param name="canHandle">Optional event precondition.</param>
    public static void RegisterRuleFactory(EventType namedEvent, string logName,
        IReadOnlyList<int> eventIds, Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? canHandle = null) {
        RegisterRuleFactoryCore(namedEvent, logName, eventIds, Array.Empty<string>(),
            factory, canHandle, ruleType: null, matchPriority: 0);
    }

    /// <summary>Registers a provider-scoped delegate factory without discovering a runtime record schema.</summary>
    /// <param name="namedEvent">Named event identifier.</param>
    /// <param name="logName">Windows channel.</param>
    /// <param name="eventIds">Handled event identifiers.</param>
    /// <param name="providerNames">Handled event providers.</param>
    /// <param name="factory">Typed record factory.</param>
    /// <param name="canHandle">Optional event precondition.</param>
    public static void RegisterRuleFactory(EventType namedEvent, string logName,
        IReadOnlyList<int> eventIds, IReadOnlyList<string> providerNames,
        Func<EventObject, EventTypeRecord> factory, Func<EventObject, bool>? canHandle = null) {
        RegisterRuleFactoryCore(namedEvent, logName, eventIds, providerNames,
            factory, canHandle, ruleType: null, matchPriority: 0);
    }

    /// <summary>
    /// Registers a rule factory for an event type without relying on reflection.
    /// This enables AOT-friendly ingestion of selected rules.
    /// </summary>
    /// <param name="namedEvent">Named event identifier.</param>
    /// <param name="logName">Windows log name (channel).</param>
    /// <param name="eventIds">Event IDs this rule handles.</param>
    /// <param name="factory">Factory creating a rule instance from an <see cref="EventObject"/>.</param>
    /// <param name="canHandle">Optional predicate to further validate an event before instantiation.</param>
    /// <param name="ruleType">Optional rule type used for legacy APIs returning <see cref="Type"/>.</param>
    /// <param name="matchPriority">Relative priority used when several registered projections match one event.</param>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary field types require their enumerable interface metadata to be preserved. Use built-in or declarative event definitions in trimmed consumers.")]
#endif
    public static void RegisterRuleFactory(
        EventType namedEvent,
        string logName,
        IReadOnlyList<int> eventIds,
        Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? canHandle = null,
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        Type? ruleType = null,
        int matchPriority = 0) {

        if (ruleType != null) {
            EventFieldDefinition.RegisterEnumerableFieldTypes(ruleType);
        }
        RegisterRuleFactoryCore(
            namedEvent,
            logName,
            eventIds,
            Array.Empty<string>(),
            factory,
            canHandle,
            ruleType,
            matchPriority);
    }

    /// <summary>
    /// Registers a provider-scoped rule factory for an event type without relying on reflection.
    /// </summary>
    /// <param name="namedEvent">Named event identifier.</param>
    /// <param name="logName">Windows log name (channel).</param>
    /// <param name="eventIds">Event IDs this rule handles.</param>
    /// <param name="providerNames">Event-provider names used to scope native subscriptions.</param>
    /// <param name="factory">Factory creating a rule instance from an <see cref="EventObject"/>.</param>
    /// <param name="canHandle">Optional predicate to further validate an event before instantiation.</param>
    /// <param name="ruleType">Optional rule type used for legacy APIs returning <see cref="Type"/>.</param>
    /// <param name="matchPriority">Relative priority used when several registered projections match one event.</param>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary field types require their enumerable interface metadata to be preserved. Use built-in or declarative event definitions in trimmed consumers.")]
#endif
    public static void RegisterRuleFactory(
        EventType namedEvent,
        string logName,
        IReadOnlyList<int> eventIds,
        IReadOnlyList<string> providerNames,
        Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? canHandle = null,
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        Type? ruleType = null,
        int matchPriority = 0) {

        if (ruleType != null) {
            EventFieldDefinition.RegisterEnumerableFieldTypes(ruleType);
        }
        RegisterRuleFactoryCore(
            namedEvent,
            logName,
            eventIds,
            providerNames,
            factory,
            canHandle,
            ruleType,
            matchPriority);
    }

    private static void RegisterRuleFactoryCore(
        EventType namedEvent,
        string logName,
        IReadOnlyList<int> eventIds,
        IReadOnlyList<string> providerNames,
        Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? canHandle,
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
#endif
        Type? ruleType,
        int matchPriority) {
        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException("logName cannot be null or whitespace.", nameof(logName));
        }
        if (eventIds is null || eventIds.Count == 0) {
            throw new ArgumentException("eventIds cannot be null or empty.", nameof(eventIds));
        }
        if (factory is null) {
            throw new ArgumentNullException(nameof(factory));
        }

        var normalizedLog = logName.Trim();
        var ids = eventIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0) {
            throw new ArgumentException("eventIds must contain at least one positive event id.", nameof(eventIds));
        }
        if (providerNames is null) {
            throw new ArgumentNullException(nameof(providerNames));
        }
        if (providerNames.Any(static provider => string.IsNullOrWhiteSpace(provider))) {
            throw new ArgumentException("providerNames cannot contain empty values.", nameof(providerNames));
        }
        string[] providers = providerNames
            .Select(static provider => provider.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        lock (_initLock) {
            if (_initialized) {
                throw new InvalidOperationException("Rule factories must be registered before the first event-type query.");
            }

            var reg = new RuleFactoryRegistration(
                namedEvent,
                normalizedLog,
                ids,
                factory,
                canHandle,
                ruleType,
                matchPriority,
                providers);
            _ruleFactories[namedEvent] = reg;

            if (ruleType is not null) {
                _explicitRuleTypes[namedEvent] = ruleType;
            }

            foreach (var eventId in ids) {
                var factoryKey = (eventId, normalizedLog);
                if (!_factoryHandlers.TryGetValue(factoryKey, out var factoryList)) {
                    factoryList = new List<RuleFactoryRegistration>();
                    _factoryHandlers[factoryKey] = factoryList;
                }
                if (!factoryList.Contains(reg)) {
                    factoryList.Add(reg);
                }

                if (ruleType is not null) {
                    var legacyKey = (eventId, normalizedLog);
                    if (!_explicitHandlers.TryGetValue(legacyKey, out var legacyList)) {
                        legacyList = new List<Type>();
                        _explicitHandlers[legacyKey] = legacyList;
                    }
                    if (!legacyList.Contains(ruleType)) {
                        legacyList.Add(ruleType);
                    }
                }
            }
        }
    }

    private static void EnsureInitialized() {
        if (_initialized) {
            return;
        }
        lock (_initLock) {
            if (_initialized) {
                return;
            }
            if (_discoveryMode != EventRuleDiscoveryMode.ExplicitOnly) {
                InitializeEventRulesWithReflection();
            }
            _initialized = true;
        }
    }

    /// <summary>
    /// Populates the legacy registration tables from the built-in static factories.
    /// </summary>
    private static void InitializeEventRulesWithReflection() {
        RegisterBuiltInRuleFactories(reflection: true);
    }

    /// <summary>
    /// Gets all event rule types that can handle the given event.
    /// </summary>
    public static List<Type> GetEventHandlers(int eventId, string logName) {
        var key = (eventId, logName);
        var mode = _discoveryMode;
        EnsureInitialized();

        if (mode == EventRuleDiscoveryMode.ExplicitOnly) {
            return _explicitHandlers.TryGetValue(key, out var explicitHandlers) ? new List<Type>(explicitHandlers) : new List<Type>();
        }
        if (mode == EventRuleDiscoveryMode.Reflection) {
            return _reflectionHandlers.TryGetValue(key, out var reflectionHandlers) ? new List<Type>(reflectionHandlers) : new List<Type>();
        }

        var combined = new List<Type>();
        if (_explicitHandlers.TryGetValue(key, out var explicitList)) {
            combined.AddRange(explicitList);
        }
        if (_reflectionHandlers.TryGetValue(key, out var reflectionList)) {
            foreach (var t in reflectionList) {
                if (!combined.Contains(t)) {
                    combined.Add(t);
                }
            }
        }
        return combined;
    }

    /// <summary>
    /// Gets the event rule type for an event type.
    /// </summary>
#if NET5_0_OR_GREATER
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields)]
#endif
    public static Type? GetEventRuleType(EventType namedEvent) {
        var mode = _discoveryMode;
        EnsureInitialized();

        if (mode == EventRuleDiscoveryMode.ExplicitOnly) {
            return _ruleFactories.TryGetValue(namedEvent, out RuleFactoryRegistration? registration)
                ? registration.RuleType
                : null;
        }
        if (mode == EventRuleDiscoveryMode.Reflection) {
            return _reflectionRuleTypes.TryGetValue(namedEvent, out var reflectionType) ? reflectionType.RuleType : null;
        }

        return _ruleFactories.TryGetValue(namedEvent, out RuleFactoryRegistration? registered) ? registered.RuleType
            : _reflectionRuleTypes.TryGetValue(namedEvent, out var reflection) ? reflection.RuleType
            : null;
    }

    /// <summary>
    /// Creates an event rule instance from an <see cref="EventObject"/>.
    /// </summary>
    public static EventTypeRecord? CreateEventRule(
        EventObject eventObject,
        IReadOnlyCollection<EventType> targetEventTypes) {

        if (eventObject == null) {
            throw new ArgumentNullException(nameof(eventObject));
        }
        if (targetEventTypes == null) {
            throw new ArgumentNullException(nameof(targetEventTypes));
        }

        return CreateEventRule(eventObject, CompileProjectionPlan(targetEventTypes));
    }

    /// <summary>Creates an event rule instance using an immutable precompiled projection plan.</summary>
    /// <param name="eventObject">Event to project.</param>
    /// <param name="plan">Projection plan compiled for the owning query or watcher.</param>
    /// <returns>The most specific matching typed record, or <see langword="null"/> when no rule matches.</returns>
    public static EventTypeRecord? CreateEventRule(
        EventObject eventObject,
        EventTypeProjectionPlan plan) {

        if (eventObject == null) {
            throw new ArgumentNullException(nameof(eventObject));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }

        EnsureInitialized();
        List<string>? failedRuleNames = null;
        List<Exception>? projectionErrors = null;
        string eventLog = eventObject.OriginalLogName;

        foreach (EventProjectorDefinition projector in plan.GetCandidates(eventObject.Id, eventLog)) {
            try {
                if (projector.Precondition != null && !projector.Precondition(eventObject)) {
                    continue;
                }
                EventTypeRecord instance = projector.Factory(eventObject);

                if (instance is IEventRule eventRule) {
                    if (eventRule.CanHandle(eventObject)) {
                        return instance;
                    }
                } else {
                    return instance;
                }
            } catch (Exception ex) {
                failedRuleNames ??= new List<string>();
                projectionErrors ??= new List<Exception>();
                failedRuleNames.Add(projector.Name);
                projectionErrors.Add(ex is TargetInvocationException { InnerException: not null }
                    ? ex.InnerException
                    : ex);
                continue;
            }
        }

        if (projectionErrors != null && failedRuleNames != null) {
            throw new EventRuleProjectionException(eventObject, failedRuleNames, projectionErrors);
        }

        return null;
    }

    /// <summary>Compiles immutable source routing and specificity ordering for selected event types.</summary>
    /// <param name="eventTypes">Leaf or composite event types to include.</param>
    /// <returns>A reusable projection plan.</returns>
    public static EventTypeProjectionPlan CompileProjectionPlan(IEnumerable<EventType> eventTypes) {
        if (eventTypes == null) {
            throw new ArgumentNullException(nameof(eventTypes));
        }

        EventType[] requested = eventTypes.ToArray();
        var mode = _discoveryMode;
        EnsureInitialized();
        EventType[] expanded = Expand(requested).ToArray();
        IReadOnlyList<EventType> ordered = GetOrderedExpandedCandidates(expanded, mode);
        var candidateLists = new Dictionary<EventTypeProjectionPlan.SourceKey, List<EventProjectorDefinition>>();
        foreach (EventType type in ordered) {
            if (UnavailableSources.ContainsKey(type)) { continue; }
            if (!TryGetRuleSource(
                    type,
                    mode,
                    out string logName,
                    out IReadOnlyList<int> eventIds,
                    out IReadOnlyList<string> providerNames)) {
                continue;
            }
            if (!TryCreateProjector(type, mode, out EventProjectorDefinition? projector)) {
                continue;
            }
            if (providerNames.Count > 0) {
                EventProjectorDefinition unscopedProjector = projector!;
                projector = new EventProjectorDefinition(
                    unscopedProjector.Type,
                    unscopedProjector.Name,
                    unscopedProjector.Factory,
                    eventObject =>
                        providerNames.Contains(
                            eventObject.ProviderName,
                            StringComparer.OrdinalIgnoreCase) &&
                        (unscopedProjector.Precondition == null ||
                         unscopedProjector.Precondition(eventObject)));
            }
            foreach (int eventId in eventIds) {
                var key = new EventTypeProjectionPlan.SourceKey(eventId, logName);
                if (!candidateLists.TryGetValue(key, out List<EventProjectorDefinition>? candidates)) {
                    candidates = new List<EventProjectorDefinition>();
                    candidateLists[key] = candidates;
                }
                candidates.Add(projector!);
            }
        }

        var candidatesBySource = new Dictionary<EventTypeProjectionPlan.SourceKey, EventProjectorDefinition[]>(candidateLists.Count);
        foreach (KeyValuePair<EventTypeProjectionPlan.SourceKey, List<EventProjectorDefinition>> pair in candidateLists) {
            candidatesBySource[pair.Key] = pair.Value.ToArray();
        }
        return new EventTypeProjectionPlan(requested, expanded, candidatesBySource);
    }

    private static IReadOnlyList<EventType> GetOrderedExpandedCandidates(
        IReadOnlyList<EventType> expanded,
        EventRuleDiscoveryMode mode) {

        if (expanded.Count < 2) {
            return expanded;
        }

        bool hasPriority = false;
        var candidates = new (EventType Type, int Index, int Priority)[expanded.Count];
        for (int index = 0; index < expanded.Count; index++) {
            EventType type = expanded[index];
            int priority = GetMatchPriority(type, mode);
            candidates[index] = (type, index, priority);
            hasPriority |= priority != 0;
        }
        if (!hasPriority) {
            return expanded;
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Priority)
            .ThenBy(static candidate => candidate.Index)
            .Select(static candidate => candidate.Type)
            .ToArray();
    }

    private static int GetMatchPriority(EventType type, EventRuleDiscoveryMode mode) {
        if (mode != EventRuleDiscoveryMode.Reflection &&
            _ruleFactories.TryGetValue(type, out RuleFactoryRegistration? registration)) {
            return registration.MatchPriority;
        }
        if (mode != EventRuleDiscoveryMode.ExplicitOnly &&
            _reflectionRulePriorities.TryGetValue(type, out int priority)) {
            return priority;
        }
        return 0;
    }

    private static bool TryGetRuleSource(
        EventType type,
        EventRuleDiscoveryMode mode,
        out string logName,
        out IReadOnlyList<int> eventIds) {

        if (mode != EventRuleDiscoveryMode.Reflection &&
            _ruleFactories.TryGetValue(type, out RuleFactoryRegistration? registration)) {
            logName = registration.LogName;
            eventIds = registration.EventIds;
            return true;
        }
        if (mode != EventRuleDiscoveryMode.ExplicitOnly &&
            _reflectionRuleSources.TryGetValue(type, out var source)) {
            logName = source.LogName;
            eventIds = source.EventIds;
            return true;
        }

        logName = string.Empty;
        eventIds = Array.Empty<int>();
        return false;
    }

    private static bool TryGetRuleSource(
        EventType type,
        EventRuleDiscoveryMode mode,
        out string logName,
        out IReadOnlyList<int> eventIds,
        out IReadOnlyList<string> providerNames) {

        if (mode != EventRuleDiscoveryMode.Reflection &&
            _ruleFactories.TryGetValue(type, out RuleFactoryRegistration? registration)) {
            logName = registration.LogName;
            eventIds = registration.EventIds;
            providerNames = registration.ProviderNames;
            return true;
        }
        if (mode != EventRuleDiscoveryMode.ExplicitOnly &&
            _reflectionRuleSources.TryGetValue(type, out var source)) {
            logName = source.LogName;
            eventIds = source.EventIds;
            providerNames = source.ProviderNames;
            return true;
        }

        logName = string.Empty;
        eventIds = Array.Empty<int>();
        providerNames = Array.Empty<string>();
        return false;
    }

    private static bool TryCreateProjector(
        EventType type,
        EventRuleDiscoveryMode mode,
        out EventProjectorDefinition? projector) {

        if (mode != EventRuleDiscoveryMode.Reflection &&
            _ruleFactories.TryGetValue(type, out RuleFactoryRegistration? registration)) {
            projector = new EventProjectorDefinition(
                type,
                registration.RuleType?.FullName ?? type.ToString(),
                registration.Factory,
                registration.CanHandle);
            return true;
        }
        if (mode != EventRuleDiscoveryMode.ExplicitOnly &&
            _reflectionProjectors.TryGetValue(type, out EventProjectorDefinition? reflectionProjector)) {
            projector = reflectionProjector;
            return true;
        }

        projector = null;
        return false;
    }

    /// <summary>
    /// Gets event IDs and log names for event types using rule classes.
    /// </summary>
    internal static Dictionary<string, HashSet<int>> GetSourceMap(IReadOnlyCollection<EventType> eventTypes) {
        var eventInfoDict = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (EventSourceDefinition source in GetSources(eventTypes)) {
            if (!eventInfoDict.TryGetValue(source.LogName, out HashSet<int>? ids)) {
                ids = new HashSet<int>();
                eventInfoDict[source.LogName] = ids;
            }
            ids.UnionWith(source.EventIds);
        }

        return eventInfoDict;
    }
}
