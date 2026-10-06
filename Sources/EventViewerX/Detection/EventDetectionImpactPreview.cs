namespace EventViewerX;

/// <summary>A bounded comparison of two plans against exactly the same historical evidence.</summary>
public sealed class EventDetectionImpactPreview {
    internal EventDetectionImpactPreview(string previousHash, string currentHash, int observations,
        IReadOnlyList<EventDetectionFinding> previous, IReadOnlyList<EventDetectionFinding> current,
        bool complete, IReadOnlyList<string> newRequirements, IReadOnlyList<string> diagnostics) {
        PreviousPlanHash = previousHash; CurrentPlanHash = currentHash; ObservationCount = observations;
        PreviousFindingCount = previous.Count(item => item.Status == EventDetectionFindingStatus.Matched);
        CurrentFindingCount = current.Count(item => item.Status == EventDetectionFindingStatus.Matched);
        var before = new HashSet<string>(previous.Where(IsMatch).Select(Identity), StringComparer.Ordinal);
        var after = new HashSet<string>(current.Where(IsMatch).Select(Identity), StringComparer.Ordinal);
        Appearing = Array.AsReadOnly(current.Where(item => IsMatch(item) && !before.Contains(Identity(item))).ToArray());
        Disappearing = Array.AsReadOnly(previous.Where(item => IsMatch(item) && !after.Contains(Identity(item))).ToArray());
        IsComplete = complete;
        NewSourceRequirements = Array.AsReadOnly(newRequirements.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
    private static bool IsMatch(EventDetectionFinding finding) => finding.Status == EventDetectionFindingStatus.Matched;
    private static string Identity(EventDetectionFinding finding) =>
        finding.RuleId.Length + ":" + finding.RuleId.ToUpperInvariant() + ":" + string.Join(";", finding.EvidenceIdentities);
    /// <summary>Previous effective plan identity, including tuning.</summary>
    public string PreviousPlanHash { get; }
    /// <summary>Proposed effective plan identity, including tuning.</summary>
    public string CurrentPlanHash { get; }
    /// <summary>Number of historical observations compared.</summary>
    public int ObservationCount { get; }
    /// <summary>Previous matched finding volume in the bounded sample.</summary>
    public int PreviousFindingCount { get; }
    /// <summary>Proposed matched finding volume in the bounded sample.</summary>
    public int CurrentFindingCount { get; }
    /// <summary>Proposed minus previous matched finding volume.</summary>
    public int FindingCountChange => CurrentFindingCount - PreviousFindingCount;
    /// <summary>Findings introduced by the proposed plan, identified by rule and evidence.</summary>
    public IReadOnlyList<EventDetectionFinding> Appearing { get; }
    /// <summary>Findings no longer produced by the proposed plan.</summary>
    public IReadOnlyList<EventDetectionFinding> Disappearing { get; }
    /// <summary>Additional selector or typed-projection requirements. A wildcard denotes an unrestricted source dimension.</summary>
    public IReadOnlyList<string> NewSourceRequirements { get; }
    /// <summary>True only when coverage, input bounds, and both evaluations are complete.</summary>
    public bool IsComplete { get; }
    /// <summary>Reasons why this comparison cannot establish the full historical impact.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}

public static partial class EventDetectionEngine {
    /// <summary>Previews a plan change on a bounded common sample. This does not enable either plan or deliver alerts.</summary>
    public static EventDetectionImpactPreview PreviewChanges(IEnumerable<EventObservation> observations,
        EventDetectionPlan previous, EventDetectionPlan current, EventDetectionEngineOptions? options = null,
        int maximumObservations = 10_000, int maximumFindings = 100_000, CancellationToken cancellationToken = default) {
        if (observations == null) { throw new ArgumentNullException(nameof(observations)); }
        if (previous == null) { throw new ArgumentNullException(nameof(previous)); }
        if (current == null) { throw new ArgumentNullException(nameof(current)); }
        if (maximumObservations <= 0 || maximumObservations == int.MaxValue) { throw new ArgumentOutOfRangeException(nameof(maximumObservations)); }
        if (maximumFindings <= 0 || maximumFindings == int.MaxValue) { throw new ArgumentOutOfRangeException(nameof(maximumFindings)); }
        var sample = new List<EventObservation>();
        foreach (EventObservation observation in observations) {
            cancellationToken.ThrowIfCancellationRequested();
            if (observation == null) { throw new ArgumentException("Observations cannot contain null.", nameof(observations)); }
            sample.Add(observation);
            if (sample.Count > maximumObservations) { break; }
        }
        bool truncated = sample.Count > maximumObservations;
        if (truncated) { sample.RemoveAt(sample.Count - 1); }
        sample.Sort(CompareObservations);
        var diagnostics = new List<string>();
        if (truncated) { diagnostics.Add("The historical observation limit was reached; comparison covers only the bounded sample."); }
        EventDetectionCoverage coverage = options?.Coverage ?? EventDetectionCoverage.Unknown();
        if (!coverage.IsComplete) { diagnostics.Add("Historical collection coverage is incomplete or undeclared."); }
        string[] scopeGaps = PreviewCoverageGaps(previous, coverage).Concat(PreviewCoverageGaps(current, coverage))
            .Distinct(StringComparer.Ordinal).ToArray();
        diagnostics.AddRange(scopeGaps);
        EventDetectionEngineOptions runOptions = options ?? new EventDetectionEngineOptions();
        if (truncated || scopeGaps.Length != 0) {
            string[] failures = scopeGaps.Concat(truncated ? new[] { "Historical impact input was truncated." } : Array.Empty<string>()).ToArray();
            runOptions = new EventDetectionEngineOptions(runOptions.MaximumObservations, runOptions.MaximumGroups,
                runOptions.MaximumStateObservations, runOptions.MaximumStateBytes, runOptions.MaximumCandidateRules,
                coverage.WithFailures(failures)).WithAbsenceWindow(runOptions.AbsenceWindow);
        }
        EventDetectionFinding[] Run(EventDetectionPlan plan) {
            EventDetectionFinding[] findings = StreamCore(sample, plan, runOptions, cancellationToken).Take(maximumFindings + 1).ToArray();
            if (findings.Length > maximumFindings) {
                diagnostics.Add($"Plan {plan.PlanHash} reached the finding limit.");
                findings = findings.Take(maximumFindings).ToArray();
            }
            diagnostics.AddRange(findings.Where(item => item.Status != EventDetectionFindingStatus.Matched)
                .Select(item => item.CompletenessDiagnostic ?? "Detection evaluation was incomplete."));
            return findings;
        }
        EventDetectionFinding[] before = Run(previous);
        EventDetectionFinding[] after = Run(current);
        string[] newRequirements = SourceRequirements(current).Except(SourceRequirements(previous), StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return new EventDetectionImpactPreview(previous.PlanHash, current.PlanHash, sample.Count, before, after,
            diagnostics.Count == 0, newRequirements, diagnostics);
    }

    private static IEnumerable<string> PreviewCoverageGaps(EventDetectionPlan plan, EventDetectionCoverage coverage) {
        var gaps = new List<string>();
        void Check<T>(string dimension, IReadOnlyCollection<T> expected, IReadOnlyCollection<T> observed,
            IEnumerable<T> required, IEqualityComparer<T>? comparer = null) {
            // An omitted dimension adds no filter to the caller's explicit coverage declaration.
            // Once a finite scope is declared, it cannot establish an unrestricted rule scope.
            if (expected.Count == 0 && observed.Count == 0) { return; }
            var selected = new HashSet<T>(required, comparer);
            var collected = new HashSet<T>(observed, comparer);
            if (selected.Count == 0 || !selected.IsSubsetOf(collected)) {
                gaps.Add($"Historical {dimension} coverage does not establish all sources required by plan {plan.PlanHash}.");
            }
        }
        foreach (EventDetectionPlan.CompiledRule rule in plan.CompiledRules) {
            IReadOnlyList<EventSourceDefinition> typedSources = rule.IndexEventTypeNames.Count == 0
                ? Array.Empty<EventSourceDefinition>()
                : EventTypeCatalog.GetSources(rule.Definition.EventTypes.Concat(rule.Definition.Steps.SelectMany(step => step.EventTypes)));
            Check("EventId", coverage.ExpectedEventIds, coverage.ObservedEventIds,
                rule.IndexEventIds.Count != 0 ? rule.IndexEventIds : typedSources.SelectMany(source => source.EventIds));
            Check("Channel", coverage.ExpectedChannels, coverage.ObservedChannels,
                rule.IndexChannels.Count != 0 ? rule.IndexChannels : typedSources.Select(source => source.LogName), StringComparer.OrdinalIgnoreCase);
            Check("Provider", coverage.ExpectedProviders, coverage.ObservedProviders,
                rule.IndexProviders.Count != 0 ? rule.IndexProviders : typedSources.Any(source => source.ProviderNames.Count == 0)
                    ? Array.Empty<string>() : typedSources.SelectMany(source => source.ProviderNames), StringComparer.OrdinalIgnoreCase);
            Check("EventType", EventTypeCatalog.Expand(coverage.ExpectedEventTypes), EventTypeCatalog.Expand(coverage.ObservedEventTypes),
                rule.IndexEventTypeNames.Select(name => (EventType)Enum.Parse(typeof(EventType), name)));
        }
        return gaps;
    }

    private static IEnumerable<string> SourceRequirements(EventDetectionPlan plan) {
        foreach (EventDetectionPlan.CompiledRule compiled in plan.CompiledRules) {
            EventDetectionRuleDefinition rule = compiled.Definition;
            foreach (string channel in compiled.IndexChannels.DefaultIfEmpty("*")) { yield return "Channel:" + channel; }
            foreach (string provider in compiled.IndexProviders.DefaultIfEmpty("*")) { yield return "Provider:" + provider; }
            foreach (int id in compiled.IndexEventIds) { yield return "EventId:" + id; }
            // Retain selector combinations as well as individual dimensions. Moving an
            // existing provider to another channel can introduce a new collection scope.
            string Scope(EventDetectionStepDefinition? step) => "Scope:" + System.Text.Json.JsonSerializer.Serialize(new {
                RuleChannels = rule.Channels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                RuleProviders = rule.Providers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                RuleEventIds = rule.EventIds.OrderBy(value => value).ToArray(),
                RuleEventTypes = rule.EventTypes.OrderBy(value => value).ToArray(),
                StepChannels = step?.Channels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                StepProviders = step?.Providers.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                StepEventIds = step?.EventIds.OrderBy(value => value).ToArray(),
                StepEventTypes = step?.EventTypes.OrderBy(value => value).ToArray()
            });
            if (rule.Steps.Count == 0) { yield return Scope(null); }
            if (rule.EventIds.Count == 0 && rule.EventTypes.Count == 0 &&
                (rule.Steps.Count == 0 || rule.Steps.Any(step => step.EventIds.Count == 0 && step.EventTypes.Count == 0))) { yield return "EventId:*"; }
            foreach (EventDetectionStepDefinition step in rule.Steps) {
                yield return Scope(step);
            }
        }
        foreach (EventType type in plan.RequiredEventTypes) { yield return "EventType:" + type; }
    }
}
