namespace EventViewerX;

/// <summary>Native and managed execution plan for one typed event predicate.</summary>
public sealed class EventPredicatePlan {
    internal EventPredicatePlan(
        EventFilter? nativeFilter,
        EventPredicate? managedPredicate,
        IReadOnlyList<EventPredicatePlanStep> steps) {

        NativeFilter = nativeFilter;
        ManagedPredicate = managedPredicate;
        Steps = steps;
    }

    /// <summary>Predicate dimensions pushed into Windows Event Log selection.</summary>
    public EventFilter? NativeFilter { get; }

    /// <summary>Exact predicate evaluated after typed projection. Native-pushed nodes may be retained for verification.</summary>
    public EventPredicate? ManagedPredicate { get; }

    /// <summary>Per-node planning explanation.</summary>
    public IReadOnlyList<EventPredicatePlanStep> Steps { get; }

    /// <summary>Whether every selection node has a native prefilter. Exact typed verification may still follow.</summary>
    public bool IsFullyNative => HasNativeFilter && Steps.All(static step =>
        step.Stage == EventPredicatePlanStage.Native ||
        string.Equals(step.Expression, "Exact predicate verification", StringComparison.Ordinal));

    /// <summary>Whether the plan pushes at least one dimension into the native query.</summary>
    public bool HasNativeFilter => NativeFilter?.HasAny == true;
}
