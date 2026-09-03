using System;
using System.Collections.Generic;

namespace EventViewerX;

/// <summary>
/// Attribute to define event rule metadata for simple cases
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class EventRuleAttribute : Attribute {
    /// <summary>Event identifiers handled by the rule.</summary>
    public List<int> EventIds { get; }
    /// <summary>Name of the event log.</summary>
    public string LogName { get; }
    /// <summary>Associated event type.</summary>
    public EventType Type { get; }

    /// <summary>Event providers that emit this rule's events. An empty array means the rule is not provider-scoped.</summary>
    public string[] ProviderNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of the attribute.
    /// </summary>
    /// <param name="namedEvent">Named event represented by the rule.</param>
    /// <param name="logName">Log name to watch.</param>
    /// <param name="eventIds">Event IDs handled by the rule.</param>
    public EventRuleAttribute(EventType namedEvent, string logName, params int[] eventIds) {
        Type = namedEvent;
        LogName = logName;
        EventIds = new List<int>(eventIds);
    }
}
