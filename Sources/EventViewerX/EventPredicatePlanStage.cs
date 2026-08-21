namespace EventViewerX;

/// <summary>Execution stage selected for a typed predicate node.</summary>
public enum EventPredicatePlanStage {
    /// <summary>The Windows Event Log engine evaluates the predicate before projection.</summary>
    Native,
    /// <summary>EventViewerX evaluates the predicate after typed projection.</summary>
    Managed
}
