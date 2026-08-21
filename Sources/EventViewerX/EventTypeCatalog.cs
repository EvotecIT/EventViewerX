using System.Reflection;

namespace EventViewerX;

/// <summary>
/// Discovers, registers, and projects event-type rules independently of typed output records.
/// </summary>
public static partial class EventTypeCatalog {
    private sealed class RuleFactoryRegistration {
        public RuleFactoryRegistration(EventType namedEvent, string logName, IReadOnlyList<int> eventIds,
            Func<EventObject, EventTypeRecord> factory, Func<EventObject, bool>? canHandle, Type? ruleType) {
            Type = namedEvent;
            LogName = logName;
            EventIds = eventIds;
            Factory = factory;
            CanHandle = canHandle;
            RuleType = ruleType;
        }

        public EventType Type { get; }
        public string LogName { get; }
        public IReadOnlyList<int> EventIds { get; }
        public Func<EventObject, EventTypeRecord> Factory { get; }
        public Func<EventObject, bool>? CanHandle { get; }
        public Type? RuleType { get; }
    }

    private static readonly Dictionary<EventType, Type> _reflectionRuleTypes = new();
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
    /// Configures how rule discovery works. Call this once at startup (before any queries) for AOT-friendly behavior.
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
    public static void RegisterRuleFactory(
        EventType namedEvent,
        string logName,
        IReadOnlyList<int> eventIds,
        Func<EventObject, EventTypeRecord> factory,
        Func<EventObject, bool>? canHandle = null,
        Type? ruleType = null) {
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

        lock (_initLock) {
            if (_initialized) {
                throw new InvalidOperationException("Rule factories must be registered before the first event-type query.");
            }

            var reg = new RuleFactoryRegistration(namedEvent, normalizedLog, ids, factory, canHandle, ruleType);
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
    /// Discovers and registers all event rule types using reflection (legacy behavior).
    /// </summary>
    private static void InitializeEventRulesWithReflection() {
        var assembly = typeof(EventTypeRecord).Assembly;

        var eventRuleTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                   (t.IsSubclassOf(typeof(EventRuleBase)) ||
                    (t.IsSubclassOf(typeof(EventTypeRecord)) && t.GetInterfaces().Contains(typeof(IEventRule)))));

        foreach (var type in eventRuleTypes) {
            RegisterEventRuleType(type);
        }
    }

    /// <summary>
    /// Registers a single event rule type (reflection-based).
    /// </summary>
    private static void RegisterEventRuleType(Type ruleType) {
        if (ruleType.IsSubclassOf(typeof(EventRuleBase))) {
            try {
#pragma warning disable SYSLIB0050
                var instance = (EventRuleBase)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(ruleType);
#pragma warning restore SYSLIB0050
                _reflectionRuleTypes[instance.Type] = ruleType;

                foreach (var eventId in instance.EventIds) {
                    var key = (eventId, instance.LogName);
                    if (!_reflectionHandlers.ContainsKey(key)) {
                        _reflectionHandlers[key] = new List<Type>();
                    }
                    _reflectionHandlers[key].Add(ruleType);
                }
            } catch {
                return;
            }
        } else {
            var attr = ruleType.GetCustomAttribute<EventRuleAttribute>();
            if (attr != null) {
                _reflectionRuleTypes[attr.Type] = ruleType;

                foreach (var eventId in attr.EventIds) {
                    var key = (eventId, attr.LogName);
                    if (!_reflectionHandlers.ContainsKey(key)) {
                        _reflectionHandlers[key] = new List<Type>();
                    }
                    _reflectionHandlers[key].Add(ruleType);
                }
            }
        }
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
    public static Type? GetEventRuleType(EventType namedEvent) {
        var mode = _discoveryMode;
        EnsureInitialized();

        if (mode == EventRuleDiscoveryMode.ExplicitOnly) {
            return _explicitRuleTypes.TryGetValue(namedEvent, out var explicitType) ? explicitType : null;
        }
        if (mode == EventRuleDiscoveryMode.Reflection) {
            return _reflectionRuleTypes.TryGetValue(namedEvent, out var reflectionType) ? reflectionType : null;
        }

        return _explicitRuleTypes.TryGetValue(namedEvent, out var type) ? type
            : _reflectionRuleTypes.TryGetValue(namedEvent, out var reflection) ? reflection
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
        var mode = _discoveryMode;
        EnsureInitialized();
        List<string>? failedRuleNames = null;
        List<Exception>? projectionErrors = null;
        string eventLog = eventObject.OriginalLogName;

        foreach (var namedEvent in Expand(targetEventTypes)) {
            if (mode != EventRuleDiscoveryMode.Reflection && _ruleFactories.TryGetValue(namedEvent, out var reg)) {
                try {
                    if (!string.Equals(reg.LogName, eventLog, StringComparison.OrdinalIgnoreCase) ||
                        !reg.EventIds.Contains(eventObject.Id)) {
                        continue;
                    }
                    if (reg.CanHandle != null && !reg.CanHandle(eventObject)) {
                        continue;
                    }

                    var instance = reg.Factory(eventObject);
                    if (instance is IEventRule eventRule) {
                        if (eventRule.CanHandle(eventObject)) {
                            return instance;
                        }
                        continue;
                    }
                    return instance;
                } catch (Exception ex) {
                    failedRuleNames ??= new List<string>();
                    projectionErrors ??= new List<Exception>();
                    failedRuleNames.Add(reg.RuleType?.FullName ?? namedEvent.ToString());
                    projectionErrors.Add(ex);
                    continue;
                }
            }

            if (mode == EventRuleDiscoveryMode.ExplicitOnly) {
                continue;
            }

            if (!_reflectionRuleTypes.TryGetValue(namedEvent, out var ruleType) || ruleType == null) {
                continue;
            }
            if (!_reflectionHandlers.TryGetValue((eventObject.Id, eventLog), out List<Type>? handlers) ||
                !handlers.Contains(ruleType)) {
                continue;
            }

            try {
                var constructor = ruleType.GetConstructor(new[] { typeof(EventObject) });
                if (constructor == null) {
                    continue;
                }

                var instance = (EventTypeRecord)constructor.Invoke(new object[] { eventObject });

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
                failedRuleNames.Add(ruleType.FullName ?? ruleType.Name);
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

    private static EventType GetEventTypeForRuleType(Type type) {
        if (type.IsSubclassOf(typeof(EventRuleBase))) {
            try {
#pragma warning disable SYSLIB0050
                var instance = (EventRuleBase)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
                return instance.Type;
            } catch {
            }
        }

        var attr = type.GetCustomAttribute<EventRuleAttribute>();
        if (attr != null) {
            return attr.Type;
        }

        throw new InvalidOperationException($"Type {type.Name} is not properly configured");
    }

    /// <summary>
    /// Gets event IDs and log names for event types using rule classes.
    /// </summary>
    internal static Dictionary<string, HashSet<int>> GetSourceMap(IReadOnlyCollection<EventType> eventTypes) {
        var mode = _discoveryMode;
        EnsureInitialized();

        var eventInfoDict = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var namedEvent in Expand(eventTypes)) {
            if (mode != EventRuleDiscoveryMode.Reflection && _ruleFactories.TryGetValue(namedEvent, out var reg)) {
                if (!eventInfoDict.TryGetValue(reg.LogName, out var idSet)) {
                    idSet = new HashSet<int>();
                    eventInfoDict[reg.LogName] = idSet;
                }
                foreach (var id in reg.EventIds) {
                    idSet.Add(id);
                }
                continue;
            }

            if (mode == EventRuleDiscoveryMode.ExplicitOnly) {
                continue;
            }

            if (!_reflectionRuleTypes.TryGetValue(namedEvent, out var ruleType) || ruleType == null) {
                continue;
            }

            List<int>? ruleEventIds = null;
            string? ruleLogName = null;

            if (ruleType.IsSubclassOf(typeof(EventRuleBase))) {
                try {
#pragma warning disable SYSLIB0050
                    var instance = (EventRuleBase)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(ruleType);
#pragma warning restore SYSLIB0050
                    ruleEventIds = instance.EventIds;
                    ruleLogName = instance.LogName;
                } catch {
                    continue;
                }
            } else {
                var attr = ruleType.GetCustomAttribute<EventRuleAttribute>();
                if (attr != null) {
                    ruleEventIds = attr.EventIds;
                    ruleLogName = attr.LogName;
                }
            }

            if (ruleEventIds != null && ruleLogName != null) {
                if (!eventInfoDict.TryGetValue(ruleLogName, out var eventIdSet)) {
                    eventIdSet = new HashSet<int>();
                    eventInfoDict[ruleLogName] = eventIdSet;
                }

                foreach (var eventId in ruleEventIds) {
                    eventIdSet.Add(eventId);
                }
            }
        }

        return eventInfoDict;
    }
}
