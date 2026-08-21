using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Provision credentials ping start/end (Directory Synchronization 653/654).
/// </summary>
public class AADSyncProvisionCredentialsPing : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 653, 654 };
    /// <inheritdoc />
    public override string LogName => "Application";
    /// <inheritdoc />
    public override EventType Type => EventType.AADSyncProvisionCredentialsPing;

    /// <summary>Accepts Directory Synchronization credential ping events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider matches Directory Synchronization.</returns>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Directory Synchronization");
    }

    /// <summary>Server that emitted the ping.</summary>
    public string Computer;
    /// <summary>State derived from the event (Start or End).</summary>
    public string State; // Start/End
    /// <summary>Tracking identifier when present.</summary>
    public string? TrackingId;
    /// <summary>Partition name extracted from the XML payload.</summary>
    public string? PartitionName;
    /// <summary>Connector identifier extracted from the XML payload.</summary>
    public string? ConnectorId;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    private static readonly Regex PartitionRegex = new("<partition-name>(?<p>[^<]+)</partition-name>", RegexOptions.IgnoreCase);
    private static readonly Regex ConnectorRegex = new("<connector-id>(?<c>[^<]+)</connector-id>", RegexOptions.IgnoreCase);

    /// <summary>
    /// Builds a credentials ping record from Directory Synchronization events 653/654.
    /// </summary>
    /// <param name="eventObject">Event describing the ping state.</param>
    public AADSyncProvisionCredentialsPing(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "AADSyncProvisionCredentialsPing";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        State = eventObject.Id == 653 ? "Start" : "End";
        var msg = Rules.RuleHelpers.GetMessage(SourceEvent);
        TrackingId = ExtractAfter(msg, "TrackingID :") ?? ExtractAfter(msg, "TrackingId:");

        var partitionMatch = PartitionRegex.Match(msg);
        if (partitionMatch.Success) PartitionName = partitionMatch.Groups["p"].Value;

        var connectorMatch = ConnectorRegex.Match(msg);
        if (connectorMatch.Success) ConnectorId = connectorMatch.Groups["c"].Value;
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
