using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Password hash sync manager heartbeat (Directory Synchronization 663).
/// </summary>
public class AADSyncPasswordHashSyncStatus : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 663 };
    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };
    /// <inheritdoc />
    public override EventType Type => EventType.AADSyncPasswordHashSyncStatus;

    /// <summary>Accepts Directory Synchronization password hash sync heartbeat events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider matches Directory Synchronization.</returns>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Directory Synchronization");
    }

    /// <summary>Server emitting the heartbeat.</summary>
    public string Computer;
    /// <summary>Partition name, if present.</summary>
    public string? PartitionName;
    /// <summary>Connector identifier from the payload.</summary>
    public string? ConnectorId;
    /// <summary>Heartbeat state; true indicates the component reports being alive.</summary>
    public bool Alive = true;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    private static readonly Regex PartitionRegex = new("<partition-name>(?<p>[^<]+)</partition-name>", RegexOptions.IgnoreCase);
    private static readonly Regex ConnectorRegex = new("<connector-id>(?<c>[^<]+)</connector-id>", RegexOptions.IgnoreCase);

    /// <summary>
    /// Builds a password hash sync status record from Directory Synchronization event 663.
    /// </summary>
    /// <param name="eventObject">Event containing password hash sync status.</param>
    public AADSyncPasswordHashSyncStatus(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "AADSyncPasswordHashSyncStatus";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        var msg = Rules.RuleHelpers.GetMessage(SourceEvent);
        var partitionMatch = PartitionRegex.Match(msg);
        if (partitionMatch.Success) PartitionName = partitionMatch.Groups["p"].Value;

        var connectorMatch = ConnectorRegex.Match(msg);
        if (connectorMatch.Success) ConnectorId = connectorMatch.Groups["c"].Value;
    }
}
