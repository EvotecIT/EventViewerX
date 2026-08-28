using EventViewerX.Reporting;

namespace EventViewerX;

/// <summary>Metadata supplied when building a decision-oriented detection report.</summary>
public sealed class EventDetectionReportOptions {
    /// <summary>Report title.</summary>
    public string Title { get; set; } = "EventViewerX detection report";
    /// <summary>Query, watcher, storage job, or caller that owns source selection.</summary>
    public string QueryOwner { get; set; } = "Caller-supplied observations";
    /// <summary>Whether optional durable history contributed to the analysis.</summary>
    public bool UsedStorageHistory { get; set; }
    /// <summary>Applied query, state, or result limits.</summary>
    public IReadOnlyList<string> Limits { get; set; } = Array.Empty<string>();
    /// <summary>Source or execution failures that affect completeness.</summary>
    public IReadOnlyList<string> Failures { get; set; } = Array.Empty<string>();
}

/// <summary>Integrity and data-source state for one enabled detection pack.</summary>
public sealed class EventDetectionPackHealth {
    internal EventDetectionPackHealth(
        EventDetectionPack pack,
        EventDetectionPackValidationResult validation,
        EventDetectionPackCoverage coverage,
        IReadOnlyList<EventObservation> observations) {

        PackId = pack.PackId;
        Version = pack.Version;
        ContentHash = pack.ContentHash;
        ContentHashValid = validation.ContentHashValid;
        SignatureStatus = validation.SignatureStatus;
        RuleCount = pack.Rules.Count;
        RequiredEventTypes = coverage.EventTypes;
        RequiredEventIds = coverage.EventIds;
        RequiredChannels = coverage.Channels;
        RequiredProviders = coverage.Providers;
        var observedTypes = new HashSet<string>(
            observations.Select(static observation => observation.TypeName),
            StringComparer.OrdinalIgnoreCase);
        var observedChannels = new HashSet<string>(
            observations.Select(static observation => observation.SourceLog),
            StringComparer.OrdinalIgnoreCase);
        var observedProviders = new HashSet<string>(
            observations.Select(static observation => observation.ProviderName),
            StringComparer.OrdinalIgnoreCase);
        MissingRequiredEventTypes = coverage.EventTypes
            .Where(type => !observedTypes.Contains(type.ToString()))
            .ToArray();
        MissingRequiredChannels = coverage.Channels
            .Where(channel => !observedChannels.Contains(channel))
            .ToArray();
        MissingRequiredProviders = coverage.Providers
            .Where(provider => !observedProviders.Contains(provider))
            .ToArray();
        Diagnostics = validation.Diagnostics;
    }

    /// <summary>Pack ID.</summary>
    public string PackId { get; }
    /// <summary>Pack version.</summary>
    public string Version { get; }
    /// <summary>Pack content hash.</summary>
    public string ContentHash { get; }
    /// <summary>Whether content integrity is valid.</summary>
    public bool ContentHashValid { get; }
    /// <summary>Signature verification state.</summary>
    public EventDetectionPackSignatureStatus SignatureStatus { get; }
    /// <summary>Number of rules in the pack.</summary>
    public int RuleCount { get; }
    /// <summary>Typed event requirements.</summary>
    public IReadOnlyList<EventType> RequiredEventTypes { get; }
    /// <summary>Explicit native event-ID requirements.</summary>
    public IReadOnlyList<int> RequiredEventIds { get; }
    /// <summary>Explicit channel requirements.</summary>
    public IReadOnlyList<string> RequiredChannels { get; }
    /// <summary>Explicit provider requirements.</summary>
    public IReadOnlyList<string> RequiredProviders { get; }
    /// <summary>Required typed projections absent from the supplied observation window.</summary>
    public IReadOnlyList<EventType> MissingRequiredEventTypes { get; }
    /// <summary>Required source channels absent from the supplied observation window.</summary>
    public IReadOnlyList<string> MissingRequiredChannels { get; }
    /// <summary>Required providers absent from the supplied observation window.</summary>
    public IReadOnlyList<string> MissingRequiredProviders { get; }
    /// <summary>Whether every declared source requirement is represented in the observation window.</summary>
    public bool HasRequiredDataCoverage =>
        MissingRequiredEventTypes.Count == 0 &&
        MissingRequiredChannels.Count == 0 &&
        MissingRequiredProviders.Count == 0;
    /// <summary>Pack validation diagnostics.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}

/// <summary>Reusable detection, health, coverage, and incident-timeline report snapshot.</summary>
public sealed class EventDetectionReportSnapshot {
    internal EventDetectionReportSnapshot(
        string title,
        DateTime generatedAtUtc,
        DateTime? startTimeUtc,
        DateTime? endTimeUtc,
        string queryOwner,
        bool usedStorageHistory,
        IReadOnlyList<string> targets,
        IReadOnlyList<string> channels,
        IReadOnlyList<string> limits,
        IReadOnlyList<string> failures,
        IReadOnlyList<EventObservation> observations,
        IReadOnlyList<EventDetectionFinding> findings,
        IReadOnlyList<EventDetectionPackHealth> packs,
        EventTimeline timeline,
        EventReport presentationReport) {

        Title = title;
        GeneratedAtUtc = generatedAtUtc;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        QueryOwner = queryOwner;
        UsedStorageHistory = usedStorageHistory;
        Targets = Array.AsReadOnly(targets.ToArray());
        Channels = Array.AsReadOnly(channels.ToArray());
        Limits = Array.AsReadOnly(limits.ToArray());
        Failures = Array.AsReadOnly(failures.ToArray());
        Observations = Array.AsReadOnly(observations.ToArray());
        Findings = Array.AsReadOnly(findings.ToArray());
        Packs = Array.AsReadOnly(packs.ToArray());
        Timeline = timeline;
        PresentationReport = presentationReport;
        SeverityCounts = findings
            .Where(static finding => finding.Status == EventDetectionFindingStatus.Matched)
            .GroupBy(static finding => finding.Severity)
            .ToDictionary(static group => group.Key, static group => group.Count());
        IsComplete = failures.Count == 0 &&
            findings.All(static finding => finding.Status == EventDetectionFindingStatus.Matched) &&
            packs.All(static pack => pack.HasRequiredDataCoverage);
    }

    /// <summary>Report title.</summary>
    public string Title { get; }
    /// <summary>UTC generation time.</summary>
    public DateTime GeneratedAtUtc { get; }
    /// <summary>Earliest represented event time.</summary>
    public DateTime? StartTimeUtc { get; }
    /// <summary>Latest represented event time.</summary>
    public DateTime? EndTimeUtc { get; }
    /// <summary>Owner of source query semantics.</summary>
    public string QueryOwner { get; }
    /// <summary>Whether optional storage history was used.</summary>
    public bool UsedStorageHistory { get; }
    /// <summary>Represented source or collector targets.</summary>
    public IReadOnlyList<string> Targets { get; }
    /// <summary>Represented original channels.</summary>
    public IReadOnlyList<string> Channels { get; }
    /// <summary>Applied execution limits.</summary>
    public IReadOnlyList<string> Limits { get; }
    /// <summary>Failures affecting completeness.</summary>
    public IReadOnlyList<string> Failures { get; }
    /// <summary>Canonical source observations.</summary>
    public IReadOnlyList<EventObservation> Observations { get; }
    /// <summary>Matched, incomplete, and error findings.</summary>
    public IReadOnlyList<EventDetectionFinding> Findings { get; }
    /// <summary>Enabled pack health and coverage.</summary>
    public IReadOnlyList<EventDetectionPackHealth> Packs { get; }
    /// <summary>Reusable incident timeline.</summary>
    public EventTimeline Timeline { get; }
    /// <summary>Renderer-ready findings and timeline report.</summary>
    public EventReport PresentationReport { get; }
    /// <summary>Matched findings grouped by severity.</summary>
    public IReadOnlyDictionary<EventDetectionSeverity, int> SeverityCounts { get; }
    /// <summary>Whether no source, bound, or rule evaluation failure affected the report.</summary>
    public bool IsComplete { get; }
}
