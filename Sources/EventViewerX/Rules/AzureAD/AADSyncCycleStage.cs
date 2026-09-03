using System;
using System.Collections.Generic;

namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Azure AD Connect cycle stage (Directory Synchronization event 904).
/// </summary>
public class AADSyncCycleStage : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 904 };
    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };
    /// <inheritdoc />
    public override EventType Type => EventType.AADSyncCycleStage;

    /// <summary>Accepts Directory Synchronization provider events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider is Directory Synchronization.</returns>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Directory Synchronization");
    }

    /// <summary>Host where the sync stage ran.</summary>
    public string Computer;
    /// <summary>Derived stage name (CycleStarted/Importing/Finished/etc.).</summary>
    public string Stage;
    /// <summary>Connector referenced in the event, when present.</summary>
    public string? Connector;
    /// <summary>Delta or full cycle indicator, when detectable.</summary>
    public string? CycleType;
    /// <summary>Tracking identifier emitted by ADSync.</summary>
    public string? TrackingId;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>
    /// Builds a cycle stage record from a Directory Synchronization event (ID 904).
    /// </summary>
    /// <param name="eventObject">Event carrying ADSync cycle information.</param>
    public AADSyncCycleStage(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "AADSyncCycleStage";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        var message = Rules.RuleHelpers.GetMessage(SourceEvent);

        // Basic stage derivation
        Stage = DeriveStage(message);
        Connector = ExtractAfter(message, "Connector:");
        CycleType = message.IndexOf("Delta", StringComparison.OrdinalIgnoreCase) >= 0 ? "Delta" :
            message.IndexOf("Full", StringComparison.OrdinalIgnoreCase) >= 0 ? "Full" : null;
        TrackingId = ExtractAfter(message, "TrackingId:") ?? ExtractAfter(message, "TrackingID :");
    }

    private static string DeriveStage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        if (message.IndexOf("cycle started", StringComparison.OrdinalIgnoreCase) >= 0) return "CycleStarted";
        if (message.StartsWith("Import/Sync/Export cycle", StringComparison.OrdinalIgnoreCase)) return "CycleStarted";
        if (message.StartsWith("Initializing", StringComparison.OrdinalIgnoreCase)) return "Initializing";
        if (message.StartsWith("Importing", StringComparison.OrdinalIgnoreCase)) return "Importing";
        if (message.StartsWith("Starting:", StringComparison.OrdinalIgnoreCase)) return "Starting";
        if (message.StartsWith("Finished:", StringComparison.OrdinalIgnoreCase)) return "Finished";
        if (message.StartsWith("Before Execute RunProfile", StringComparison.OrdinalIgnoreCase)) return "RunProfileBefore";
        if (message.StartsWith("After Execute RunProfile", StringComparison.OrdinalIgnoreCase)) return "RunProfileAfter";
        if (message.IndexOf("scheduler", StringComparison.OrdinalIgnoreCase) >= 0 &&
            message.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "SchedulerComplete";
        }
        return "Info";
    }

    private static string? ExtractAfter(string message, string marker)
    {
        var idx = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var slice = message.Substring(start).Trim();
        return slice.TrimEnd('.', ';');
    }
}
