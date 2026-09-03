using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Azure AD Connect import/sync status (Directory Synchronization 105/132/133/134).
/// </summary>
public class AADSyncImportStatus : EventRuleBase
{
    /// <inheritdoc />
    public override List<int> EventIds => new() { 105, 132, 133, 134 };
    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };
    /// <inheritdoc />
    public override EventType Type => EventType.AADSyncImportStatus;

    /// <summary>Accepts Directory Synchronization import/status events.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider matches Directory Synchronization.</returns>
    public override bool CanHandle(EventObject eventObject)
    {
        return RuleHelpers.IsProvider(eventObject, "Directory Synchronization");
    }

    /// <summary>Host where the ADSync import is running.</summary>
    public string Computer;
    /// <summary>Stage of the import pipeline (derived from event id/message).</summary>
    public string Phase;
    /// <summary>Tracking identifier when provided in the message.</summary>
    public string? TrackingId;
    /// <summary>Session identifier associated with the run.</summary>
    public string? SessionId;
    /// <summary>Watermark hash extracted from the event payload.</summary>
    public string? WatermarkHash;
    /// <summary>Partition name parsed from the XML payload.</summary>
    public string? PartitionName;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    private static readonly Regex WatermarkHashRegex = new("Hash:\\s*(?<h>[A-Za-z0-9+/=\\-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex TrackingRegex = new("TrackingId:\\s*(?<t>[A-Za-z0-9\\-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex SessionRegex = new("Session\\s*Id:?\\s*(?<s>[A-Za-z0-9\\-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex PartitionRegex = new("<partition-name>(?<p>[^<]+)</partition-name>", RegexOptions.IgnoreCase);

    /// <summary>
    /// Builds an import status record from Directory Synchronization events (105, 132-134).
    /// </summary>
    /// <param name="eventObject">Event containing import status details.</param>
    public AADSyncImportStatus(EventObject eventObject) : base(eventObject)
    {
        SourceEvent = eventObject;
        TypeName = "AADSyncImportStatus";
        Computer = SourceEvent.ComputerName;
        When = SourceEvent.TimeCreated;

        var msg = Rules.RuleHelpers.GetMessage(SourceEvent);

        Phase = eventObject.Id switch
        {
            105 => "ImportIteration",
            132 => "RefetchBaseline",
            133 => "ImportStart",
            134 => "ImportComplete",
            _ => "Info"
        };

        TrackingId = MatchOrNull(TrackingRegex, msg);
        SessionId = MatchOrNull(SessionRegex, msg);
        WatermarkHash = MatchOrNull(WatermarkHashRegex, msg);

        var partitionMatch = PartitionRegex.Match(msg);
        if (partitionMatch.Success) PartitionName = partitionMatch.Groups["p"].Value;
    }

    private static string? MatchOrNull(Regex rx, string input)
    {
        var m = rx.Match(input);
        return m.Success ? m.Groups[1].Value : null;
    }
}
