using System.Collections.ObjectModel;

namespace EventViewerX;

/// <summary>Explainable detection outcome with stable rule and evidence provenance.</summary>
public sealed class EventDetectionFinding {
    internal EventDetectionFinding(
        string ruleId,
        string ruleVersion,
        string packId,
        string packVersion,
        string sourceKind,
        string sourceId,
        string sourceStatus,
        string sourceHash,
        string license,
        string title,
        EventDetectionSeverity severity,
        int confidence,
        EventDetectionFindingStatus status,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        IReadOnlyList<EventObservation> evidence,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> falsePositives,
        IReadOnlyList<string> references,
        IReadOnlyDictionary<string, string> entities,
        EventDetectionCoverage coverage,
        string explanation,
        string? completenessDiagnostic) {

        RuleId = ruleId;
        RuleVersion = ruleVersion;
        PackId = packId;
        PackVersion = packVersion;
        SourceKind = sourceKind;
        SourceId = sourceId;
        SourceStatus = sourceStatus;
        SourceHash = sourceHash;
        License = license;
        Title = title;
        Severity = severity;
        Confidence = confidence;
        Status = status;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        Evidence = Array.AsReadOnly(evidence.ToArray());
        EvidenceIdentities = Array.AsReadOnly(evidence.Select(static item => item.Identity).ToArray());
        Tags = Array.AsReadOnly(tags.ToArray());
        FalsePositives = Array.AsReadOnly(falsePositives.ToArray());
        References = Array.AsReadOnly(references.ToArray());
        Entities = new ReadOnlyDictionary<string, string>(entities.ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase));
        Coverage = coverage.Snapshot();
        Explanation = explanation;
        CompletenessDiagnostic = completenessDiagnostic;
    }

    /// <summary>Stable detection rule identifier.</summary>
    public string RuleId { get; }
    /// <summary>Detection content version.</summary>
    public string RuleVersion { get; }
    /// <summary>Pack that supplied the rule.</summary>
    public string PackId { get; }
    /// <summary>Version of the supplying pack.</summary>
    public string PackVersion { get; }
    /// <summary>Native, Sigma, or another source format.</summary>
    public string SourceKind { get; }
    /// <summary>Source rule identifier.</summary>
    public string SourceId { get; }
    /// <summary>Source maturity status.</summary>
    public string SourceStatus { get; }
    /// <summary>SHA-256 source content hash.</summary>
    public string SourceHash { get; }
    /// <summary>Rule content license.</summary>
    public string License { get; }
    /// <summary>Operator-facing title.</summary>
    public string Title { get; }
    /// <summary>Effective severity after tuning.</summary>
    public EventDetectionSeverity Severity { get; }
    /// <summary>Confidence from zero through one hundred.</summary>
    public int Confidence { get; }
    /// <summary>Matched, incomplete, or error outcome.</summary>
    public EventDetectionFindingStatus Status { get; }
    /// <summary>UTC start of the evidence window.</summary>
    public DateTime StartTimeUtc { get; }
    /// <summary>UTC end of the evidence window.</summary>
    public DateTime EndTimeUtc { get; }
    /// <summary>Evidence observations retained for explanation and pivots.</summary>
    public IReadOnlyList<EventObservation> Evidence { get; }
    /// <summary>Stable identities of every evidence event.</summary>
    public IReadOnlyList<string> EvidenceIdentities { get; }
    /// <summary>Rule tags including ATT&amp;CK metadata where supplied.</summary>
    public IReadOnlyList<string> Tags { get; }
    /// <summary>Expected benign explanations.</summary>
    public IReadOnlyList<string> FalsePositives { get; }
    /// <summary>Rule source references.</summary>
    public IReadOnlyList<string> References { get; }
    /// <summary>Actor, target, host, account, or configured grouping entities.</summary>
    public IReadOnlyDictionary<string, string> Entities { get; }
    /// <summary>Expected and observed data-source coverage for the execution window.</summary>
    public EventDetectionCoverage Coverage { get; }
    /// <summary>Human-readable reason the rule matched or became incomplete.</summary>
    public string Explanation { get; }
    /// <summary>Reason the result is not complete.</summary>
    public string? CompletenessDiagnostic { get; }
}
