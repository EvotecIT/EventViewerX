namespace EventViewerX;

/// <summary>Rule-level changes between two versions of the same detection pack.</summary>
public sealed class EventDetectionPackComparison {
    internal EventDetectionPackComparison(
        string packId,
        string previousVersion,
        string currentVersion,
        IReadOnlyList<string> addedRuleIds,
        IReadOnlyList<string> removedRuleIds,
        IReadOnlyList<string> changedRuleIds,
        IReadOnlyList<string> unchangedRuleIds) {

        PackId = packId;
        PreviousVersion = previousVersion;
        CurrentVersion = currentVersion;
        AddedRuleIds = Array.AsReadOnly(addedRuleIds.ToArray());
        RemovedRuleIds = Array.AsReadOnly(removedRuleIds.ToArray());
        ChangedRuleIds = Array.AsReadOnly(changedRuleIds.ToArray());
        UnchangedRuleIds = Array.AsReadOnly(unchangedRuleIds.ToArray());
    }

    /// <summary>Compared pack ID.</summary>
    public string PackId { get; }
    /// <summary>Earlier version.</summary>
    public string PreviousVersion { get; }
    /// <summary>Later version.</summary>
    public string CurrentVersion { get; }
    /// <summary>New rule IDs.</summary>
    public IReadOnlyList<string> AddedRuleIds { get; }
    /// <summary>Removed rule IDs.</summary>
    public IReadOnlyList<string> RemovedRuleIds { get; }
    /// <summary>Rules whose source hash or effective metadata changed.</summary>
    public IReadOnlyList<string> ChangedRuleIds { get; }
    /// <summary>Rules unchanged between versions.</summary>
    public IReadOnlyList<string> UnchangedRuleIds { get; }
    /// <summary>Whether the new pack changes detection content.</summary>
    public bool HasChanges => AddedRuleIds.Count != 0 || RemovedRuleIds.Count != 0 || ChangedRuleIds.Count != 0;
}

/// <summary>Channels, providers, event IDs, typed projections, and readiness prerequisites required by a pack.</summary>
public sealed class EventDetectionPackCoverage {
    internal EventDetectionPackCoverage(
        IReadOnlyList<EventType> eventTypes,
        IReadOnlyList<int> eventIds,
        IReadOnlyList<string> channels,
        IReadOnlyList<string> providers,
        IReadOnlyList<EventPrerequisite> prerequisites) {

        EventTypes = Array.AsReadOnly(eventTypes.ToArray());
        EventIds = Array.AsReadOnly(eventIds.ToArray());
        Channels = Array.AsReadOnly(channels.ToArray());
        Providers = Array.AsReadOnly(providers.ToArray());
        Prerequisites = Array.AsReadOnly(prerequisites.ToArray());
        AuditPolicies = Array.AsReadOnly(prerequisites
            .Where(static prerequisite => prerequisite.Kind == EventRequirementKind.AuditPolicy)
            .ToArray());
        TargetRoles = Array.AsReadOnly(prerequisites
            .Where(static prerequisite => prerequisite.Kind == EventRequirementKind.TargetRole)
            .ToArray());
    }

    /// <summary>Required typed event projections.</summary>
    public IReadOnlyList<EventType> EventTypes { get; }
    /// <summary>Explicit native event IDs.</summary>
    public IReadOnlyList<int> EventIds { get; }
    /// <summary>Explicit source channels.</summary>
    public IReadOnlyList<string> Channels { get; }
    /// <summary>Explicit providers.</summary>
    public IReadOnlyList<string> Providers { get; }
    /// <summary>Readiness requirements for all typed projections used by the pack.</summary>
    public IReadOnlyList<EventPrerequisite> Prerequisites { get; }
    /// <summary>Advanced-audit policy requirements, including success/failure outcomes.</summary>
    public IReadOnlyList<EventPrerequisite> AuditPolicies { get; }
    /// <summary>Computer roles on which the required evidence is emitted.</summary>
    public IReadOnlyList<EventPrerequisite> TargetRoles { get; }
}
