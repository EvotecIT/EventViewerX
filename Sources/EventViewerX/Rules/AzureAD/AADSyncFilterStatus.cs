using System;
using System.Collections.Generic;

namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// ADSync connector filtering status (ADSync 6952).
/// </summary>
public class AADSyncFilterStatus : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 6952 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.AADSyncFilterStatus;

    /// <summary>Accepts ADSync provider filter status events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider is ADSync.</returns>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "ADSync");
    }

    /// <summary>Server running the connector.</summary>
    public string Computer;
    /// <summary>Connector name if present in the message.</summary>
    public string? Connector;
    /// <summary>True when the connector filter is enabled.</summary>
    public bool Enabled;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>
    /// Builds a filter status record from ADSync event 6952.
    /// </summary>
    /// <param name="eventObject">Event describing the filter status.</param>
    public AADSyncFilterStatus(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "AADSyncFilterStatus";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        var msg = Rules.RuleHelpers.GetMessage(SourceEvent);
        Enabled = msg.IndexOf("enabled", StringComparison.OrdinalIgnoreCase) >= 0;
        Connector = ExtractAfter(msg, "Connector");
    }

    private static string? ExtractAfter(string message, string marker)
    {
        var idx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var slice = message.Substring(idx + marker.Length).Trim();
        return slice;
    }
}
