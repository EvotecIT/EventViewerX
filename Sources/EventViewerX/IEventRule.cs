using System;
using System.Collections.Generic;

namespace EventViewerX;

/// <summary>
/// Interface for all event rule classes that defines required metadata
/// </summary>
public interface IEventRule {
    /// <summary>
    /// Gets the event IDs this rule handles
    /// </summary>
    List<int> EventIds { get; }

    /// <summary>
    /// Gets the log name where these events are found
    /// </summary>
    string LogName { get; }

    /// <summary>
    /// Gets the event type this rule represents.
    /// </summary>
    EventType Type { get; }

    /// <summary>
    /// Determines if this rule can handle the given event object
    /// </summary>
    /// <param name="eventObject">The event object to evaluate</param>
    /// <returns>True if this rule can process the event, false otherwise</returns>
    bool CanHandle(EventObject eventObject);
}

/// <summary>
/// Base class for event rules with metadata
/// </summary>
public abstract class EventRuleBase : EventTypeRecord, IEventRule {
    /// <summary>
    /// Gets the relative priority used when multiple projections can handle the same native event.
    /// Higher values are evaluated first; rules with the same value retain the requested composite order.
    /// </summary>
    public virtual int MatchPriority => 0;

    /// <summary>
    /// Gets the event providers that emit this rule's events. An empty list means the rule is not provider-scoped.
    /// </summary>
    public virtual IReadOnlyList<string> ProviderNames => Array.Empty<string>();

    /// <summary>Event identifiers this rule is responsible for.</summary>
    public abstract List<int> EventIds { get; }
    /// <summary>Windows log name (channel) where the events are emitted.</summary>
    public abstract string LogName { get; }
    /// <summary>Logical event type used by the module for routing results.</summary>
    public abstract EventType Type { get; }

    /// <summary>
    /// Initializes the rule with the raw event data so subclasses can hydrate their fields.
    /// </summary>
    /// <param name="eventObject">Underlying event record to wrap.</param>
    protected EventRuleBase(EventObject eventObject) : base(eventObject) {
    }

    /// <summary>Checks whether the supplied event matches this rule.</summary>
    /// <param name="eventObject">Candidate event to evaluate.</param>
    /// <returns><c>true</c> when the rule can parse the event; otherwise <c>false</c>.</returns>
    public abstract bool CanHandle(EventObject eventObject);

    /// <summary>
    /// Creates an instance of the event rule from an EventObject
    /// </summary>
    /// <param name="eventObject">The event object to process</param>
    /// <returns>An instance of the event rule or null if it cannot be processed</returns>
    public static EventTypeRecord? TryCreate<T>(EventObject eventObject) where T : EventRuleBase, new() {
        var instance = new T();
        return instance.CanHandle(eventObject) ? Activator.CreateInstance(typeof(T), eventObject) as T : null;
    }
}
